using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Restaurant;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Catalog;

namespace SistemiPos.Application.Restaurant;

public sealed partial class RestaurantService(IRestaurantRepository repository, IOperationContextAccessor context,
    ICurrentUserAccessor user, IOperationAuthorizationService authorization, IDateTimeProvider clock) : IRestaurantService
{
    private async Task<Guid> Branch(string permission, CancellationToken ct)
    {
        if (!user.UserId.HasValue || !await authorization.HasPermissionAsync(user.UserName, permission, ct)) throw new DomainRuleException("No tienes permiso para esta operación.");
        var id = context.CurrentBranchId ?? throw new DomainRuleException("Selecciona una sucursal de trabajo.");
        if (!(await context.GetAuthorizedBranchesAsync(ct)).Any(x => x.Id == id && x.IsActive)) throw new DomainRuleException("Sucursal fuera de tu alcance.");
        return id;
    }
    public async Task<RestaurantWorkspace> WorkspaceAsync(CancellationToken ct = default, Guid? warehouseId = null, bool includeMenu = true)
    {
        var branch = await Branch(PermissionCodes.OrdersView, ct);
        var setup = await repository.SetupAsync(branch, ct);
        var definition = setup?.Read() ?? new RestaurantDefinition();
        var warehouses = await repository.WarehousesAsync(branch, ct);
        if (warehouseId.HasValue && warehouses.All(x => x.Id != warehouseId)) throw new DomainRuleException("Bodega fuera de la sucursal.");
        var warehouse = warehouses.FirstOrDefault(x => x.Id == (warehouseId ?? context.CurrentWarehouseId)) ?? warehouses.OrderByDescending(x => x.IsDefault).FirstOrDefault();
        var products = includeMenu ? await repository.ProductsAsync(ct) : [];
        var menu = new List<RestaurantMenuProduct>();
        foreach (var entry in definition.Menu.Where(x => x.Active && definition.Stations.Any(s => s.Id == x.StationId && s.Active))
            .OrderBy(x => definition.Sections.FirstOrDefault(s => s.Id == x.SectionId)?.SortOrder ?? int.MaxValue)
            .ThenBy(x => x.SortOrder).ThenByDescending(x => x.Favorite))
        {
            var product = products.FirstOrDefault(x => x.Id == entry.ProductId && IsVisible(x, branch));
            if (product is null) continue;
            decimal available = 0;
            if (warehouse is not null) available = await AvailableAsync(product, warehouse.Id, ct);
            var basePrice = entry.CustomPrice ?? Catalog.ProductPricingService.Instance.Calculate(product, 1, branch).GrossAmount;
            var promotion = definition.Promotions.Where(x => x.Active && x.ProductId == product.Id && x.FromUtc <= clock.UtcNow && x.ToUtc > clock.UtcNow)
                .OrderByDescending(x => x.Priority).ThenByDescending(x => x.Percent).ThenBy(x => x.Id).FirstOrDefault();
            var sectionName = definition.Sections.FirstOrDefault(x => x.Id == entry.SectionId && x.Active)?.Name ?? "Sin sección";
            var displayName = !string.IsNullOrWhiteSpace(entry.CustomName) ? entry.CustomName : product.DisplayName;
            menu.Add(new(product.Id, displayName, product.InternalCode, sectionName, product.ImageUrl,
                basePrice - decimal.Round(basePrice * (promotion?.Percent ?? 0) / 100, 2), available, entry.Favorite, entry));
        }
        return new(branch, context.CurrentBranchName ?? "Sucursal", setup?.Revision ?? 0, definition, menu, warehouses,
            await repository.TurnsAsync(branch, ct), await repository.OrdersAsync(branch, ct), warehouse?.Id,
            products.Select(x => new RestaurantComponentName(x.Id, x.DisplayName)).ToList());
    }
    public async Task<IReadOnlyList<Product>> ProductsAsync(CancellationToken ct = default)
    { await Branch(PermissionCodes.RestaurantConfigure, ct); return await repository.ProductsAsync(ct); }
    public async Task<IReadOnlyList<RestaurantMenuCandidate>> MenuCandidatesAsync(CancellationToken ct = default)
    {
        var branch = await Branch(PermissionCodes.RestaurantConfigure, ct);
        var products = (await repository.ProductsAsync(ct)).Where(x => IsVisible(x, branch)).ToList();
        var recipes = await repository.ActiveRecipeSnapshotsAsync(products.Select(x => x.Id).ToArray(), ct);
        return products.Where(x => x.ArticleType != ArticleType.Preparado || recipes.ContainsKey(x.Id))
            .OrderBy(x => x.DisplayName)
            .Select(x =>
            {
                recipes.TryGetValue(x.Id, out var recipe);
                var canExclude = recipe?.ConsumptionMode == Domain.Recipes.RecipeConsumptionMode.OnSale;
                var basePrice = Catalog.ProductPricingService.Instance.Calculate(x, 1, branch).GrossAmount;
                return new RestaurantMenuCandidate(x.Id, x.DisplayName, x.InternalCode, x.Category.Name,
                    x.ArticleType.Label(), recipe is not null, canExclude,
                    canExclude ? recipe!.Components.Select(c =>
                    {
                        var isReq = c.Name.Contains("Masa", StringComparison.OrdinalIgnoreCase) ||
                                    c.Name.Contains("Salsa", StringComparison.OrdinalIgnoreCase) ||
                                    c.Name.Contains("Base", StringComparison.OrdinalIgnoreCase);
                        return new RestaurantComponentName(c.ProductId, c.Name, c.Quantity, c.UnitCode, isReq);
                    }).ToList() : [],
                    basePrice, x.ImageUrl, recipe?.Instructions);
            }).ToList();
    }
    public async Task<RestaurantDefinition> CopyBranchMenuAsync(Guid sourceBranchId, CancellationToken ct = default)
    {
        var currentBranch = await Branch(PermissionCodes.RestaurantConfigure, ct);
        var authorizedBranches = await context.GetAuthorizedBranchesAsync(ct);
        if (!authorizedBranches.Any(x => x.Id == sourceBranchId && x.IsActive))
            throw new DomainRuleException("La sucursal de origen no está autorizada o está inactiva.");
        if (sourceBranchId == currentBranch)
            throw new DomainRuleException("No puedes copiar el menú a la misma sucursal.");

        var sourceSetup = await repository.SetupAsync(sourceBranchId, ct);
        if (sourceSetup is null)
            throw new DomainRuleException("La sucursal seleccionada aún no tiene configuración comercial de restaurante.");

        var sourceDefinition = sourceSetup.Read();
        var currentProducts = (await repository.ProductsAsync(ct)).Where(x => IsVisible(x, currentBranch)).ToDictionary(x => x.Id);
        var compatibleMenu = sourceDefinition.Menu.Where(m => currentProducts.ContainsKey(m.ProductId)).ToList();

        return new RestaurantDefinition
        {
            Channels = sourceDefinition.Channels.Select(x => x with { Id = Guid.NewGuid() }).ToList(),
            Stations = sourceDefinition.Stations.Select(x => x with { Id = Guid.NewGuid() }).ToList(),
            Drivers = sourceDefinition.Drivers.Select(x => x with { Id = Guid.NewGuid() }).ToList(),
            Tables = sourceDefinition.Tables.Select(x => x with { Id = Guid.NewGuid() }).ToList(),
            Zones = sourceDefinition.Zones.Select(x => x with { Id = Guid.NewGuid() }).ToList(),
            Sections = sourceDefinition.Sections.Select(x => x with { Id = Guid.NewGuid() }).ToList(),
            Menu = compatibleMenu,
            Combos = sourceDefinition.Combos,
            Promotions = sourceDefinition.Promotions
        };
    }
    public async Task SaveSetupAsync(RestaurantDefinition definition, int revision, CancellationToken ct = default)
    {
        var branch = await Branch(PermissionCodes.RestaurantConfigure, ct);
        if (definition is null) throw new DomainRuleException("Falta la configuración de restaurante.");
        definition.Validate();
        var products = (await repository.ProductsAsync(ct)).ToDictionary(x => x.Id);
        foreach (var item in definition.Menu)
        {
            if (!products.TryGetValue(item.ProductId, out var product) || !IsVisible(product, branch)) throw new DomainRuleException("El menú contiene un producto no vendible en esta sucursal.");
            var recipe = (await repository.RecipeAsync(item.ProductId, ct))?.Read();
            if (product.ArticleType == ArticleType.Preparado && recipe is null)
                throw new DomainRuleException("Un preparado necesita una receta activa antes de incorporarlo al menú.");
            if (item.ExcludableProducts.Any(id => recipe is null || recipe.ConsumptionMode != Domain.Recipes.RecipeConsumptionMode.OnSale || recipe.Components.All(x => x.ProductId != id)))
                throw new DomainRuleException("Solo puedes permitir exclusiones de componentes directos de la receta elaborada al pedido.");
            foreach (var component in item.Groups.SelectMany(x => x.Options).SelectMany(x => x.Components))
                if (!products.TryGetValue(component.ProductId, out var p) || !p.IsActive || p.ManagesVariants || p.BaseUnit is not { IsActive: true } || decimal.Round(component.Quantity, p.BaseUnit.Precision) != component.Quantity)
                    throw new DomainRuleException("El consumo del extra requiere un artículo activo y cantidad en su unidad base.");
        }
        await repository.TransactionAsync(async () =>
        {
            var setup = await repository.SetupAsync(branch, ct);
            // Stable IDs retain the identity used by open tickets and occupied tables.
            var orders = await repository.OrdersAsync(branch, ct);
            var activeStations = orders.Where(x => x.Status is not (RestaurantOrderStatus.Delivered or RestaurantOrderStatus.Cancelled))
                .SelectMany(x => x.Lines).Where(x => x.ActiveQuantity > 0).Select(x => x.StationId).Distinct();
            if (activeStations.Any(id => definition.Stations.All(x => x.Id != id || !x.Active))) throw new DomainRuleException("Hay comandas abiertas en una estación que deseas desactivar.");
            var turns = await repository.TurnsAsync(branch, ct);
            if (turns.Any(t => !t.ClosedAtUtc.HasValue && definition.Tables.All(x => x.Id != t.TableId || !x.Active))) throw new DomainRuleException("No desactives mesas ocupadas.");
            if (setup is null) { setup = new(branch); repository.Add(setup); }
            setup.Update(definition, revision); await repository.SaveAsync(ct); return true;
        }, ct);
    }
    public async Task<RestaurantOrder> GetAsync(Guid id, CancellationToken ct = default)
    {
        var branch = await Branch(PermissionCodes.OrdersView, ct); return await ScopedOrder(id, branch, ct);
    }
    private async Task<RestaurantOrder> ScopedOrder(Guid id, Guid branch, CancellationToken ct)
    { var order = await repository.OrderAsync(id, ct); if (order is null || order.BranchId != branch) throw new DomainRuleException("Pedido no encontrado en la sucursal."); return order; }
    private async Task<T> Run<T>(Guid branch, Guid operation, object payload, Func<Task<T>> action, Func<T, Guid> resultId, Func<Guid, Task<T>> replay, CancellationToken ct)
    {
        if (operation == Guid.Empty) throw new DomainRuleException("Falta el identificador de operación.");
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))));
        return await repository.TransactionAsync(async () =>
        {
            var existing = await repository.CommandAsync(operation, ct);
            if (existing is not null)
            {
                if (existing.BranchId != branch || existing.UserName != user.UserName || existing.Fingerprint != fingerprint)
                    throw new DomainRuleException("El identificador de operación ya se utilizó con otros datos.");
                return await replay(existing.ResultId);
            }
            var result = await action(); repository.Add(new RestaurantCommand(operation, branch, user.UserName, fingerprint, resultId(result)));
            await repository.SaveAsync(ct); return result;
        }, ct);
    }
    public async Task<Guid> SaveDraftAsync(DraftCommand command, CancellationToken ct = default)
    {
        var branch = await Branch(command.OrderId.HasValue ? PermissionCodes.OrdersUpdate : PermissionCodes.OrdersCreate, ct);
        return await Run(branch, command.OperationId, command, async () =>
        {
            var setup = (await repository.SetupAsync(branch, ct))?.Read() ?? throw new DomainRuleException("Configura primero el menú del restaurante.");
            var input = command.Order;
            if (input is null || input.Notes is null || input.Address is null || input.Phone is null || input.CustomerName is null)
                throw new DomainRuleException("Faltan datos del pedido.");
            if (!(await repository.WarehousesAsync(branch, ct)).Any(x => x.Id == input.WarehouseId)) throw new DomainRuleException("La bodega debe estar activa y pertenecer a la sucursal.");
            var channel = setup.Channels.SingleOrDefault(x => x.Id == input.ChannelId && x.Active) ?? throw new DomainRuleException("Selecciona un canal activo.");
            if (input.ExternalReference?.Length > 120 || input.Notes.Length > 1000 || input.Address.Length > 500 || input.Phone.Length > 50 || input.CustomerName.Length > 200) throw new DomainRuleException("La referencia, contacto, dirección u observación es demasiado larga.");
            if (!string.IsNullOrWhiteSpace(input.ExternalReference))
            {
                var duplicate = await repository.ExternalAsync(branch, channel.Id, input.ExternalReference.Trim().ToUpperInvariant(), ct);
                if (duplicate is not null && duplicate.Id != command.OrderId) throw new DomainRuleException($"La referencia ya corresponde al pedido {duplicate.Number}.");
            }
            var lines = await Quote(input.Items, input.Combos, branch, input.WarehouseId, setup, ct);
            var draft = JsonSerializer.Serialize(input);
            var zone = input.ServiceType == RestaurantServiceType.Delivery
                ? setup.Zones.SingleOrDefault(x => x.Id == input.ZoneId && x.Active) ?? throw new DomainRuleException("Selecciona una zona de entrega.") : null;
            Guid? turnId = null;
            if (input.ServiceType == RestaurantServiceType.DineIn)
            {
                await Branch(PermissionCodes.TablesManage, ct);
                var table = setup.Tables.SingleOrDefault(x => x.Id == input.TableId && x.Active) ?? throw new DomainRuleException("Selecciona una mesa activa.");
                var turn = (await repository.TurnsAsync(branch, ct)).SingleOrDefault(x => x.TableId == table.Id && !x.ClosedAtUtc.HasValue);
                if (turn is null) { turn = new(branch, table, clock.UtcNow); repository.Add(turn); }
                turnId = turn.Id;
            }
            RestaurantOrder order;
            if (command.OrderId.HasValue)
            {
                order = await ScopedOrder(command.OrderId.Value, branch, ct); order.CheckVersion(command.Revision);
                var oldTurnId = order.TableTurnId;
                order.UpdateDraftDetails(input.WarehouseId, channel, input.ServiceType, input.ExternalReference, input.CustomerName,
                    input.Phone, input.Address, zone?.Name ?? "", zone?.Fee ?? 0, input.Notes, turnId);
                if (oldTurnId.HasValue && oldTurnId != turnId)
                {
                    await Branch(PermissionCodes.TablesManage, ct);
                    var others = (await repository.OrdersAsync(branch, ct)).Where(x => x.Id != order.Id && x.TableTurnId == oldTurnId);
                    if (!others.Any(x => x.Status is not (RestaurantOrderStatus.Delivered or RestaurantOrderStatus.Cancelled)))
                    {
                        var oldTurn = (await repository.TurnsAsync(branch, ct)).Single(x => x.Id == oldTurnId);
                        if (!oldTurn.ClosedAtUtc.HasValue) oldTurn.Close(clock.UtcNow);
                    }
                }
                order.ReplaceDraft(draft, lines);
            }
            else
            {
                order = new(branch, input.WarehouseId, await repository.NumberAsync(ct), channel, input.ServiceType,
                    input.ExternalReference, input.CustomerName, input.Phone, input.Address, zone?.Name ?? "", zone?.Fee ?? 0, input.Notes, draft);
                if (turnId.HasValue) order.SetTable(turnId.Value);
                order.AddLines(lines); repository.Add(order);
            }
            order.Record("Borrador", user.UserName, clock.UtcNow, $"Pedido guardado; canal {order.ChannelName}; servicio {order.ServiceType}; bodega {order.WarehouseId}; turno {order.TableTurnId}; tarifa {order.DeliveryFee}; total {order.Total}."); return order.Id;
        }, x => x, Task.FromResult, ct);
    }
    public async Task AddLinesAsync(Guid id, AddOrderLines command, CancellationToken ct = default)
    {
        var branch = await Branch(PermissionCodes.OrdersUpdate, ct);
        await Run(branch, command.OperationId, new { id, command }, async () =>
        {
            var order = await ScopedOrder(id, branch, ct); order.CheckVersion(command.Revision);
            if (order.Status == RestaurantOrderStatus.Draft) throw new DomainRuleException("Edita el borrador antes de enviar.");
            var setup = (await repository.SetupAsync(branch, ct))!.Read();
            var oldIds = order.Lines.Select(x => x.Id).ToHashSet();
            order.Record("Adición", user.UserName, clock.UtcNow, "Nueva revisión enviada a cocina.");
            order.AddLines(await Quote(command.Items, command.Combos, branch, order.WarehouseId, setup, ct, order.Lines.Select(x => x.Read().ComboSequence).DefaultIfEmpty().Max()));
            await Reserve(order, order.Lines.Where(x => !oldIds.Contains(x.Id)), ct); order.QueueNewLines(clock.UtcNow); return id;
        }, x => x, Task.FromResult, ct);
    }
    public async Task ActAsync(Guid id, OrderAction command, CancellationToken ct = default)
    {
        var permission = command.Action switch
        {
            "start" or "ready" => PermissionCodes.KitchenOrdersStatus,
            "cancel" => PermissionCodes.OrdersCancel,
            "assign" or "depart" or "incident" => PermissionCodes.DeliveryManage,
            "move-table" or "close-table" => PermissionCodes.TablesManage,
            "deliver" => PermissionCodes.OrdersDeliver,
            _ => PermissionCodes.OrdersUpdate
        };
        var branch = await Branch(permission, ct);
        await Run(branch, command.OperationId, new { id, command }, async () =>
        {
            var order = await ScopedOrder(id, branch, ct); order.CheckVersion(command.Revision);
            var now = clock.UtcNow; var setup = (await repository.SetupAsync(branch, ct))!.Read();
            var line = command.LineId.HasValue ? order.Lines.SingleOrDefault(x => x.Id == command.LineId) ?? throw new DomainRuleException("Línea no encontrada.") : null;
            var previousStatus = order.Status; var previousLineStatus = line?.Status;
            string detail = command.Reason?.Trim() ?? "";
            if (detail.Length > 1000) throw new DomainRuleException("El motivo admite hasta 1000 caracteres.");
            switch (command.Action)
            {
                case "submit":
                    if (order.Status != RestaurantOrderStatus.Draft) throw new DomainRuleException("El pedido ya fue enviado.");
                    var input = JsonSerializer.Deserialize<OrderInput>(order.DraftJson)!;
                    if (!setup.Channels.Any(x => x.Id == order.ChannelId && x.Active)) throw new DomainRuleException("El canal ya no está activo.");
                    if (!(await repository.WarehousesAsync(branch, ct)).Any(x => x.Id == order.WarehouseId)) throw new DomainRuleException("La bodega ya no está activa.");
                    if (order.ServiceType == RestaurantServiceType.Delivery && !setup.Zones.Any(x => x.Id == input.ZoneId && x.Active && x.Fee == order.DeliveryFee && x.Name == order.DeliveryZoneName))
                        throw new DomainRuleException("La zona o tarifa cambió. Actualiza y revisa el borrador antes de enviar.");
                    if (order.TableTurnId.HasValue && !(await repository.TurnsAsync(branch, ct)).Any(x => x.Id == order.TableTurnId && !x.ClosedAtUtc.HasValue))
                        throw new DomainRuleException("El turno de mesa ya se cerró.");
                    var quote = await Quote(input.Items, input.Combos, branch, order.WarehouseId, setup, ct);
                    if (JsonSerializer.Serialize(quote) != JsonSerializer.Serialize(order.Lines.Select(x => x.Read()).ToList()))
                        throw new DomainRuleException("El menú, precio o receta cambió. Guarda otra vez el borrador y revisa sus importes.");
                    await Reserve(order, order.Lines, ct); order.Submit(now); break;
                case "start":
                    if (line is null) throw new DomainRuleException("Selecciona una línea.");
                    if (command.Quantity < 0 || command.Quantity > line.ActiveQuantity) throw new DomainRuleException("La cantidad a preparar excede lo pendiente.");
                    if (command.Quantity > 0 && command.Quantity < line.ActiveQuantity)
                    {
                        var parentId = line.Id; line = await SplitPreparation(order, line, command.Quantity, ct);
                        detail = $"Preparación parcial: {command.Quantity}; línea original {parentId}; nueva línea {line.Id}.";
                    }
                    line.Start(now); await Consume(order, line, ct); order.RefreshStatus(); break;
                case "ready":
                    if (line is null) throw new DomainRuleException("Selecciona una línea.");
                    line.Ready(now); order.RefreshStatus(); break;
                case "cancel":
                    if (string.IsNullOrWhiteSpace(detail)) throw new DomainRuleException("Indica el motivo de cancelación.");
                    if (order.Status is RestaurantOrderStatus.OutForDelivery or RestaurantOrderStatus.Delivered or RestaurantOrderStatus.Cancelled) throw new DomainRuleException("No puedes cancelar este pedido desde cocina.");
                    if (command.ReturnIntact && line is null) throw new DomainRuleException("Selecciona la línea del artículo intacto.");
                    if (order.Status == RestaurantOrderStatus.Draft) order.CancelDraft();
                    else
                    {
                        var targets = line is null ? order.Lines.Where(x => x.ActiveQuantity > 0).ToList() : [line];
                        foreach (var target in targets)
                        {
                            var qty = line is null ? target.ActiveQuantity : command.Quantity;
                            var before = target.ActiveQuantity;
                            var product = (await repository.ProductsAsync(ct)).Single(x => x.Id == target.ProductId);
                            if (decimal.Round(qty, Math.Min(2, product.BaseUnit!.Precision)) != qty) throw new DomainRuleException("La cancelación debe respetar la unidad del producto.");
                            if (command.ReturnIntact) await ReturnIntact(order, target, qty, detail, ct);
                            target.Cancel(qty, command.ReturnIntact);
                            if (!target.StartedAtUtc.HasValue) await Release(order, target, qty / before, ct);
                            else if (command.ReturnIntact) detail += $" | Reintegro físico: {target.Read().ProductName}, {qty}; línea {target.Id}.";
                            else
                            {
                                var consumed = (await repository.ReservationsAsync(order.WarehouseId, order.Id, ct)).Where(x => x.LineId == target.Id && x.Status == ReservationStatus.Consumed).ToList();
                                var cost = decimal.Round(consumed.Sum(x => x.Quantity * x.UnitCost) * qty / target.PreparedQuantity, 6);
                                repository.Add(new OrderWaste(order.Id, target.Id, qty, cost, command.Reason!, user.UserName, now,
                                    JsonSerializer.Serialize(consumed.Select(x => new { x.ProductId, x.LotId, x.MovementId, Quantity = x.Quantity * qty / target.PreparedQuantity, x.UnitCost }))));
                                detail += $" | Merma: {target.Read().ProductName}, {qty}; costo {cost}; línea {target.Id}.";
                            }
                        }
                        order.RefreshStatus();
                    }
                    break;
                case "assign":
                    if (command.Route?.Length > 1000) throw new DomainRuleException("La ruta admite hasta 1000 caracteres.");
                    order.AssignDriver(setup.Drivers.SingleOrDefault(x => x.Id == command.TargetId && x.Active) ?? throw new DomainRuleException("Selecciona un motorista activo."), command.Route, now);
                    detail = $"Motorista: {order.DriverName} ({order.DriverId}); ruta: {order.Route}."; break;
                case "depart": order.Depart(now); break;
                case "incident":
                    if (order.Status != RestaurantOrderStatus.OutForDelivery || string.IsNullOrWhiteSpace(detail)) throw new DomainRuleException("La incidencia requiere un delivery en ruta y un motivo."); break;
                case "deliver": order.Deliver(now); break;
                case "move-table":
                case "close-table":
                    if (!order.TableTurnId.HasValue || string.IsNullOrWhiteSpace(detail)) throw new DomainRuleException("Selecciona un turno e indica el motivo.");
                    var turns = await repository.TurnsAsync(branch, ct); var turn = turns.Single(x => x.Id == order.TableTurnId);
                    if (command.Action == "move-table")
                    {
                        var table = setup.Tables.SingleOrDefault(x => x.Id == command.TargetId && x.Active) ?? throw new DomainRuleException("Mesa no válida.");
                        if (turns.Any(x => x.TableId == table.Id && !x.ClosedAtUtc.HasValue && x.Id != turn.Id)) throw new DomainRuleException("La mesa destino está ocupada.");
                        detail = $"{turn.TableName} → {table.Area} / {table.Name}. {detail}"; turn.Move(table);
                    }
                    else
                    {
                        var related = (await repository.OrdersAsync(branch, ct)).Where(x => x.TableTurnId == turn.Id);
                        if (related.Any(x => x.Status is not (RestaurantOrderStatus.Delivered or RestaurantOrderStatus.Cancelled))) throw new DomainRuleException("El turno conserva pedidos sin entregar.");
                        turn.Close(now);
                    }
                    break;
                default: throw new DomainRuleException("Acción no válida.");
            }
            detail = $"Operación {command.OperationId}. Pedido: {previousStatus} → {order.Status}. " + (line is null ? "" : $"Línea: {previousLineStatus} → {line.Status}. ") + detail;
            order.Record(command.Action, user.UserName, now, detail, line?.Id); return id;
        }, x => x, Task.FromResult, ct);
    }
    public async Task<OrderCheckoutSnapshot> CheckoutAsync(Guid id, CancellationToken ct = default)
    {
        var branch = await Branch(PermissionCodes.SalesManage, ct); var order = await ScopedOrder(id, branch, ct);
        if (order.Status is RestaurantOrderStatus.Draft or RestaurantOrderStatus.Cancelled) throw new DomainRuleException("El pedido debe estar enviado y activo para preparar su cobro.");
        var reservations = await repository.ReservationsAsync(order.WarehouseId, order.Id, ct);
        return new(order.Id, order.Revision, order.BranchId, order.WarehouseId, order.Total, order.DeliveryFee,
            order.Lines.Where(x => x.ActiveQuantity > 0).Select(x => new OrderCheckoutLine(x.Id, x.Read(), x.ActiveQuantity,
                x.StartedAtUtc.HasValue, reservations.Where(r => r.LineId == x.Id && r.MovementId.HasValue).Select(r => r.MovementId!.Value).ToList())).ToList());
    }
}
