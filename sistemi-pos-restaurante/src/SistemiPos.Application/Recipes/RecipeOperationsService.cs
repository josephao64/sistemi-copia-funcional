using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Organizations;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Organizations;
using SistemiPos.Domain.Recipes;

namespace SistemiPos.Application.Recipes;

public sealed class RecipeOperationsService(IRecipeOperationsRepository repository, IRecipeRepository recipes,
    IOrganizationRepository organizations, IOperationContextAccessor context, IOperationAuthorizationService authorization,
    ICurrentUserAccessor user, IDateTimeProvider clock, IUnitOfWork work) : IRecipeOperationsService
{
    private async Task Authorize(string code, CancellationToken ct)
    {
        if (!user.UserId.HasValue || !await authorization.HasPermissionAsync(user.UserName, code, ct))
            throw new DomainRuleException("No tienes permiso para esta operación.");
    }
    public async Task<IReadOnlyList<Warehouse>> WarehousesAsync(CancellationToken ct = default)
    {
        var branches = (await context.GetAuthorizedBranchesAsync(ct)).Where(x => x.IsActive).Select(x => x.Id).ToHashSet();
        return (await organizations.GetWarehousesAsync(onlyActive: true, cancellationToken: ct))
            .Where(x => branches.Contains(x.BranchId)).OrderBy(x => x.Name).ToList();
    }
    private async Task<Warehouse> Warehouse(Guid id, CancellationToken ct) =>
        (await WarehousesAsync(ct)).FirstOrDefault(x => x.Id == id)
        ?? throw new DomainRuleException("La bodega está inactiva o fuera de tus sucursales autorizadas.");
    public async Task<IReadOnlyList<RecipeVersion>> VersionsAsync(Guid recipeId, CancellationToken ct = default)
    {
        await Authorize(PermissionCodes.RecipesView, ct); return await repository.VersionsAsync(recipeId, ct);
    }
    public async Task<IReadOnlyList<ProductionOrder>> OrdersAsync(CancellationToken ct = default)
    {
        await Authorize(PermissionCodes.ProductionView, ct);
        var warehouses = await WarehousesAsync(ct); var allowed = warehouses.Select(x => x.Id).ToHashSet();
        var origins = warehouses.Where(x => !context.CurrentBranchId.HasValue || x.BranchId == context.CurrentBranchId)
            .Select(x => x.Id).ToHashSet();
        return (await repository.OrdersAsync(ct)).Where(x => origins.Contains(x.OriginWarehouseId) && allowed.Contains(x.DestinationWarehouseId)).ToList();
    }
    public async Task<ProductionOrder> OrderAsync(Guid id, CancellationToken ct = default)
    {
        await Authorize(PermissionCodes.ProductionView, ct);
        var order = await repository.OrderAsync(id, ct) ?? throw new DomainRuleException("Producción no encontrada.");
        await Warehouse(order.OriginWarehouseId, ct); await Warehouse(order.DestinationWarehouseId, ct); return order;
    }
    public async Task<RecipeQuote> QuoteAsync(Guid versionId, Guid warehouseId, decimal quantity, DateTimeOffset? at = null, CancellationToken ct = default)
    {
        await Authorize(PermissionCodes.RecipesView, ct); await Warehouse(warehouseId, ct);
        var version = await repository.VersionAsync(versionId, ct) ?? throw new DomainRuleException("Versión no encontrada.");
        return await Quote(version, warehouseId, quantity, at ?? clock.UtcNow, ct);
    }
    private async Task<RecipeQuote> Quote(RecipeVersion version, Guid warehouseId, decimal quantity, DateTimeOffset at, CancellationToken ct)
    {
        if (at > clock.UtcNow) throw new DomainRuleException("La fecha de costo no puede estar en el futuro.");
        if (version.EffectiveAtUtc > at) throw new DomainRuleException("La versión todavía no existía en la fecha indicada.");
        var snapshot = version.Read(); RecipeRules.ValidateQuantity(quantity, "El rendimiento");
        if (decimal.Round(quantity, snapshot.YieldPrecision) != quantity) throw new DomainRuleException("La cantidad no respeta la precisión del producto elaborado.");
        var lines = new List<RecipeAvailabilityLine>();
        foreach (var component in snapshot.Components)
        {
            var required = RecipeQuantities.Scale(component.BaseQuantity, quantity, snapshot.YieldQuantity, component.Precision);
            var cost = await ComponentCost(component, at, new HashSet<Guid> { version.Id }, ct);
            lines.Add(new(component, await repository.AvailableAsync(component.ProductId, warehouseId, clock.UtcNow, ct), required, cost));
        }
        var limiting = lines.OrderBy(x => x.Available * snapshot.YieldQuantity / x.Component.BaseQuantity).First();
        var factor = (decimal)Math.Pow(10, snapshot.YieldPrecision);
        var available = decimal.Floor(limiting.Available * snapshot.YieldQuantity / limiting.Component.BaseQuantity * factor) / factor;
        var total = decimal.Round(lines.Sum(x => x.Required * x.Cost.UnitCost), 6);
        return new(version, warehouseId, at, quantity, total, decimal.Round(total / quantity, 6), available, limiting.Component.Name, lines);
    }
    private async Task<RecipeCost> ComponentCost(RecipeComponentSnapshot component, DateTimeOffset at, HashSet<Guid> visited, CancellationToken ct)
    {
        var recorded = await repository.CostAsync(component.ProductId, at, ct);
        if (recorded is not null && (!component.SubrecipeVersionId.HasValue || recorded.Source != "Referencia al iniciar producción")) return recorded;
        if (component.SubrecipeVersionId is Guid id)
        {
            if (!visited.Add(id)) throw new DomainRuleException("Referencia circular en el histórico de recetas.");
            var version = await repository.VersionAsync(id, ct) ?? throw new DomainRuleException("Versión de subreceta no encontrada.");
            var snapshot = version.Read(); decimal total = 0; var known = true;
            foreach (var child in snapshot.Components)
            {
                var cost = await ComponentCost(child, at, visited, ct); total += child.BaseQuantity * cost.UnitCost; known &= cost.Known;
            }
            visited.Remove(id);
            return new(decimal.Round(total / snapshot.YieldQuantity, 6), $"Teórico de subreceta v{version.Number}", known);
        }
        return new(0, "Sin costo registrado a esa fecha", false);
    }
    private async Task ValidateProduct(Guid id, Guid unitId, Guid warehouseId, CancellationToken ct)
    {
        var product = await repository.ProductAsync(id, ct) ?? throw new DomainRuleException("Producto no encontrado.");
        var warehouse = await Warehouse(warehouseId, ct);
        if (!product.IsActive || product.ManagesVariants || product.BaseUnit is not { IsActive: true } || product.BaseUnitId != unitId
            || product.BranchSettings.Any(x => x.BranchId == warehouse.BranchId && !x.IsEnabled))
            throw new DomainRuleException($"{product.DisplayName}: revisa estado, unidad base y disponibilidad en la sucursal.");
    }
    public async Task<Guid> CreateAsync(ProductionRequest request, CancellationToken ct = default)
    {
        await Authorize(PermissionCodes.ProductionManage, ct);
        await Warehouse(request.OriginWarehouseId, ct); await Warehouse(request.DestinationWarehouseId, ct);
        return await recipes.InTransactionAsync(async () =>
        {
            var existing = await repository.OperationAsync(request.OperationId, ct);
            if (existing is not null)
            {
                if (existing.RecipeVersion.RecipeId != request.RecipeId || existing.OriginWarehouseId != request.OriginWarehouseId
                    || existing.DestinationWarehouseId != request.DestinationWarehouseId || existing.PlannedQuantity != request.PlannedQuantity
                    || existing.LotNumber != request.LotNumber.Trim() || existing.ExpiryDate != request.ExpiryDate
                    || existing.ScheduledAtUtc != request.ScheduledAtUtc || existing.ResponsibleUser != user.UserName)
                    throw new DomainRuleException("La operación ya fue utilizada con otros datos.");
                return existing.Id;
            }
            var recipe = await recipes.GetRecipeAsync(request.RecipeId, ct);
            if (recipe is not { IsActive: true, ConsumptionMode: RecipeConsumptionMode.FromProduction })
                throw new DomainRuleException("Selecciona una receta activa configurada para producir existencias.");
            var version = (await repository.VersionsAsync(recipe.Id, ct)).First();
            var snapshot = version.Read();
            await ValidateProduct(snapshot.ProductId, snapshot.YieldUnitId, request.DestinationWarehouseId, ct);
            RecipeRules.ValidateQuantity(request.PlannedQuantity, "La cantidad planificada");
            if (decimal.Round(request.PlannedQuantity, snapshot.YieldPrecision) != request.PlannedQuantity)
                throw new DomainRuleException("La cantidad planificada no respeta la unidad del producto.");
            var output = await repository.ProductAsync(snapshot.ProductId, ct);
            if (output!.ManagesExpiry && !request.ExpiryDate.HasValue) throw new DomainRuleException("El producto requiere vencimiento del lote.");
            var order = new ProductionOrder(request.OperationId, version.Id, request.OriginWarehouseId,
                request.DestinationWarehouseId, request.PlannedQuantity, request.LotNumber, request.ExpiryDate, request.ScheduledAtUtc, user.UserName);
            await repository.AddOrderAsync(order, ct); await work.SaveChangesAsync(ct); return order.Id;
        }, ct);
    }
    public async Task ConfirmAsync(Guid id, uint version, decimal actualQuantity, string? reason, CancellationToken ct = default)
    {
        await Authorize(PermissionCodes.ProductionConfirm, ct);
        await recipes.InTransactionAsync(async () =>
        {
            var order = await repository.OrderAsync(id, ct) ?? throw new DomainRuleException("Producción no encontrada.");
            await Warehouse(order.OriginWarehouseId, ct); await Warehouse(order.DestinationWarehouseId, ct);
            if (order.Status == ProductionStatus.Confirmed)
            {
                if (order.ActualQuantity != actualQuantity || order.VarianceReason != reason?.Trim())
                    throw new DomainRuleException("La producción ya se confirmó con otro rendimiento.");
                return true;
            }
            if (order.Version != version) throw new DomainRuleException("La producción cambió. Recarga antes de confirmar.");
            var snapshot = order.RecipeVersion.Read(); var now = clock.UtcNow;
            await ValidateProduct(snapshot.ProductId, snapshot.YieldUnitId, order.DestinationWarehouseId, ct);
            var output = await repository.ProductAsync(snapshot.ProductId, ct);
            if (output!.ManagesExpiry && (!order.ExpiryDate.HasValue || order.ExpiryDate < DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(-6)).Date)))
                throw new DomainRuleException("El lote requiere un vencimiento vigente.");
            var quote = await Quote(order.RecipeVersion, order.OriginWarehouseId, order.PlannedQuantity, now, ct);
            var consumption = quote.Lines.Select(x => new RecipeConsumption(x.Component.ProductId, x.Component.Name,
                x.Component.BaseUnitCode, x.Required, x.Cost.UnitCost, x.Cost.Source)).ToList();
            order.Confirm(actualQuantity, reason, consumption, now, user.UserName, snapshot.YieldPrecision);
            foreach (var line in quote.Lines)
            {
                await ValidateProduct(line.Component.ProductId, line.Component.BaseUnitId, order.OriginWarehouseId, ct);
                if (line.Required > line.Available) throw new DomainRuleException($"Existencia insuficiente de {line.Component.Name}.");
                await Move(line.Component.ProductId, order.OriginWarehouseId, -line.Required, order.Number,
                    InventoryMovementType.ProductionConsumption, line.Cost.UnitCost, now, null, null, ct);
            }
            if (actualQuantity > 0) await Move(snapshot.ProductId, order.DestinationWarehouseId, actualQuantity, order.Number,
                InventoryMovementType.ProductionEntry, order.UnitCost, now, order.LotNumber, order.ExpiryDate, ct);
            await work.SaveChangesAsync(ct); return true;
        }, ct);
    }
    private async Task Move(Guid product, Guid warehouse, decimal delta, string document, InventoryMovementType type,
        decimal? cost, DateTimeOffset now, string? lot, DateOnly? expiry, CancellationToken ct)
    {
        var stock = await repository.StockAsync(product, warehouse, ct); var before = stock.CurrentStock;
        stock.ReplaceForSystem(before + delta);
        var movement = new InventoryMovement(product, type, Math.Abs(delta), before, stock.CurrentStock, now,
            user.UserName, document, "Producción de receta", warehouseId: warehouse, unitCost: cost);
        movement.SetLotDetails(expiry, lot); await repository.AddMovementAsync(movement, ct);
    }
    public async Task<IReadOnlyList<SistemiPos.Application.Catalog.ProductDto>> SaleCatalogAsync(
        IReadOnlyList<SistemiPos.Application.Catalog.ProductDto> products, Guid? branchId, CancellationToken ct = default)
    {
        await Authorize(PermissionCodes.SalesManage, ct);
        var warehouses = await WarehousesAsync(ct);
        var branch = branchId ?? context.CurrentBranchId;
        var warehouse = warehouses.Where(x => x.BranchId == branch).OrderByDescending(x => x.IsDefault).FirstOrDefault();
        var result = new List<SistemiPos.Application.Catalog.ProductDto>();
        foreach (var product in products.Where(x => x.IsActive && !x.ManagesVariants))
        {
            decimal available = 0;
            if (warehouse is not null) try
            {
                var version = await repository.ActiveVersionAsync(product.Id, clock.UtcNow, ct);
                if (version is not null && version.Read().ConsumptionMode == RecipeConsumptionMode.OnSale)
                {
                    var snapshot = version.Read();
                    var capacities = new List<decimal>();
                    foreach (var component in snapshot.Components)
                    {
                        await ValidateProduct(component.ProductId, component.BaseUnitId, warehouse.Id, ct);
                        var stock = await repository.AvailableAsync(component.ProductId, warehouse.Id, clock.UtcNow, ct);
                        capacities.Add(stock * snapshot.YieldQuantity / component.BaseQuantity);
                    }
                    var factor = (decimal)Math.Pow(10, snapshot.YieldPrecision);
                    available = decimal.Floor(capacities.Min() * factor) / factor;
                }
                else available = await repository.AvailableAsync(product.Id, warehouse.Id, clock.UtcNow, ct);
            }
            catch (DomainRuleException) { available = 0; }
            result.Add(product with { CurrentStock = available });
        }
        return result;
    }
    public async Task<SaleRecipePlan?> PlanSaleAsync(Guid productId, decimal quantity, Guid warehouseId, DateTimeOffset at, CancellationToken ct = default)
    {
        await Authorize(PermissionCodes.SalesManage, ct); await Warehouse(warehouseId, ct);
        var version = await repository.ActiveVersionAsync(productId, at, ct);
        if (version is null) return null;
        var snapshot = version.Read();
        await ValidateProduct(productId, snapshot.YieldUnitId, warehouseId, ct);
        if (snapshot.ConsumptionMode == RecipeConsumptionMode.FromProduction)
        {
            var cost = await repository.CostAsync(productId, at, ct) ?? new RecipeCost(0, "Sin costo registrado", false);
            return new(version.Id, version.Number, cost.UnitCost, [new(productId, snapshot.ProductName, snapshot.YieldUnitCode, quantity, cost.UnitCost, cost.Source)]);
        }
        var quote = await Quote(version, warehouseId, quantity, at, ct);
        foreach (var line in quote.Lines) await ValidateProduct(line.Component.ProductId, line.Component.BaseUnitId, warehouseId, ct);
        return new(version.Id, version.Number, quote.UnitCost, quote.Lines.Select(x => new RecipeConsumption(x.Component.ProductId,
            x.Component.Name, x.Component.BaseUnitCode, x.Required, x.Cost.UnitCost, x.Cost.Source)).ToList());
    }
}
