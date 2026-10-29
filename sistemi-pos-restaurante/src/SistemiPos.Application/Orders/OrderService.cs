using System.Text.Json;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Catalog;
using SistemiPos.Application.Common.Exceptions;
using SistemiPos.Application.Recipes;
using SistemiPos.Application.Sales;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Orders;
using SistemiPos.Domain.Recipes;

namespace SistemiPos.Application.Orders;

public sealed class OrderService(IOrderRepository repository, IRecipeRepository recipes, ISaleRepository sales,
    IUnitOfWork unitOfWork, ICurrentUserAccessor user, IOperationContextAccessor context,
    IOperationAuthorizationService authorization, IDateTimeProvider clock, IProductPricingService pricing) : IOrderService
{
    private async Task<Guid> AuthorizeAsync(string permission, CancellationToken ct)
    {
        if (!user.UserId.HasValue || !await authorization.HasPermissionAsync(user.UserName, permission, ct))
            throw new DomainRuleException("No tienes permiso para realizar esta operación de pedidos.");
        return context.CurrentBranchId ?? throw new DomainRuleException("Selecciona una sucursal operativa.");
    }

    public async Task<IReadOnlyList<TerminalDto>> GetTerminalsAsync(CancellationToken ct = default)
    {
        var branchId = await AuthorizeAsync(PermissionCodes.OrdersView, ct);
        return (await repository.GetTerminalsAsync(branchId, ct))
            .Select(x => new TerminalDto(x.Id, x.BranchId, x.WarehouseId, x.Code, x.RegisterCode)).ToList();
    }

    public async Task<Guid> CreateTerminalAsync(SaveTerminalRequest request, CancellationToken ct = default)
    {
        var branchId = await AuthorizeAsync(PermissionCodes.PosTerminalsManage, ct);
        return await repository.InTransactionAsync(async () =>
        {
            var warehouse = await repository.GetWarehouseAsync(request.WarehouseId, ct)
                ?? throw new NotFoundException("No se encontró la bodega.");
            var terminal = new PosTerminal(branchId, warehouse, request.Code, request.RegisterCode);
            if (await repository.TerminalCodeExistsAsync(branchId, terminal.Code, null, ct))
                throw new DomainRuleException("Ya existe una terminal con ese código en esta sucursal.");
            await repository.AddTerminalAsync(terminal, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return terminal.Id;
        }, ct);
    }

    public async Task<TerminalDto> GetTerminalForEditAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await AuthorizeAsync(PermissionCodes.PosTerminalsManage, ct);
        var terminal = await repository.GetTerminalAsync(id, ct);
        if (terminal is null || terminal.BranchId != branchId)
            throw new NotFoundException("No se encontró la terminal en esta sucursal.");
        return new(terminal.Id, terminal.BranchId, terminal.WarehouseId, terminal.Code, terminal.RegisterCode);
    }

    public async Task UpdateTerminalAsync(Guid id, SaveTerminalRequest request, CancellationToken ct = default)
    {
        var branchId = await AuthorizeAsync(PermissionCodes.PosTerminalsManage, ct);
        await repository.InTransactionAsync(async () =>
        {
            var terminal = await repository.GetTerminalAsync(id, ct);
            if (terminal is null || terminal.BranchId != branchId)
                throw new NotFoundException("No se encontró la terminal en esta sucursal.");
            var warehouse = await repository.GetWarehouseAsync(request.WarehouseId, ct)
                ?? throw new NotFoundException("No se encontró la bodega.");
            var proposed = new PosTerminal(branchId, warehouse, request.Code, request.RegisterCode);
            if (await repository.TerminalCodeExistsAsync(branchId, proposed.Code, id, ct))
                throw new DomainRuleException("Ya existe una terminal con ese código en esta sucursal.");
            if ((terminal.WarehouseId != proposed.WarehouseId || terminal.RegisterCode != proposed.RegisterCode)
                && await repository.HasTerminalOperationsAsync(id, branchId, terminal.RegisterCode, proposed.RegisterCode, ct))
                throw new DomainRuleException("Para cambiar la caja o bodega, cierra los turnos de las cajas involucradas y completa o cancela los pedidos pendientes de esta terminal.");
            terminal.Update(warehouse, proposed.Code, proposed.RegisterCode);
            await unitOfWork.SaveChangesAsync(ct);
            return terminal.Id;
        }, ct);
    }

    public async Task<IReadOnlyList<OrderDto>> GetOrdersAsync(CancellationToken ct = default)
    {
        var branchId = await AuthorizeAsync(PermissionCodes.OrdersView, ct);
        return (await repository.GetOrdersAsync(branchId, ct)).Select(Map).ToList();
    }

    public async Task<OrderDto> GetOrderAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await AuthorizeAsync(PermissionCodes.OrdersView, ct);
        return Map(await FindAsync(id, branchId, ct));
    }

    private async Task<RestaurantOrder> FindAsync(Guid id, Guid branchId, CancellationToken ct)
    {
        var order = await repository.GetOrderAsync(id, ct);
        if (order is null || order.BranchId != branchId) throw new NotFoundException("No se encontró el pedido en esta sucursal.");
        return order;
    }

    public async Task<Guid> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        var branchId = await AuthorizeAsync(PermissionCodes.OrdersCreate, ct);
        return await repository.InTransactionAsync(async () =>
        {
            var terminal = await repository.GetTerminalAsync(request.TerminalId, ct)
                ?? throw new NotFoundException("No se encontró la terminal.");
            if (terminal.BranchId != branchId || !terminal.IsActive)
                throw new DomainRuleException("La terminal no está disponible en esta sucursal.");
            await ValidateContextAsync(terminal, request.CashSessionId, ct);
            var lines = await BuildLinesAsync(request.Lines, branchId, false, ct);
            var order = new RestaurantOrder(terminal, request.CashSessionId, lines);
            await repository.AddOrderAsync(order, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return order.Id;
        }, ct);
    }

    private async Task ValidateContextAsync(PosTerminal terminal, Guid sessionId, CancellationToken ct)
    {
        var warehouse = await repository.GetWarehouseAsync(terminal.WarehouseId, ct);
        var session = await sales.GetCashSessionAsync(sessionId, ct);
        if (warehouse is null || !warehouse.IsActive || warehouse.BranchId != terminal.BranchId
            || session is null || session.Status != CashSessionStatus.Active || session.BranchId != terminal.BranchId
            || !string.Equals(session.RegisterCode, terminal.RegisterCode, StringComparison.OrdinalIgnoreCase))
            throw new DomainRuleException("La terminal, caja y bodega deben estar activas y pertenecer a la misma ubicación.");
        if (!string.Equals(session.CashierUserName, user.UserName, StringComparison.OrdinalIgnoreCase))
            throw new DomainRuleException("El pedido debe utilizar la caja abierta del usuario actual.");
    }

    public async Task ReviseLineAsync(Guid id, Guid lineId, ReviseOrderLineRequest request, CancellationToken ct = default)
    {
        var branch = await AuthorizeAsync(PermissionCodes.OrdersUpdate, ct);
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500)
            throw new DomainRuleException("La revisión requiere un motivo de hasta 500 caracteres.");
        if (string.IsNullOrWhiteSpace(request.OperationId) || request.OperationId.Trim().Length > 80)
            throw new DomainRuleException("El identificador de revisión es obligatorio y admite hasta 80 caracteres.");
        await repository.InTransactionAsync(async () =>
        {
            var order = await FindAsync(id, branch, ct);
            var replay = order.Lines.SingleOrDefault(x => x.RevisionOperationId == request.OperationId.Trim());
            if (replay is not null)
            {
                if (replay.ReplacesLineId != lineId || replay.ProductId != request.ProductId || replay.Quantity != request.Quantity
                    || order.Events.Single(x => x.OperationId == request.OperationId.Trim()).Reason != request.Reason.Trim())
                    throw new DomainRuleException("La revisión ya corresponde a otros datos.");
                return false;
            }
            var previous = order.Line(lineId);
            var oldProduct = await repository.GetProductAsync(previous.ProductId, ct);
            var newProduct = await repository.GetProductAsync(request.ProductId, ct);
            if (newProduct is null || oldProduct is null || oldProduct.Id != newProduct.Id
                && (!oldProduct.ParentProductId.HasValue || oldProduct.ParentProductId != newProduct.ParentProductId))
                throw new DomainRuleException("La revisión admite el mismo producto o una variante configurada del mismo producto base.");
            var terminal = await repository.GetTerminalAsync(order.TerminalId, ct) ?? throw new NotFoundException("Terminal no encontrada.");
            await ValidateContextAsync(terminal, order.CashSessionId, ct);
            if (order.CurrencyCode is not null && order.CurrencyCode != (await sales.GetSystemSettingAsync(ct)).CurrencyCode)
                throw new DomainRuleException("La moneda del pedido cambió; revisa la operación.");
            var lines = await BuildLinesAsync([new(request.ProductId, request.Quantity)], branch, true, ct);
            order.AppendRevision(lineId, lines.Single(), request.OperationId, request.Reason, clock.UtcNow, user.UserName);
            await repository.TrackNewDetailsAsync(order, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, ct);
    }

    public async Task ApplyAsync(Guid id, OrderActionRequest request, CancellationToken ct = default)
    {
        var permission = request.Type switch
        {
            OrderEventType.Confirm => PermissionCodes.OrdersUpdate,
            OrderEventType.StartPreparation or OrderEventType.FinishPreparation => PermissionCodes.KitchenOrdersStatus,
            OrderEventType.Dispatch => PermissionCodes.OrdersDispatch,
            OrderEventType.Cancel => PermissionCodes.OrdersCancel,
            _ => throw new DomainRuleException("La operación de pedido no es válida.")
        };
        var branchId = await AuthorizeAsync(permission, ct);
        if (string.IsNullOrWhiteSpace(request.OperationId) || request.OperationId.Trim().Length > 80)
            throw new DomainRuleException("El identificador de operación es obligatorio y admite hasta 80 caracteres.");
        await repository.InTransactionAsync(async () =>
        {
            var order = await FindAsync(id, branchId, ct);
            var extras = (request.ExtraPackaging ?? []).Where(x => x.ProductId != Guid.Empty || x.Quantity != 0).OrderBy(x => x.ProductId).ToList();
            if (extras.Count > 10 || extras.Select(x => x.ProductId).Distinct().Count() != extras.Count
                || extras.Count > 0 && request.Type != OrderEventType.Dispatch)
                throw new DomainRuleException("Los empaques adicionales solo se registran al despachar, sin duplicados.");
            var extrasJson = extras.Count == 0 ? null : JsonSerializer.Serialize(extras);
            if (order.IsReplay(request.OperationId, request.Type, request.LineId, request.Quantity, request.Reason, extrasJson)) return false;
            var terminal = await repository.GetTerminalAsync(order.TerminalId, ct)
                ?? throw new NotFoundException("No se encontró la terminal del pedido.");
            if (!terminal.IsActive) throw new DomainRuleException("La terminal del pedido está inactiva.");
            // Kitchen staff need permissions and branch scope, not the cashier's credentials.
            var warehouse = await repository.GetWarehouseAsync(order.WarehouseId, ct);
            if (warehouse is null || !warehouse.IsActive || warehouse.BranchId != branchId)
                throw new DomainRuleException("La bodega del pedido no está disponible en esta sucursal.");
            if (request.Type == OrderEventType.Confirm)
            {
                await ValidateContextAsync(terminal, order.CashSessionId, ct);
                var refreshed = await BuildLinesAsync(order.Lines.Select(x => new OrderLineRequest(x.ProductId, x.Quantity)).ToList(),
                    branchId, true, ct);
                order.ReplaceDraftLines(refreshed);
                order.SetCurrency((await sales.GetSystemSettingAsync(ct)).CurrencyCode);
            }
            order.Apply(request.Type, request.LineId, request.Quantity, request.OperationId, clock.UtcNow, user.UserName, request.Reason, extrasJson);
            var consumption = new Dictionary<Guid, decimal>();
            if (request.Type == OrderEventType.StartPreparation)
            {
                var line = order.Line(request.LineId!.Value);
                var formula = JsonSerializer.Deserialize<RecipeFormula>(line.RecipeJson!)
                    ?? throw new DomainRuleException("No se encontró la receta histórica del pedido.");
                consumption = new Dictionary<Guid, decimal>(OrderRecipeSnapshot.Consumption(formula, request.Quantity));
            }
            else if (request.Type == OrderEventType.Dispatch)
            {
                var line = order.Line(request.LineId!.Value);
                if (!line.RequiresPreparation) consumption[line.ProductId] = request.Quantity;
            }
            foreach (var extra in extras)
            {
                RecipeRules.ValidateQuantity(extra.Quantity, "La cantidad de empaque adicional");
                var packaging = await repository.GetProductAsync(extra.ProductId, ct);
                if (packaging is null || !packaging.IsActive || packaging.ArticleType != ArticleType.Empaque
                    || packaging.BranchSettings.Any(x => x.BranchId == branchId && !x.IsEnabled))
                    throw new DomainRuleException("Selecciona un empaque activo y habilitado en esta sucursal.");
                var line = order.Line(request.LineId!.Value);
                if (line.RecipeJson is not null && OrderRecipeSnapshot.ContainsProduct(JsonSerializer.Deserialize<RecipeFormula>(line.RecipeJson)!, extra.ProductId))
                    throw new DomainRuleException("Este empaque ya se consumió con la receta; no lo registres nuevamente en el despacho.");
                consumption[extra.ProductId] = extra.Quantity;
            }
            decimal consumedCost = 0;
            foreach (var (productId, quantity) in consumption.OrderBy(x => x.Key))
            {
                var component = await repository.GetProductAsync(productId, ct)
                    ?? throw new DomainRuleException("Componente de consumo no encontrado.");
                var stock = await repository.GetStockAsync(productId, order.WarehouseId, ct)
                    ?? throw new DomainRuleException("No existe stock del componente en la bodega del pedido.");
                if (stock.CurrentStock < quantity)
                    throw new DomainRuleException("No hay existencia suficiente para completar la operación del pedido.");
                var previous = stock.CurrentStock;
                stock.ReplaceForSystem(previous - quantity);
                var movement = new InventoryMovement(productId, InventoryMovementType.PosOrderConsumption,
                    quantity, previous, stock.CurrentStock, clock.UtcNow, user.UserName, $"PED-{order.Id:N}",
                    $"{request.Type}; línea {request.LineId}; operación {request.OperationId}", warehouseId: order.WarehouseId,
                    unitCost: component.LastCost);
                consumedCost += quantity * component.LastCost;
                movement.LinkOrderEvent(order.Events.Single(x => x.OperationId == request.OperationId.Trim()).Id);
                await repository.AddMovementAsync(movement, ct);
            }
            if (consumption.Count > 0 && request.LineId.HasValue)
                order.Line(request.LineId.Value).CaptureConsumptionCost(consumedCost,
                    request.Type is OrderEventType.StartPreparation or OrderEventType.Dispatch ? request.Quantity : 0);
            await repository.TrackNewDetailsAsync(order, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }, ct);
    }

    private async Task<List<RestaurantOrderLine>> BuildLinesAsync(IReadOnlyList<OrderLineRequest> requested,
        Guid branchId, bool capture, CancellationToken ct)
    {
        if (requested.Count is 0 or > 200) throw new DomainRuleException("El pedido debe tener entre 1 y 200 líneas.");
        var graph = await recipes.GetRecipesAsync(ct);
        var versions = await recipes.GetVersionsAsync(ct);
        var capturedVersions = versions.Select(x => (x.RecipeId, x.Number)).ToHashSet();
        var settings = await sales.GetSystemSettingAsync(ct);
        var result = new List<RestaurantOrderLine>();
        foreach (var item in requested)
        {
            var product = await repository.GetProductAsync(item.ProductId, ct)
                ?? throw new NotFoundException("No se encontró el producto del pedido.");
            if (!product.IsActive || product.ManagesVariants || product.ArticleType is ArticleType.Insumo or ArticleType.Ingrediente or ArticleType.Subreceta or ArticleType.Empaque
                || product.BranchSettings.Any(x => x.BranchId == branchId && !x.IsEnabled))
                throw new DomainRuleException("El producto no está habilitado para venta en esta sucursal.");
            var quote = pricing.Calculate(product, item.Quantity, branchId);
            string? formula = null;
            int? revision = null;
            if (product.ArticleType == ArticleType.Preparado)
            {
                var recipe = graph.SingleOrDefault(x => x.ProductId == product.Id && x.IsActive)
                    ?? throw new DomainRuleException("El producto preparado requiere una receta activa.");
                formula = JsonSerializer.Serialize(OrderRecipeSnapshot.Capture(recipe, graph, versions));
                revision = recipe.Revision;
                if (capture && capturedVersions.Add((recipe.Id, recipe.Revision)))
                    await recipes.CaptureVersionAsync(recipe, ct);

            }
            result.Add(new RestaurantOrderLine(product.Id, product.DisplayName, item.Quantity,
                quote.UnitPrice, quote.GrossAmount, settings.TaxPercent, !product.AppliesTax, formula, revision,
                product.ParentProductId.HasValue ? JsonSerializer.Serialize(new { product.ParentProductId, product.VariantValue,
                    Options = product.VariantOptions.OrderBy(x => x.SortOrder).Select(x => new { x.VariantOptionId,
                        Attribute = x.VariantOption.VariantType.Name, x.VariantOption.Value }).ToList() }) : null));
        }
        return result;
    }

    private static OrderDto Map(RestaurantOrder x) => new(x.Id, x.TerminalId, x.BranchId, x.WarehouseId,
        x.CashSessionId, x.IsConfirmed, x.Revision,
        x.Lines.Select(l => new OrderLineDto(l.Id, l.ProductId, l.ProductName, l.Quantity, l.UnitPrice, l.GrossAmount,
            l.TaxPercent, l.IsTaxExempt, l.RecipeRevision, l.RequiresPreparation, l.PendingQuantity, l.PreparingQuantity,
            l.PreparedQuantity, l.DispatchedQuantity, l.CancelledQuantity, l.CancelledAfterPreparationQuantity, l.SoldQuantity, l.SellableQuantity)).ToList(),
        x.Events.OrderBy(e => e.OccurredAtUtc).Select(e => new OrderEventDto(e.Id, e.Type, e.LineId, e.Quantity,
            e.OperationId, e.OccurredAtUtc, e.UserName, e.Reason)).ToList());
}
