using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Catalog;
using SistemiPos.Application.Common.Exceptions;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Customers;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Finance;
using SistemiPos.Domain.Sales;
using SistemiPos.Application.Orders;
using SistemiPos.Domain.Orders;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SistemiPos.Application.Sales;

public sealed class SaleService(
    ISaleRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUserAccessor currentUserAccessor,
    IDateTimeProvider dateTimeProvider,
    IProductPricingService? productPricingService = null,
    IOperationAuthorizationService? operationAuthorizationService = null,
    IOperationContextAccessor? operationContext = null,
    IOrderRepository? orderRepository = null,
    IOfflinePosProtector? offlineProtector = null) : ISaleService
{
    private readonly IProductPricingService _productPricingService = productPricingService ?? ProductPricingService.Instance;
    public async Task<IReadOnlyList<PaymentMethodOption>> GetOtherPaymentMethodsAsync(CancellationToken ct = default) =>
        (await repository.GetPaymentDefinitionsAsync(ct)).Where(x => x.IsActive && x.Behavior == PaymentFinancialBehavior.OtherNonCash)
            .Select(x => new PaymentMethodOption(x.Id, x.Name)).ToList();

    public async Task<IReadOnlyList<TerminalDto>> GetPosTerminalsAsync(CancellationToken ct = default)
    {
        if (orderRepository is null || operationContext?.CurrentBranchId is not Guid branchId) return [];
        var session = await repository.GetActiveCashSessionByUserAsync(currentUserAccessor.UserName, ct);
        return (await orderRepository.GetTerminalsAsync(branchId, ct))
            .Where(x => session is null ||
                (string.Equals(x.RegisterCode, session.RegisterCode, StringComparison.OrdinalIgnoreCase)
                 && (!session.TerminalId.HasValue || x.Id == session.TerminalId.Value)))
            .Select(x => new TerminalDto(x.Id, x.BranchId, x.WarehouseId, x.Code, x.RegisterCode)).ToList();
    }

    public async Task<OfflinePosTicket> CreateOfflineTicketAsync(Guid sessionId, Guid? terminalId = null, CancellationToken ct = default)
    {
        var terminals = await GetPosTerminalsAsync(ct);
        var terminal = terminalId.HasValue ? terminals.SingleOrDefault(x => x.Id == terminalId)
            : terminals.Count == 1 ? terminals[0] : null;
        var session = await repository.GetCashSessionAsync(sessionId, ct);
        if (terminal is null || session is null || session.Status != CashSessionStatus.Active
            || !string.Equals(session.CashierUserName, currentUserAccessor.UserName, StringComparison.OrdinalIgnoreCase)
            || terminal.BranchId != session.BranchId || !currentUserAccessor.UserId.HasValue || offlineProtector is null)
            throw new DomainRuleException("Selecciona una terminal compatible y una caja activa antes de preparar el modo offline.");
        var warehouse = await orderRepository!.GetWarehouseAsync(terminal.WarehouseId, ct);
        if (warehouse is null || !warehouse.IsActive || warehouse.BranchId != terminal.BranchId)
            throw new DomainRuleException("La bodega de la terminal offline no está disponible.");
        var settings = await repository.GetSystemSettingAsync(ct);
        if (session.CurrencyCode is not null && session.CurrencyCode != settings.CurrencyCode)
            throw new DomainRuleException("Cierra la caja en su moneda original antes de preparar ventas con otra moneda.");
        var context = new OfflinePosContext(currentUserAccessor.UserId.Value, currentUserAccessor.UserName,
            terminal.BranchId, terminal.WarehouseId, terminal.Id, session.Id, dateTimeProvider.UtcNow, settings.CurrencyCode);
        return new(context, offlineProtector.Protect(context));
    }
    public async Task<IReadOnlyDictionary<Guid, decimal>> GetOfflineStocksAsync(string ticket, CancellationToken ct = default)
    {
        var context = offlineProtector?.Unprotect(ticket);
        if (context is null || context.UserId != currentUserAccessor.UserId || context.BranchId != operationContext?.CurrentBranchId || orderRepository is null)
            throw new DomainRuleException("El catálogo offline no pertenece al contexto operativo.");
        return await orderRepository.GetStocksAsync(context.WarehouseId, ct);
    }

    public async Task<CheckoutQuote> QuoteAsync(IReadOnlyList<SaleItemRequest> items, Guid? orderId = null, CancellationToken ct = default, int? people = null)
    {
        RestaurantOrder? order = null;
        if (orderId.HasValue)
        {
            order = orderRepository is null ? null : await orderRepository.GetOrderAsync(orderId.Value, ct);
            if (order is null || order.BranchId != operationContext?.CurrentBranchId || !order.IsConfirmed)
                throw new DomainRuleException("No se encontró un pedido confirmado en la sucursal operativa.");
        }
        if (items.Count is 0 or > 200) throw new DomainRuleException("Selecciona las cantidades que deseas cobrar.");
        var lines = new List<CheckoutQuoteLine>();
        foreach (var item in items)
        {
            var product = await repository.GetProductAsync(item.ProductId, ct) ?? throw new NotFoundException("Producto no encontrado.");
            var source = order is null ? null : order.Line(item.OrderLineId ?? Guid.Empty);
            if (source is not null && source.ProductId != item.ProductId) throw new DomainRuleException("La línea no corresponde al producto.");
            var gross = source is null ? _productPricingService.Calculate(product, item.Quantity, operationContext?.CurrentBranchId).GrossAmount : source.QuoteGross(item.Quantity);
            var discount = item.DiscountValue.HasValue && item.DiscountType == SaleDiscountType.FixedAmount ? item.DiscountValue.Value
                : decimal.Round(gross * (item.DiscountValue ?? item.DiscountPercent) / 100m, 2);
            if (discount < 0 || discount > gross) throw new DomainRuleException("El descuento no es válido.");
            lines.Add(new(item.ProductId, item.OrderLineId, source?.ProductName ?? product.DisplayName, item.Quantity, gross, discount, gross - discount));
        }
        var total = lines.Sum(x => x.Total);
        return new CheckoutQuote(lines, total, people.HasValue ? SaleAccountPart.Divide(total, people.Value) : null);
    }

    public async Task<IReadOnlyList<SaleDto>> GetSalesAsync(CancellationToken cancellationToken = default)
    {
        var sales = await repository.GetSalesAsync(cancellationToken);
        return sales.OrderByDescending(x => x.SoldAtUtc).Select(Map).ToList();
    }

    public async Task<SaleDto?> GetSaleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sale = await repository.GetSaleAsync(id, cancellationToken);
        return sale is null ? null : Map(sale);
    }

    public Task<Guid> CreateSaleAsync(CreateSaleRequest request, CancellationToken cancellationToken = default) =>
        orderRepository is null ? CreateSaleCoreAsync(request, cancellationToken)
            : orderRepository.InTransactionAsync(() => CreateSaleCoreAsync(request, cancellationToken), cancellationToken);

    private async Task<Guid> CreateSaleCoreAsync(CreateSaleRequest request, CancellationToken cancellationToken)
    {
        if (request.Items.Count is 0 or > 200 || request.Items.Any(x => x.Quantity <= 0 || decimal.Round(x.Quantity, 2) != x.Quantity))
            throw new DomainRuleException("Selecciona cantidades comerciales positivas con hasta dos decimales.");
        if (request.ClientOperationId?.Trim().Length > 80)
            throw new DomainRuleException("El identificador de operación admite hasta 80 caracteres.");
        OfflinePosContext? offline = null;
        if (request.LocalCreatedAtUtc.HasValue && offlineProtector is not null)
        {
            offline = offlineProtector.Unprotect(request.OfflineTicket ?? string.Empty);
            if (offline.UserId != currentUserAccessor.UserId || offline.BranchId != operationContext?.CurrentBranchId
                || offline.CashSessionId != request.CashSessionId || request.RestaurantOrderId.HasValue
                || request.LocalCreatedAtUtc < offline.CapturedAtUtc.AddMinutes(-5)
                || request.LocalCreatedAtUtc > offline.CapturedAtUtc.AddDays(7)
                || request.LocalCreatedAtUtc > dateTimeProvider.UtcNow.AddMinutes(5)
                || request.ExpectedTotal is null || request.Items.Any(x => (x.DiscountValue ?? x.DiscountPercent) > 0)
                || (request.Payments ?? [request.Payment]).Any(x => x.PaymentMethod == PaymentMethodType.Credit))
                throw new DomainRuleException("La venta offline no corresponde al contexto autorizado o supera sus límites.");
        }
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            request with { ClientOperationId = null, DiscountAuthorizerPassword = null }))));
        var cashSession = await repository.GetCashSessionAsync(request.CashSessionId, cancellationToken)
            ?? throw new NotFoundException("La sesión de caja no fue encontrada.");
        if (operationContext?.CurrentBranchId is Guid currentBranchId
            && cashSession.BranchId != currentBranchId)
            throw new DomainRuleException("La caja no pertenece a la sucursal operativa actual.");

        if (!string.IsNullOrWhiteSpace(request.ClientOperationId)
            && await repository.ExistsClientOperationIdAsync(request.ClientOperationId, cancellationToken))
        {
            var existingSale = await repository.GetByClientOperationIdAsync(request.ClientOperationId, cancellationToken);
            if (existingSale is null
                || existingSale.CashSessionId != request.CashSessionId
                || existingSale.RequestFingerprint is not null && existingSale.RequestFingerprint != fingerprint
                || !string.Equals(existingSale.CashierUserName, currentUserAccessor.UserName, StringComparison.OrdinalIgnoreCase))
                throw new DomainRuleException("El identificador de la operación offline ya fue utilizado por otra venta.");
            return existingSale.Id;
        }

        var settings = await repository.GetSystemSettingAsync(cancellationToken);
        if (cashSession.CurrencyCode is not null && cashSession.CurrencyCode != settings.CurrencyCode)
            throw new DomainRuleException("La moneda de la caja y la moneda activa no coinciden. Cierra la caja antes de operar con otra moneda.");
        if (offline is not null && offline.CurrencyCode != settings.CurrencyCode)
            throw new DomainRuleException("La moneda cambió desde la captura offline; la operación requiere revisión.");
        RestaurantOrder? order = null;
        if (request.RestaurantOrderId is Guid orderId)
        {
            order = orderRepository is null ? null : await orderRepository.GetOrderAsync(orderId, cancellationToken);
            if (order is null || !order.IsConfirmed || order.BranchId != cashSession.BranchId)
                throw new DomainRuleException("El pedido debe estar confirmado y pertenecer a la sucursal del cobro.");
            if (order.CurrencyCode is not null && order.CurrencyCode != settings.CurrencyCode)
                throw new DomainRuleException("La moneda activa cambió desde la confirmación del pedido; revisa la operación antes de cobrar.");
            if (request.Items.Any(x => !x.OrderLineId.HasValue)
                || request.Items.Select(x => x.OrderLineId).Distinct().Count() != request.Items.Count)
                throw new DomainRuleException("Selecciona una sola asignación por línea del pedido.");
        }
        else if (request.Items.Any(x => x.OrderLineId.HasValue))
            throw new DomainRuleException("Las líneas de pedido requieren identificar el pedido de origen.");

        if (cashSession.Status != CashSessionStatus.Active)
        {
            throw new DomainRuleException("La venta requiere una caja activa.");
        }
        if (!string.Equals(cashSession.CashierUserName, currentUserAccessor.UserName, StringComparison.OrdinalIgnoreCase))
            throw new DomainRuleException("La venta debe registrarse en la caja abierta del usuario actual.");
        if ((request.Payments ?? [request.Payment]).Any(p => p.PaymentMethod == PaymentMethodType.Pos
            && cashSession.AssignedPosDevices.Count > 0
            && cashSession.AssignedPosDevices.All(x => x.PosDeviceId != p.PosDeviceId)))
            throw new DomainRuleException("El POS seleccionado no está asignado a la caja actual.");

        var customer = await ResolveCustomerAsync(request, cancellationToken);
        var customerId = customer?.Id;

        var userCanDiscount = await repository.UserHasPermissionAsync(currentUserAccessor.UserName, PermissionCodes.DiscountsApply, cancellationToken);
        var lineDiscountAudits = new List<(string ProductName, SaleDiscountType Type, decimal Value,
            decimal Amount, string? AuthorizedBy, DateTimeOffset? AuthorizedAt, string? Comment)>();

        Guid? saleWarehouseId = null;
        if (cashSession.BranchId.HasValue)
        {
            var defaultWarehouse = await repository.GetDefaultWarehouseForBranchAsync(cashSession.BranchId.Value, cancellationToken);
            if (defaultWarehouse is null || !defaultWarehouse.IsActive
                || defaultWarehouse.BranchId != cashSession.BranchId.Value)
                throw new DomainRuleException("La caja requiere una bodega activa de su misma sucursal.");
            saleWarehouseId = defaultWarehouse?.Id;
        }
        if (!saleWarehouseId.HasValue && operationContext?.CurrentWarehouseId.HasValue == true)
        {
            saleWarehouseId = operationContext.CurrentWarehouseId.Value;
        }
        if (order is not null) saleWarehouseId = order.WarehouseId;
        var selectedTerminalId = offline?.TerminalId ?? request.TerminalId ?? (order is not null && order.CashSessionId == cashSession.Id ? order.TerminalId : (Guid?)null);
        if (cashSession.TerminalId.HasValue && selectedTerminalId.HasValue && cashSession.TerminalId != selectedTerminalId)
            throw new DomainRuleException("La terminal seleccionada no corresponde al turno abierto.");
        string? terminalCode = null;
        if (!selectedTerminalId.HasValue && orderRepository is not null && cashSession.BranchId.HasValue)
        {
            var candidates = (await orderRepository.GetTerminalsAsync(cashSession.BranchId.Value, cancellationToken))
                .Where(x => string.Equals(x.RegisterCode, cashSession.RegisterCode, StringComparison.OrdinalIgnoreCase)).ToList();
            if (candidates.Count != 1) throw new DomainRuleException("Configura y selecciona una terminal para la caja activa.");
            selectedTerminalId = candidates[0].Id;
        }
        if (selectedTerminalId.HasValue)
        {
            var terminal = orderRepository is null ? null : await orderRepository.GetTerminalAsync(selectedTerminalId.Value, cancellationToken);
            var warehouse = terminal is null || orderRepository is null ? null : await orderRepository.GetWarehouseAsync(terminal.WarehouseId, cancellationToken);
            if (terminal is null || !terminal.IsActive || terminal.BranchId != cashSession.BranchId
                || !string.Equals(terminal.RegisterCode, cashSession.RegisterCode, StringComparison.OrdinalIgnoreCase)
                || warehouse is null || !warehouse.IsActive || warehouse.BranchId != cashSession.BranchId)
                throw new DomainRuleException("La terminal y su bodega no son compatibles con la caja activa.");
            saleWarehouseId = terminal.WarehouseId;
            terminalCode = terminal.Code;
            if (order is not null && saleWarehouseId != order.WarehouseId)
                throw new DomainRuleException("La terminal de cobro debe utilizar la bodega de consumo del pedido.");
            if (offline is not null && saleWarehouseId != offline.WarehouseId)
                throw new DomainRuleException("La bodega de la terminal cambió desde la captura offline.");
        }
        if (orderRepository is not null && !selectedTerminalId.HasValue)
            throw new DomainRuleException("La venta requiere una terminal autorizada.");

        var items = new List<SaleItem>();
        var requestedByProduct = new Dictionary<Guid, decimal>();
        foreach (var item in request.Items)
        {
            var product = await repository.GetProductAsync(item.ProductId, cancellationToken)
                ?? throw new NotFoundException("Producto no encontrado.");
            var sourceLine = order is null ? null : order.Line(item.OrderLineId!.Value);
            if (sourceLine is not null && sourceLine.ProductId != item.ProductId)
                throw new DomainRuleException("El producto no corresponde a la línea del pedido.");
            if (sourceLine is null && product.ArticleType is SistemiPos.Domain.Catalog.ArticleType.Preparado
                or SistemiPos.Domain.Catalog.ArticleType.Subreceta or SistemiPos.Domain.Catalog.ArticleType.Insumo or SistemiPos.Domain.Catalog.ArticleType.Empaque)
                throw new DomainRuleException("Los preparados requieren un pedido preparado; los insumos, subrecetas y empaques no se venden directamente.");
            if (request.LocalCreatedAtUtc.HasValue && product.ArticleType != SistemiPos.Domain.Catalog.ArticleType.Reventa)
                throw new DomainRuleException("Offline admite exclusivamente venta directa de reventa.");

            if (product.ArticleType == SistemiPos.Domain.Catalog.ArticleType.Ingrediente)
                throw new DomainRuleException("Los ingredientes se consumen mediante recetas y no se venden directamente.");

            if (!product.IsActive)
            {
                throw new DomainRuleException("No se puede vender un producto inactivo.");
            }

            var branchId = cashSession.BranchId ?? operationContext?.CurrentBranchId;
            if (product.BranchSettings.Any(x => x.BranchId == branchId && !x.IsEnabled))
                throw new DomainRuleException("El producto no está habilitado para la sucursal de la venta.");

            var stock = sourceLine is not null ? null : saleWarehouseId.HasValue
                ? await repository.GetStockAsync(product.Id, saleWarehouseId.Value, cancellationToken)
                : await repository.GetStockAsync(product.Id, cancellationToken);
            if (stock is null && sourceLine is null)
                throw new NotFoundException("Existencia no encontrada para el producto.");

            var requestedQuantity = requestedByProduct.GetValueOrDefault(product.Id) + item.Quantity;
            if (sourceLine is null && stock!.CurrentStock < requestedQuantity)
            {
                throw new DomainRuleException("No hay existencia suficiente para confirmar la venta.");
            }
            requestedByProduct[product.Id] = requestedQuantity;

            var price = sourceLine is null ? _productPricingService.Calculate(product, item.Quantity, branchId)
                : new ProductPriceQuote(sourceLine.UnitPrice, sourceLine.QuoteGross(item.Quantity), null, null);
            var discountType = item.DiscountValue.HasValue
                ? item.DiscountType
                : item.DiscountPercent > 0 ? SaleDiscountType.Percentage : SaleDiscountType.None;
            var discountValue = item.DiscountValue ?? item.DiscountPercent;
            if (!Enum.IsDefined(discountType))
                throw new DomainRuleException("El tipo de descuento no es válido.");
            if (discountValue < 0)
                throw new DomainRuleException("El descuento no puede ser negativo.");

            var discountPercent = discountType == SaleDiscountType.Percentage ? discountValue : 0m;
            decimal? fixedDiscountAmount = discountType == SaleDiscountType.FixedAmount ? discountValue : null;
            if (discountType == SaleDiscountType.None && discountValue != 0)
                throw new DomainRuleException("El tipo de descuento no es válido.");
            if (discountPercent > 100m)
                throw new DomainRuleException("El descuento no puede superar el total de la línea.");

            var requestedDiscountAmount = fixedDiscountAmount
                ?? decimal.Round(price.GrossAmount * discountPercent / 100m, 2);
            if (requestedDiscountAmount > price.GrossAmount)
                throw new DomainRuleException("El descuento no puede superar el total de la línea.");

            if (requestedDiscountAmount > 0)
            {
                if (!settings.DiscountsEnabled || !userCanDiscount)
                    throw new DomainRuleException("El usuario no tiene permiso para aplicar descuentos.");

            }

            var saleItem = new SaleItem(
                product.Id,
                sourceLine?.ProductName ?? product.DisplayName,
                item.Quantity,
                price.UnitPrice,
                discountPercent,
                sourceLine?.TaxPercent ?? settings.TaxPercent,
                sourceLine?.IsTaxExempt ?? !product.AppliesTax,
                product.LastCost,
                price.GrossAmount,
                fixedDiscountAmount);
            items.Add(saleItem);
            if (sourceLine is not null)
            {
                saleItem.SetOrderSnapshot(sourceLine.Id, sourceLine.RecipeJson, sourceLine.ModifiersJson);
                if(sourceLine.CapturedConsumptionCost.HasValue && sourceLine.StartedQuantity > 0)
                    saleItem.CaptureConsumedCost(sourceLine.CapturedConsumptionCost.Value/sourceLine.StartedQuantity);
            }
            else saleItem.CaptureModifiers(OrderRecipeSnapshot.Modifiers(product));

            if (saleItem.DiscountAmount > 0)
                lineDiscountAudits.Add((product.DisplayName, discountType, discountValue,
                    saleItem.DiscountAmount, item.DiscountAuthorizedByUserName, item.DiscountAuthorizedAtUtc, item.DiscountComment));
        }

        DiscountApprovalRequest? discountApproval = null;
        if (items.Any(x => x.DiscountAmount > 0))
        {
            if (request.DiscountApprovalRequestId is null)
                throw new DomainRuleException("Los descuentos requieren aprobación antes de confirmar la venta.");
            discountApproval = await repository.GetDiscountApprovalAsync(request.DiscountApprovalRequestId.Value, cancellationToken)
                ?? throw new DomainRuleException("La solicitud de aprobación del descuento no existe.");
            ValidateDiscountApproval(discountApproval, cashSession.Id, currentUserAccessor.UserName, request.Items, items);
        }

        var total = decimal.Round(items.Sum(x => x.Subtotal), 2);
        if (request.ExpectedTotal.HasValue && request.ExpectedTotal != total)
            throw new DomainRuleException(offline is null ? "El total cambió. Recalcula la cuenta antes de confirmar el cobro."
                : "El total o precio cambió desde la venta offline. La operación se conserva para revisión.");
        var paymentRequests = request.Payments ?? [request.Payment];
        if (paymentRequests.Count is 0 or > 10) throw new DomainRuleException("Registra entre uno y diez pagos.");
        var payments = new List<SalePayment>();
        foreach (var paymentRequest in paymentRequests)
        {
            var applied = paymentRequests.Count == 1 ? total : paymentRequest.AppliedAmount
                ?? throw new DomainRuleException("Indica el importe aplicado de cada pago mixto.");
            if (applied < 0 || decimal.Round(applied, 2) != applied)
                throw new DomainRuleException("El importe del pago no es válido.");
            payments.Add(await BuildPaymentAsync(paymentRequest, applied, settings, cancellationToken));
        }
        if (payments.Any(p => p.PaymentMethod == PaymentMethodType.Transfer && p.TransferStatus is not TransferPaymentStatus.Received and not TransferPaymentStatus.Verified
            || p.PaymentMethod == PaymentMethodType.Pos && p.CardStatus != CardPaymentStatus.Approved))
            throw new DomainRuleException("Confirma la recepción o aprobación de todos los pagos antes de registrar la venta.");
        var payment = payments[0];
        var number = await repository.GenerateSaleNumberAsync(cancellationToken);
        var soldAtUtc = request.LocalCreatedAtUtc ?? dateTimeProvider.UtcNow;

        var sale = new Sale(
            number,
            cashSession.Id,
            customerId,
            soldAtUtc,
            currentUserAccessor.UserName,
            items,
            payment,
            settings.CurrencyCode,
            settings.CurrencyName,
            settings.CurrencySymbol,
            settings.CurrencyDecimals,
            settings.DecimalSeparator,
            settings.ThousandsSeparator,
            settings.CurrencySymbolPosition.ToString(),
            NormalizeCustomerNitForSnapshot(request.CustomerNit),
            FirstNotBlank(request.CustomerName, customer?.Name),
            FirstNotBlank(request.CustomerAddress, customer?.Address),
            FirstNotBlank(request.CustomerPhone, customer?.Phone),
            FirstNotBlank(request.CustomerVisitOrigin, customer?.VisitOrigin),
            request.ClientOperationId,
            warehouseId: saleWarehouseId, payments: payments);
        sale.SetRequestFingerprint(fingerprint);
        if (selectedTerminalId.HasValue) sale.LinkTerminal(selectedTerminalId.Value, terminalCode);
        if (request.SplitPeople.HasValue && request.SplitAmounts is not null)
            throw new DomainRuleException("Selecciona división por personas o por importes.");
        var splitAmounts = request.SplitPeople.HasValue ? SaleAccountPart.Divide(total, request.SplitPeople.Value) : request.SplitAmounts;
        if (splitAmounts is not null) sale.SplitAccount(splitAmounts, paymentRequests.Select(x => x.AccountPartNumber).ToList());
        if (order is not null) sale.LinkOrder(order.Id);

        if (!string.IsNullOrWhiteSpace(request.ClientOperationId))
            sale.MarkSynchronized(dateTimeProvider.UtcNow);

        string? saleDiscountAuthorizedByUserName = null;
        if (sale.DiscountTotal > 0)
        {
            DateTimeOffset? authorizedAtUtc = null;

            saleDiscountAuthorizedByUserName = discountApproval!.ResolvedByUserName;
            authorizedAtUtc = discountApproval.ResolvedAtUtc;

            sale.RegisterDiscountAudit(
                currentUserAccessor.UserName,
                dateTimeProvider.UtcNow,
                request.DiscountReason,
                saleDiscountAuthorizedByUserName,
                authorizedAtUtc);
        }

        foreach (var item in sale.Items)
        {
            if (order is not null)
            {
                var line = order.Line(item.OrderLineId!.Value);
                if (!line.RequiresPreparation)
                {
                    var toDispatch = Math.Max(0, line.SoldQuantity + item.Quantity - line.DispatchedQuantity);
                    if (toDispatch > 0)
                    {
                        order.Apply(OrderEventType.Dispatch, line.Id, toDispatch, $"SALE-{sale.Id:N}-{line.Id:N}",
                            dateTimeProvider.UtcNow, currentUserAccessor.UserName);
                        var orderStock = await repository.GetStockAsync(item.ProductId, order.WarehouseId, cancellationToken)
                            ?? throw new DomainRuleException("No existe stock de reventa en la bodega del pedido.");
                        if (orderStock.CurrentStock < toDispatch) throw new DomainRuleException("No hay existencia suficiente para despachar la reventa.");
                        var previousStock = orderStock.CurrentStock;
                        orderStock.ReplaceForSystem(previousStock - toDispatch);
                        var movement = new InventoryMovement(item.ProductId, InventoryMovementType.PosOrderConsumption,
                            toDispatch, previousStock, orderStock.CurrentStock, dateTimeProvider.UtcNow, currentUserAccessor.UserName,
                            $"PED-{order.Id:N}", "Despacho al cobrar pedido.", warehouseId: order.WarehouseId);
                        movement.LinkOrderEvent(order.Events.Last().Id);
                        await repository.AddMovementAsync(movement, cancellationToken);
                    }
                }
                order.RecordSale(line.Id, item.Quantity);
                continue;
            }
            var product = await repository.GetProductAsync(item.ProductId, cancellationToken)
                ?? throw new NotFoundException("Producto no encontrado.");
            var stock = saleWarehouseId.HasValue
                ? await repository.GetStockAsync(item.ProductId, saleWarehouseId.Value, cancellationToken)
                : await repository.GetStockAsync(item.ProductId, cancellationToken);
            if (stock is null)
                throw new NotFoundException("Existencia no encontrada para el producto.");
            var previous = stock.CurrentStock;

            stock.ReplaceForSystem(previous - item.Quantity);
            await repository.AddMovementAsync(new InventoryMovement(
                item.ProductId,
                InventoryMovementType.SaleExit,
                item.Quantity,
                previous,
                stock.CurrentStock,
                dateTimeProvider.UtcNow,
                currentUserAccessor.UserName,
                sale.Number,
                "Salida por venta.",
                warehouseId: saleWarehouseId), cancellationToken);
        }

        foreach (var salePayment in sale.Payments)
        {
            var cashMovement = CreateCashMovement(sale, salePayment);
            if (cashMovement is null) continue;
            cashMovement.CashSessionId = cashSession.Id;
            cashSession.AddMovement(cashMovement);
            await repository.AddCashMovementAsync(cashMovement, cancellationToken);
        }
        await repository.AddSaleAsync(sale, cancellationToken);
        cashSession.RecordOperation();
        if (order is not null) await orderRepository!.TrackNewDetailsAsync(order, cancellationToken);
        discountApproval?.MarkUsed(sale.Id);
        foreach (var discount in lineDiscountAudits)
        {
            var typeLabel = discount.Type == SaleDiscountType.FixedAmount ? "Monto fijo" : "Porcentaje";
            var valueLabel = discount.Type == SaleDiscountType.FixedAmount
                ? $"{settings.CurrencySymbol} {discount.Value:N2}"
                : $"{discount.Value:N2}%";
            await repository.AddFinancialAuditAsync(new SistemiPos.Domain.Audit.FinancialAuditEntry
            {
                OccurredAtUtc = discount.AuthorizedAt ?? dateTimeProvider.UtcNow,
                UserName = currentUserAccessor.UserName,
                Action = "Descuento por producto",
                CashSessionId = cashSession.Id,
                DocumentType = nameof(Sale),
                DocumentId = sale.Id,
                DocumentReference = sale.Number,
                Amount = discount.Amount,
                AuthorizedByUserName = discount.AuthorizedBy ?? saleDiscountAuthorizedByUserName,
                Comment = $"Producto: {discount.ProductName}. Tipo: {typeLabel}. Valor: {valueLabel}. Monto descontado: {settings.CurrencySymbol} {discount.Amount:N2}." +
                    (string.IsNullOrWhiteSpace(discount.Comment) ? string.Empty : $" Comentario: {discount.Comment.Trim()}")
            }, cancellationToken);
        }
        if (sale.Payment.PaymentMethod == PaymentMethodType.Credit)
        {
            if (sale.CustomerId is null || customer is null) throw new DomainRuleException("Una venta a crédito requiere un cliente registrado.");
            var dueDateUtc = soldAtUtc.AddDays(customer.CreditDays);
            await repository.AddReceivableAccountAsync(new ReceivableAccount(
                sale.Id, sale.CustomerId.Value, sale.Number, soldAtUtc, dueDateUtc, sale.Total,
                customer.CreditDays, customer.GraceDays), cancellationToken);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return sale.Id;
    }

    public Task<Guid> RequestDiscountApprovalAsync(Guid cashSessionId, IReadOnlyList<SaleItemRequest> requestedItems, CancellationToken cancellationToken = default) =>
        RequestDiscountApprovalCoreAsync(cashSessionId, requestedItems, null, cancellationToken);
    public Task<Guid> RequestOrderDiscountApprovalAsync(Guid cashSessionId, Guid orderId, IReadOnlyList<SaleItemRequest> items, CancellationToken ct = default) =>
        RequestDiscountApprovalCoreAsync(cashSessionId, items, orderId, ct);
    private async Task<Guid> RequestDiscountApprovalCoreAsync(Guid cashSessionId, IReadOnlyList<SaleItemRequest> requestedItems, Guid? orderId, CancellationToken cancellationToken)
    {
        RestaurantOrder? order = orderId.HasValue && orderRepository is not null ? await orderRepository.GetOrderAsync(orderId.Value, cancellationToken) : null;
        if (orderId.HasValue && (order is null || !order.IsConfirmed || order.BranchId != operationContext?.CurrentBranchId))
            throw new DomainRuleException("El descuento requiere un pedido confirmado de la caja y sucursal operativa.");
        var session = await repository.GetCashSessionAsync(cashSessionId, cancellationToken)
            ?? throw new NotFoundException("La sesión de caja no fue encontrada.");
        if (session.Status != CashSessionStatus.Active || !string.Equals(session.CashierUserName, currentUserAccessor.UserName, StringComparison.OrdinalIgnoreCase))
            throw new DomainRuleException("La solicitud debe pertenecer a la caja activa del vendedor.");
        if (operationContext?.CurrentBranchId is Guid approvalBranch && session.BranchId != approvalBranch)
            throw new DomainRuleException("La solicitud de descuento no pertenece a la sucursal operativa.");
        var settings = await repository.GetSystemSettingAsync(cancellationToken);
        if (!settings.DiscountsEnabled || !await repository.UserHasPermissionAsync(currentUserAccessor.UserName, PermissionCodes.DiscountsApply, cancellationToken))
            throw new DomainRuleException("El usuario no tiene permiso para aplicar descuentos.");

        var snapshots = new List<DiscountApprovalItem>();
        foreach (var item in requestedItems.Where(x => x.Quantity > 0))
        {
            var product = await repository.GetProductAsync(item.ProductId, cancellationToken)
                ?? throw new NotFoundException("Producto no encontrado.");
            var branchId = session.BranchId ?? operationContext?.CurrentBranchId;
            if (product.BranchSettings.Any(x => x.BranchId == branchId && !x.IsEnabled))
                throw new DomainRuleException("El producto no está habilitado para la sucursal de la venta.");
            var origin = order is null ? null : order.Line(item.OrderLineId ?? Guid.Empty);
            if (origin is not null && origin.ProductId != product.Id)
                throw new DomainRuleException("El producto no corresponde a la línea del pedido.");
            var price = origin is null ? _productPricingService.Calculate(product, item.Quantity, branchId)
                : new ProductPriceQuote(origin.UnitPrice, origin.QuoteGross(item.Quantity), null, null);
            var type = item.DiscountValue.HasValue ? item.DiscountType : item.DiscountPercent > 0 ? SaleDiscountType.Percentage : SaleDiscountType.None;
            var value = item.DiscountValue ?? item.DiscountPercent;
            var amount = type == SaleDiscountType.FixedAmount ? value : decimal.Round(price.GrossAmount * value / 100m, 2);
            if (value < 0 || amount > price.GrossAmount || (type == SaleDiscountType.Percentage && value > 100m))
                throw new DomainRuleException("El descuento no puede superar el total de la línea.");
            snapshots.Add(new DiscountApprovalItem(product.Id, product.DisplayName, item.Quantity, price.UnitPrice,
                type, value, price.GrossAmount, Math.Max(0, amount), item.DiscountComment));
        }

        var approval = new DiscountApprovalRequest(cashSessionId, currentUserAccessor.UserName, dateTimeProvider.UtcNow, snapshots);
        await repository.AddDiscountApprovalAsync(approval, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return approval.Id;
    }

    public async Task<DiscountApprovalDto?> GetDiscountApprovalAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var approval = await repository.GetDiscountApprovalAsync(id, cancellationToken);
        if (approval is null) return null;
        var canAuthorize = await repository.UserHasPermissionAsync(currentUserAccessor.UserName, PermissionCodes.DiscountsAuthorize, cancellationToken);
        if (!canAuthorize && !string.Equals(approval.RequestedByUserName, currentUserAccessor.UserName, StringComparison.OrdinalIgnoreCase))
            throw new DomainRuleException("No tiene permiso para consultar esta solicitud.");
        return MapApproval(approval);
    }

    public async Task<IReadOnlyList<DiscountApprovalDto>> GetPendingDiscountApprovalsAsync(CancellationToken cancellationToken = default)
    {
        if (!await repository.UserHasPermissionAsync(currentUserAccessor.UserName, PermissionCodes.DiscountsAuthorize, cancellationToken))
            throw new DomainRuleException("No tiene permiso para autorizar descuentos.");
        return (await repository.GetPendingDiscountApprovalsAsync(cancellationToken)).Select(MapApproval).ToList();
    }

    public async Task DecideDiscountApprovalAsync(Guid id, bool approve, string? comment, CancellationToken cancellationToken = default)
    {
        if (!await repository.UserHasPermissionAsync(currentUserAccessor.UserName, PermissionCodes.DiscountsAuthorize, cancellationToken))
            throw new DomainRuleException("No tiene permiso para autorizar descuentos.");
        var approval = await repository.GetDiscountApprovalAsync(id, cancellationToken)
            ?? throw new NotFoundException("Solicitud de descuento no encontrada.");
        if (string.Equals(approval.RequestedByUserName, currentUserAccessor.UserName, StringComparison.OrdinalIgnoreCase))
            throw new DomainRuleException("La solicitud debe ser resuelta por un usuario superior distinto al vendedor.");
        if (approve) approval.Approve(currentUserAccessor.UserName, dateTimeProvider.UtcNow, comment);
        else approval.Reject(currentUserAccessor.UserName, dateTimeProvider.UtcNow, comment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateDiscountApproval(DiscountApprovalRequest approval, Guid cashSessionId, string seller,
        IReadOnlyList<SaleItemRequest> requests, IReadOnlyList<SaleItem> saleItems)
    {
        if (approval.Status != DiscountApprovalStatus.Approved)
            throw new DomainRuleException(approval.Status == DiscountApprovalStatus.Rejected
                ? $"El descuento fue rechazado{(string.IsNullOrWhiteSpace(approval.DecisionComment) ? "." : $": {approval.DecisionComment}")}" : "El descuento todavía está pendiente de aprobación.");
        if (approval.CashSessionId != cashSessionId || !string.Equals(approval.RequestedByUserName, seller, StringComparison.OrdinalIgnoreCase))
            throw new DomainRuleException("La aprobación no corresponde a esta caja y vendedor.");

        var expected = requests.Zip(saleItems).Select(pair => (pair.First.ProductId, pair.First.Quantity,
            pair.First.DiscountValue.HasValue ? pair.First.DiscountType : pair.First.DiscountPercent > 0 ? SaleDiscountType.Percentage : SaleDiscountType.None,
            pair.First.DiscountValue ?? pair.First.DiscountPercent, pair.Second.DiscountAmount))
            .OrderBy(x => x.ProductId).ThenBy(x => x.Quantity).ThenBy(x => x.Item3).ThenBy(x => x.Item4).ToList();
        var approved = approval.Items.Select(x => (x.ProductId, x.Quantity, x.DiscountType, x.DiscountValue, x.DiscountAmount))
            .OrderBy(x => x.ProductId).ThenBy(x => x.Quantity).ThenBy(x => x.DiscountType).ThenBy(x => x.DiscountValue).ToList();
        if (!expected.SequenceEqual(approved))
            throw new DomainRuleException("El carrito cambió después de solicitar la aprobación. Envíe una nueva solicitud.");
    }

    private static DiscountApprovalDto MapApproval(DiscountApprovalRequest x) => new(x.Id, x.CashSessionId,
        x.RequestedByUserName, x.RequestedAtUtc, x.Status, x.ResolvedByUserName, x.ResolvedAtUtc,
        x.DecisionComment, x.GrossTotal, x.DiscountTotal, x.NetTotal,
        x.Items.Select(i => new DiscountApprovalItemDto(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice,
            i.DiscountType, i.DiscountValue, i.GrossAmount, i.DiscountAmount, i.Comment)).ToList());

    public Task CancelSaleAsync(Guid saleId, CancelSaleRequest request, CancellationToken cancellationToken = default) =>
        FinancialTransactionAsync(() => CancelSaleCoreAsync(saleId, request, cancellationToken), cancellationToken);
    private async Task CancelSaleCoreAsync(Guid saleId, CancelSaleRequest request, CancellationToken cancellationToken)
    {
        var authorizedBy = await ResolveSensitiveAuthorizationAsync(PermissionCodes.SalesCancelWithoutAuthorization,
            PermissionCodes.SalesCancelAuthorize, request.AuthorizerUserName, request.AuthorizerPassword, cancellationToken);
        var sale = await repository.GetSaleAsync(saleId, cancellationToken)
            ?? throw new NotFoundException("Venta no encontrada.");

        if (await repository.GetRefundedTotalAsync(sale.Id, cancellationToken) > 0)
            throw new DomainRuleException("La venta ya tiene reembolsos; procesa el saldo mediante devoluciones y reembolsos.");
        sale.Cancel();

        var cancelWarehouseId = sale.WarehouseId;
        if (!cancelWarehouseId.HasValue && sale.CashSessionId != Guid.Empty)
        {
            var session = await repository.GetCashSessionAsync(sale.CashSessionId, cancellationToken);
            if (session?.BranchId.HasValue == true)
            {
                var defaultWarehouse = await repository.GetDefaultWarehouseForBranchAsync(session.BranchId.Value, cancellationToken);
                cancelWarehouseId = defaultWarehouse?.Id;
            }
        }

        foreach (var item in sale.Items)
        {
            if (sale.RestaurantOrderId.HasValue) continue;
            var stock = cancelWarehouseId.HasValue
                ? await repository.GetStockAsync(item.ProductId, cancelWarehouseId.Value, cancellationToken)
                : await repository.GetStockAsync(item.ProductId, cancellationToken);
            if (stock is null)
                throw new NotFoundException("Existencia no encontrada para el producto.");
            var previous = stock.CurrentStock;
            stock.ReplaceForSystem(previous + item.Quantity);

            await repository.AddMovementAsync(new InventoryMovement(
                item.ProductId,
                InventoryMovementType.CancellationReversal,
                item.Quantity,
                previous,
                stock.CurrentStock,
                dateTimeProvider.UtcNow,
                currentUserAccessor.UserName,
                sale.Number,
                "Reversión por anulación.",
                warehouseId: cancelWarehouseId), cancellationToken);
        }

        var cancellationNumber = await repository.GenerateCancellationNumberAsync(cancellationToken);
        await repository.AddCancellationAsync(new SaleCancellation(cancellationNumber, sale.Id, request.Reason, currentUserAccessor.UserName, dateTimeProvider.UtcNow, authorizedBy), cancellationToken);
        Guid? affectedSessionId = null;
        foreach (var originalPayment in sale.Payments.Where(x => x.PaymentMethod != PaymentMethodType.Credit && x.TotalAmount > 0))
        {
            var currentSession = await repository.GetActiveCashSessionByUserAsync(currentUserAccessor.UserName, cancellationToken)
                ?? throw new DomainRuleException("Debe abrir su caja para registrar el reembolso de la anulación.");
            if (operationContext?.CurrentBranchId is Guid refundBranch && currentSession.BranchId != refundBranch)
                throw new DomainRuleException("La caja del reembolso no pertenece a la sucursal operativa.");
            if (currentSession.CurrencyCode is not null && currentSession.CurrencyCode != sale.CurrencyCode)
                throw new DomainRuleException("El reembolso requiere una caja en la moneda original de la venta.");
            var method = originalPayment.PaymentMethod switch { PaymentMethodType.Cash => RefundMethodType.Cash,
                PaymentMethodType.Transfer => RefundMethodType.Transfer, PaymentMethodType.Other => RefundMethodType.Other, _ => RefundMethodType.Pos };
            var movementType = method switch { RefundMethodType.Cash => CashMovementType.RefundCash,
                RefundMethodType.Transfer => CashMovementType.RefundTransfer, RefundMethodType.Other => CashMovementType.RefundOther, _ => CashMovementType.RefundPos };
            var refund = new CashMovement(movementType, originalPayment.TotalAmount, $"Anulación {sale.Number}", currentUserAccessor.UserName,
                dateTimeProvider.UtcNow, authorizedBy, nameof(SaleCancellation), sale.Id, cancellationNumber) { CashSessionId = currentSession.Id };
            currentSession.AddMovement(refund); affectedSessionId = currentSession.Id;
            await repository.AddCashMovementAsync(refund, cancellationToken);
            await repository.AddRefundAsync(new SaleRefund(sale.Id, currentSession.Id, originalPayment.TotalAmount, method,
                request.Reason, currentUserAccessor.UserName, authorizedBy ?? currentUserAccessor.UserName,
                dateTimeProvider.UtcNow, cancellationNumber, originalPayment.Id), cancellationToken);
        }
        if (sale.Payment.PaymentMethod == PaymentMethodType.Credit)
        {
            var receivable = await repository.GetReceivableAccountBySaleAsync(sale.Id, cancellationToken);
            receivable?.Cancel();
        }
        await repository.AddFinancialAuditAsync(new SistemiPos.Domain.Audit.FinancialAuditEntry
        {
            OccurredAtUtc = dateTimeProvider.UtcNow, UserName = currentUserAccessor.UserName, Action = "Anulación de venta",
            CashSessionId = affectedSessionId, DocumentType = nameof(Sale), DocumentId = sale.Id, DocumentReference = sale.Number,
            Amount = sale.Total, AuthorizedByUserName = authorizedBy, Comment = request.Reason
        }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task ReturnSaleAsync(Guid saleId, ReturnSaleRequest request, CancellationToken cancellationToken = default) =>
        FinancialTransactionAsync(() => ReturnSaleCoreAsync(saleId, request, cancellationToken), cancellationToken);
    private async Task ReturnSaleCoreAsync(Guid saleId, ReturnSaleRequest request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0 || request.Items.Any(x => x.Quantity <= 0 || decimal.Round(x.Quantity, 2) != x.Quantity))
            throw new DomainRuleException("Selecciona cantidades positivas y válidas para la devolución.");
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500 || request.OperationId?.Trim().Length > 80)
            throw new DomainRuleException("La devolución requiere motivo e identificador de operación válidos.");
        var sale = await repository.GetSaleAsync(saleId, cancellationToken)
            ?? throw new NotFoundException("Venta no encontrada.");
        var returnFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            request with { OperationId = null, AuthorizerPassword = null }))));
        if (!string.IsNullOrWhiteSpace(request.OperationId))
        {
            var previous = await repository.GetReturnByOperationAsync(saleId, request.OperationId, cancellationToken);
            if (previous is not null)
            {
                if (previous.RequestFingerprint != returnFingerprint || !string.Equals(previous.UserName, currentUserAccessor.UserName, StringComparison.OrdinalIgnoreCase))
                    throw new DomainRuleException("El identificador ya corresponde a otra devolución.");
                return;
            }
        }
        var authorizedBy = await ResolveSensitiveAuthorizationAsync(PermissionCodes.SalesReturnWithoutAuthorization,
            PermissionCodes.SalesReturnAuthorize, request.AuthorizerUserName, request.AuthorizerPassword, cancellationToken);

        var returnWarehouseId = sale.WarehouseId;
        if (!returnWarehouseId.HasValue && sale.CashSessionId != Guid.Empty)
        {
            var session = await repository.GetCashSessionAsync(sale.CashSessionId, cancellationToken);
            if (session?.BranchId.HasValue == true)
            {
                var defaultWarehouse = await repository.GetDefaultWarehouseForBranchAsync(session.BranchId.Value, cancellationToken);
                returnWarehouseId = defaultWarehouse?.Id;
            }
        }

        var saleItemsByProduct = sale.Items.GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity));
        var returnedByProduct = await repository.GetReturnedQuantitiesByProductAsync(sale.Id, cancellationToken);
        var requestedReturnsByProduct = new Dictionary<Guid, decimal>();

        foreach (var item in request.Items)
        {
            if (!saleItemsByProduct.TryGetValue(item.ProductId, out var soldQuantity))
            {
                throw new DomainRuleException("La devolución excede la cantidad vendida.");
            }

            var alreadyReturned = returnedByProduct.GetValueOrDefault(item.ProductId);
            var requestedReturn = requestedReturnsByProduct.GetValueOrDefault(item.ProductId) + item.Quantity;
            if (alreadyReturned + requestedReturn > soldQuantity)
            {
                throw new DomainRuleException("La devolución excede la cantidad vendida.");
            }
            requestedReturnsByProduct[item.ProductId] = requestedReturn;

            if (item.ReturnedToInventory)
            {
                if (sale.Items.Any(x => x.ProductId == item.ProductId && x.RecipeJson is not null))
                    throw new DomainRuleException("Un preparado no repone ingredientes ni empaques consumidos al devolverse.");
                var stock = returnWarehouseId.HasValue
                    ? await repository.GetStockAsync(item.ProductId, returnWarehouseId.Value, cancellationToken)
                    : await repository.GetStockAsync(item.ProductId, cancellationToken);
                if (stock is null)
                    throw new NotFoundException("Existencia no encontrada para el producto.");
                var previous = stock.CurrentStock;
                stock.ReplaceForSystem(previous + item.Quantity);

                var movement = new InventoryMovement(
                    item.ProductId,
                    InventoryMovementType.ReturnEntry,
                    item.Quantity,
                    previous,
                    stock.CurrentStock,
                    dateTimeProvider.UtcNow,
                    currentUserAccessor.UserName,
                    sale.Number,
                    "Entrada por devolución.",
                    warehouseId: returnWarehouseId);
                movement.SetLotDetails(null, selectedLotId: item.LotId);
                if (sale.RestaurantOrderId.HasValue) movement.SetOrderReturn(sale.RestaurantOrderId.Value);
                await repository.AddMovementAsync(movement, cancellationToken);
            }
        }

        var returnNumber = await repository.GenerateReturnNumberAsync(cancellationToken);
        var saleReturn = new SaleReturn(
            returnNumber,
            sale.Id,
            request.Reason,
            currentUserAccessor.UserName,
            dateTimeProvider.UtcNow,
            request.Items.Select(x => new SaleReturnItem(x.ProductId, x.Quantity, x.ReturnedToInventory)),
            authorizedBy, request.OperationId, returnFingerprint);

        var totalReturned = returnedByProduct.Values.Sum() + request.Items.Sum(x => x.Quantity);
        if (totalReturned >= sale.Items.Sum(x => x.Quantity))
        {
            sale.MarkReturned();
        }
        else
        {
            sale.MarkPartiallyReturned();
        }

        await repository.AddReturnAsync(saleReturn, cancellationToken);
        sale.TouchFinancialState();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task RefundSaleAsync(Guid saleId, RefundSaleRequest request, CancellationToken cancellationToken = default) =>
        FinancialTransactionAsync(() => RefundSaleCoreAsync(saleId, request, cancellationToken), cancellationToken);
    private async Task RefundSaleCoreAsync(Guid saleId, RefundSaleRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500 || request.OperationId?.Trim().Length > 80)
            throw new DomainRuleException("El reembolso requiere motivo e identificador de operación válidos.");
        var sale = await repository.GetSaleAsync(saleId, cancellationToken)
            ?? throw new NotFoundException("Venta no encontrada.");

        var refundedTotal = await repository.GetRefundedTotalAsync(sale.Id, cancellationToken);
        if (request.Amount <= 0 || decimal.Round(request.Amount, 2) != request.Amount || sale.Status == SaleStatus.Cancelled)
            throw new DomainRuleException("El importe o estado de la venta no admite este reembolso.");
        var originalPayment = request.PaymentId.HasValue ? sale.Payments.SingleOrDefault(x => x.Id == request.PaymentId)
            : sale.Payments.Count == 1 ? sale.Payments.Single() : null;
        if (originalPayment is null || originalPayment.PaymentMethod == PaymentMethodType.Credit)
            throw new DomainRuleException("Selecciona el pago original que se reembolsa.");
        if (!string.IsNullOrWhiteSpace(request.OperationId))
        {
            var previous = await repository.GetRefundByOperationAsync(saleId, request.OperationId, cancellationToken);
            if (previous is not null)
            {
                var reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim();
                if (previous.Amount != request.Amount || previous.PaymentId != originalPayment.Id || previous.RefundMethod != request.RefundMethod
                    || previous.CashSessionId != request.CashSessionId || previous.Reason != request.Reason.Trim() || previous.Reference != reference
                    || !string.Equals(previous.ProcessedByUserName, currentUserAccessor.UserName, StringComparison.OrdinalIgnoreCase))
                    throw new DomainRuleException("El identificador ya corresponde a otro reembolso.");
                return;
            }
        }
        if (await repository.GetRefundedPaymentTotalAsync(originalPayment.Id, cancellationToken) + request.Amount > originalPayment.TotalAmount)
            throw new DomainRuleException("El reembolso supera el saldo del pago original.");
        var originalMethod = originalPayment.PaymentMethod switch { PaymentMethodType.Cash => RefundMethodType.Cash,
            PaymentMethodType.Transfer => RefundMethodType.Transfer, PaymentMethodType.Pos => RefundMethodType.Pos, _ => RefundMethodType.Other };
        if (originalMethod != request.RefundMethod
            && !await repository.UserHasPermissionAsync(currentUserAccessor.UserName, PermissionCodes.SalesRefundDifferentMethod, cancellationToken))
            throw new DomainRuleException("No tienes permiso para reembolsar por otro medio.");
        if (refundedTotal + request.Amount > sale.Total)
        {
            throw new DomainRuleException("El reembolso excede el total pendiente de la venta.");
        }

        var refundAuthorizedBy = operationAuthorizationService is null
            ? request.AuthorizedByUserName
            : await operationAuthorizationService.ValidateCredentialsAsync(request.AuthorizedByUserName, request.AuthorizerPassword ?? string.Empty,
                PermissionCodes.SalesReturnAuthorize, cancellationToken);

        var cashSession = await repository.GetCashSessionAsync(request.CashSessionId, cancellationToken)
            ?? throw new NotFoundException("La sesión de caja no fue encontrada.");

        if (cashSession.Status != CashSessionStatus.Active)
        {
            throw new DomainRuleException("El reembolso requiere una caja activa.");
        }
        if (!string.Equals(cashSession.CashierUserName, currentUserAccessor.UserName, StringComparison.OrdinalIgnoreCase))
            throw new DomainRuleException("El reembolso debe afectar la caja abierta del usuario que lo realiza.");
        if (operationContext?.CurrentBranchId is Guid branchId && cashSession.BranchId != branchId)
            throw new DomainRuleException("La caja del reembolso no pertenece a la sucursal operativa.");
        if (cashSession.CurrencyCode is not null && cashSession.CurrencyCode != sale.CurrencyCode)
            throw new DomainRuleException("El reembolso requiere una caja en la moneda original de la venta.");

        var refund = new SaleRefund(
            sale.Id,
            cashSession.Id,
            request.Amount,
            request.RefundMethod,
            request.Reason,
            currentUserAccessor.UserName,
            refundAuthorizedBy,
            dateTimeProvider.UtcNow,
            request.Reference, originalPayment.Id, request.OperationId);
        sale.TouchFinancialState();

        var refundMovement = request.RefundMethod switch
        {
            RefundMethodType.Cash => new CashMovement(CashMovementType.RefundCash, request.Amount, $"Reembolso {sale.Number}", currentUserAccessor.UserName, dateTimeProvider.UtcNow, sourceType: nameof(SaleRefund), sourceId: refund.Id, sourceDocument: sale.Number),
            RefundMethodType.Transfer => new CashMovement(CashMovementType.RefundTransfer, request.Amount, $"Reembolso {sale.Number}", currentUserAccessor.UserName, dateTimeProvider.UtcNow, sourceType: nameof(SaleRefund), sourceId: refund.Id, sourceDocument: sale.Number),
            RefundMethodType.Pos => new CashMovement(CashMovementType.RefundPos, request.Amount, $"Reembolso {sale.Number}", currentUserAccessor.UserName, dateTimeProvider.UtcNow, sourceType: nameof(SaleRefund), sourceId: refund.Id, sourceDocument: sale.Number),
            RefundMethodType.Other => new CashMovement(CashMovementType.RefundOther, request.Amount, $"Reembolso {sale.Number}", currentUserAccessor.UserName, dateTimeProvider.UtcNow, sourceType: nameof(SaleRefund), sourceId: refund.Id, sourceDocument: sale.Number),
            _ => throw new DomainRuleException("El medio de reembolso no es válido.")
        };
        refundMovement.CashSessionId = cashSession.Id;
        cashSession.AddMovement(refundMovement);
        await repository.AddCashMovementAsync(refundMovement, cancellationToken);

        await repository.AddFinancialAuditAsync(new SistemiPos.Domain.Audit.FinancialAuditEntry
        {
            OccurredAtUtc = dateTimeProvider.UtcNow, UserName = currentUserAccessor.UserName, Action = "Reembolso de venta",
            CashSessionId = cashSession.Id, DocumentType = nameof(Sale), DocumentId = sale.Id, DocumentReference = sale.Number,
            Amount = request.Amount, AuthorizedByUserName = refundAuthorizedBy, Comment = request.Reason
        }, cancellationToken);

        await repository.AddRefundAsync(refund, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task VerifyTransferAsync(Guid saleId, string? observation, CancellationToken cancellationToken = default)
    {
        if (!await repository.UserHasPermissionAsync(currentUserAccessor.UserName, PermissionCodes.TransfersVerify, cancellationToken))
        {
            throw new DomainRuleException("El usuario no tiene permiso para verificar transferencias.");
        }

        var sale = await repository.GetSaleAsync(saleId, cancellationToken)
            ?? throw new NotFoundException("Venta no encontrada.");

        foreach (var payment in sale.Payments.Where(x => x.PaymentMethod == PaymentMethodType.Transfer))
            payment.VerifyTransfer(currentUserAccessor.UserName, dateTimeProvider.UtcNow, observation);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectTransferAsync(Guid saleId, string? observation, CancellationToken cancellationToken = default)
    {
        if (!await repository.UserHasPermissionAsync(currentUserAccessor.UserName, PermissionCodes.TransfersVerify, cancellationToken))
        {
            throw new DomainRuleException("El usuario no tiene permiso para rechazar transferencias.");
        }

        var sale = await repository.GetSaleAsync(saleId, cancellationToken)
            ?? throw new NotFoundException("Venta no encontrada.");

        foreach (var payment in sale.Payments.Where(x => x.PaymentMethod == PaymentMethodType.Transfer))
            payment.RejectTransfer(currentUserAccessor.UserName, dateTimeProvider.UtcNow, observation);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<SalePayment> BuildPaymentAsync(SalePaymentRequest request, decimal total, SystemSetting settings, CancellationToken cancellationToken)
    {
        if (request.PaymentMethod == PaymentMethodType.Mixed || !await repository.IsPaymentMethodEnabledAsync(request.PaymentMethod, cancellationToken))
            throw new DomainRuleException("La forma de pago no está habilitada.");
        if (request.CardAmount.HasValue && request.PaymentMethod == PaymentMethodType.Pos && request.CardAmount != total
            || request.TransferAmount.HasValue && request.PaymentMethod == PaymentMethodType.Transfer && request.TransferAmount != total)
            throw new DomainRuleException("El importe autorizado debe coincidir con el importe aplicado del pago.");
        return request.PaymentMethod switch
        {
            PaymentMethodType.Cash when !settings.CashPaymentsEnabled => throw new DomainRuleException("La forma de pago en efectivo está deshabilitada."),
            PaymentMethodType.Cash => SalePayment.CreateCash(
                total,
                request.ReceivedAmount ?? 0m,
                currentUserAccessor.UserName),
            PaymentMethodType.Transfer when !settings.TransferPaymentsEnabled => throw new DomainRuleException("La forma de pago por transferencia está deshabilitada."),
            PaymentMethodType.Transfer => await BuildTransferPaymentAsync(request, total, cancellationToken),
            PaymentMethodType.Pos when !settings.PosPaymentsEnabled => throw new DomainRuleException("La forma de pago POS está deshabilitada."),
            PaymentMethodType.Pos => SalePayment.CreatePos(
                total,
                request.CardType ?? throw new DomainRuleException("El tipo de tarjeta es requerido."),
                request.CardLast4 ?? string.Empty,
                request.CardAuthorizationNumber ?? string.Empty,
                request.CardVoucherReference ?? string.Empty,
                request.CardAmount ?? total,
                request.CardStatus ?? CardPaymentStatus.Pending,
                currentUserAccessor.UserName,
                (await repository.GetPosDeviceAsync(request.PosDeviceId ?? Guid.Empty, cancellationToken))?.Id
                    ?? throw new DomainRuleException("Seleccione un dispositivo POS activo.")),
            PaymentMethodType.Credit when !settings.CreditPaymentsEnabled => throw new DomainRuleException("La forma de pago a crédito está deshabilitada."),
            PaymentMethodType.Credit => SalePayment.CreateCredit(total, currentUserAccessor.UserName),
            PaymentMethodType.Other => SalePayment.CreateOther(total,
                (await repository.GetPaymentDefinitionsAsync(cancellationToken)).SingleOrDefault(x => x.Id == request.DefinitionId)
                    ?? throw new DomainRuleException("Selecciona un medio no efectivo activo."), currentUserAccessor.UserName, request.OtherReference),
            _ => throw new DomainRuleException("La forma de pago no es válida.")
        };
    }

    private async Task<string?> ResolveSensitiveAuthorizationAsync(string withoutAuthorizationPermission, string authorizePermission,
        string? authorizerUserName, string? authorizerPassword, CancellationToken cancellationToken)
    {
        if (operationAuthorizationService is null) return authorizerUserName;
        if (await operationAuthorizationService.HasPermissionAsync(currentUserAccessor.UserName, withoutAuthorizationPermission, cancellationToken)) return null;
        return await operationAuthorizationService.ValidateCredentialsAsync(authorizerUserName ?? string.Empty, authorizerPassword ?? string.Empty,
            authorizePermission, cancellationToken);
    }

    private async Task FinancialTransactionAsync(Func<Task> action, CancellationToken ct)
    {
        if (orderRepository is null) { await action(); return; }
        await orderRepository.InTransactionAsync(async () => { await action(); return true; }, ct);
    }

    private async Task<Customer?> ResolveCustomerAsync(CreateSaleRequest request, CancellationToken cancellationToken)
    {
        if (request.CustomerId.HasValue)
        {
            var selectedCustomer = await repository.GetCustomerAsync(request.CustomerId.Value, cancellationToken)
                ?? throw new NotFoundException("Cliente no encontrado.");

            if (!selectedCustomer.IsActive)
            {
                throw new DomainRuleException("No se puede vender a un cliente inactivo.");
            }

            selectedCustomer.CompleteMissingSaleContact(request.CustomerPhone, request.CustomerVisitOrigin);
            return selectedCustomer;
        }

        var normalizedNit = NitNormalizer.Normalize(request.CustomerNit);
        var isFinalConsumer = normalizedNit is null or "CF";
        if (isFinalConsumer)
        {
            var hasPhone = !string.IsNullOrWhiteSpace(request.CustomerPhone);
            var hasVisitOrigin = !string.IsNullOrWhiteSpace(request.CustomerVisitOrigin);
            if (!hasPhone && !hasVisitOrigin) return null;

            if (hasPhone)
            {
                var existingByPhone = await repository.GetActiveCustomerByPhoneAsync(request.CustomerPhone!, cancellationToken);
                if (existingByPhone is not null)
                {
                    existingByPhone.CompleteMissingSaleContact(request.CustomerPhone, request.CustomerVisitOrigin);
                    return existingByPhone;
                }
            }

            var finalConsumer = new Customer(
                string.IsNullOrWhiteSpace(request.CustomerName) ? "Consumidor final" : request.CustomerName,
                null,
                request.CustomerPhone,
                null,
                request.CustomerAddress);
            finalConsumer.CompleteMissingSaleContact(request.CustomerPhone, request.CustomerVisitOrigin);
            await repository.AddCustomerAsync(finalConsumer, cancellationToken);
            return finalConsumer;
        }

        if (normalizedNit is null)
        {
            if (!string.IsNullOrWhiteSpace(request.CustomerName))
            {
                throw new DomainRuleException("El NIT es requerido para guardar el cliente de la venta.");
            }

            return null;
        }

        var existingCustomer = await repository.GetActiveCustomerByNormalizedNitAsync(normalizedNit, cancellationToken);
        if (existingCustomer is not null)
        {
            existingCustomer.UpdateSaleContact(request.CustomerName, request.CustomerAddress);
            existingCustomer.CompleteMissingSaleContact(request.CustomerPhone, request.CustomerVisitOrigin);
            return existingCustomer;
        }

        if (string.IsNullOrWhiteSpace(request.CustomerName))
        {
            throw new DomainRuleException("La razón social es requerida cuando el NIT no existe en clientes.");
        }

        var newCustomer = new Customer(request.CustomerName, request.CustomerNit, request.CustomerPhone, null, request.CustomerAddress);
        newCustomer.CompleteMissingSaleContact(request.CustomerPhone, request.CustomerVisitOrigin);
        await repository.AddCustomerAsync(newCustomer, cancellationToken);
        return newCustomer;
    }

    private static string? FirstNotBlank(string? preferred, string? fallback) =>
        !string.IsNullOrWhiteSpace(preferred) ? preferred.Trim() :
        !string.IsNullOrWhiteSpace(fallback) ? fallback.Trim() : null;

    private static string NormalizeCustomerNitForSnapshot(string? nit) =>
        NitNormalizer.Normalize(nit) is null or "CF" ? "C/F" : nit!.Trim();

    private async Task<SalePayment> BuildTransferPaymentAsync(SalePaymentRequest request, decimal total, CancellationToken cancellationToken)
    {
        var bankAccountId = request.TransferBankAccountId
            ?? throw new DomainRuleException("La cuenta destino es requerida.");
        var bankAccount = await repository.GetBankAccountAsync(bankAccountId, cancellationToken)
            ?? throw new NotFoundException("Cuenta bancaria no encontrada.");

        if (!bankAccount.IsActive)
        {
            throw new DomainRuleException("La cuenta bancaria seleccionada está inactiva.");
        }

        return SalePayment.CreateTransfer(
            total,
            request.TransferSourceBank ?? string.Empty,
            bankAccount.Id,
            bankAccount.Bank,
            bankAccount.AccountName,
            bankAccount.AccountNumber,
            request.TransferReference ?? string.Empty,
            total,
            dateTimeProvider.UtcNow,
            request.TransferStatus ?? TransferPaymentStatus.Received,
            currentUserAccessor.UserName,
            request.TransferSenderName ?? string.Empty);
    }

    private static CashMovement? CreateCashMovement(Sale sale, SalePayment payment) =>
        payment.PaymentMethod switch
        {
            PaymentMethodType.Cash => payment.TotalAmount == 0 ? null : new CashMovement(CashMovementType.SaleCash, payment.TotalAmount, $"Venta {sale.Number}", sale.CashierUserName, sale.SoldAtUtc, sourceType: nameof(SalePayment), sourceId: payment.Id, sourceDocument: sale.Number),
            PaymentMethodType.Transfer => new CashMovement(CashMovementType.SaleTransfer, payment.TotalAmount, $"Venta {sale.Number}", sale.CashierUserName, sale.SoldAtUtc, sourceType: nameof(SalePayment), sourceId: payment.Id, sourceDocument: sale.Number),
            PaymentMethodType.Pos => new CashMovement(CashMovementType.SalePos, payment.TotalAmount, $"Venta {sale.Number}", sale.CashierUserName, sale.SoldAtUtc, sourceType: nameof(SalePayment), sourceId: payment.Id, sourceDocument: sale.Number),
            PaymentMethodType.Credit => null,
            PaymentMethodType.Other => new CashMovement(CashMovementType.SaleOther, payment.TotalAmount, $"Venta {sale.Number}", sale.CashierUserName, sale.SoldAtUtc, sourceType: nameof(SalePayment), sourceId: payment.Id, sourceDocument: sale.Number),
            _ => throw new DomainRuleException("La forma de pago no es válida.")
        };

    private static SaleDto Map(Sale sale) =>
        new(
            sale.Id,
            sale.Number,
            sale.CashSessionId,
            sale.CustomerId,
            sale.CustomerNameSnapshot ?? (sale.CustomerId.HasValue ? sale.Customer?.Name ?? "Cliente" : "Consumidor final"),
            sale.SoldAtUtc,
            sale.CashierUserName,
            sale.Subtotal,
            sale.DiscountTotal,
            sale.TaxableBaseTotal,
            sale.TaxTotal,
            sale.Total,
            sale.DiscountAppliedByUserName,
            sale.DiscountAppliedAtUtc,
            sale.DiscountReason,
            sale.DiscountAuthorizedByUserName,
            sale.DiscountAuthorizedAtUtc,
            sale.CurrencyCode,
            sale.CurrencyName,
            sale.CurrencySymbol,
            sale.CurrencyDecimals,
            sale.DecimalSeparator,
            sale.ThousandsSeparator,
            sale.CurrencySymbolPosition,
            sale.Status,
            MapPayment(sale.Payment),
            sale.Items.Select(x => new SaleItemDto(
                x.ProductId,
                x.ProductName,
                x.Quantity,
                x.UnitPrice,
                x.DiscountPercent,
                x.DiscountAmount,
                x.TaxableBaseAmount,
                x.TaxPercent,
                x.TaxAmount,
                x.Subtotal,
                x.IsTaxExempt,
                x.GrossAmount)).ToList(),
            sale.CustomerNitSnapshot ?? sale.Customer?.Nit,
            sale.ClientOperationId,
            sale.SynchronizedAtUtc,
            sale.Payments.Select(p => MapPayment(p) with { AccountPartNumber = sale.AccountParts.SingleOrDefault(x => x.Id == p.AccountPartId)?.Number }).ToList(),
            sale.RestaurantOrderId, sale.AccountParts.Select(x => new SaleAccountPartDto(x.Number, x.Amount)).ToList(), sale.TerminalCode);

    private static SalePaymentDto MapPayment(SalePayment p) => new(p.PaymentMethod, p.TotalAmount,
        p.ReceivedAmount, p.ChangeAmount, p.TransferSourceBank, p.TransferDestinationBank,
        p.TransferDestinationAccountName, p.TransferDestinationAccountNumber, p.TransferReference, p.TransferAmount,
        p.TransferDateUtc, p.TransferStatus, p.TransferReviewedByUserName, p.TransferReviewedAtUtc,
        p.TransferReviewObservation, p.CardType, p.CardLast4, p.CardAuthorizationNumber, p.CardVoucherReference,
        p.CardAmount, p.CardStatus, p.TransferSenderName, p.Id, DefinitionName: p.DefinitionName, OtherReference: p.OtherReference);
}
