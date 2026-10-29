using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Recipes;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Recipes;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Recipes;

public sealed class RecipeOperationsRepository(AppDbContext db) : IRecipeOperationsRepository
{
    public Task<List<RecipeVersion>> VersionsAsync(Guid recipeId, CancellationToken ct) =>
        db.RecipeVersions.AsNoTracking().Where(x => x.RecipeId == recipeId).OrderByDescending(x => x.Number).ToListAsync(ct);
    public Task<RecipeVersion?> VersionAsync(Guid id, CancellationToken ct) => db.RecipeVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<RecipeVersion?> ActiveVersionAsync(Guid productId, DateTimeOffset at, CancellationToken ct)
    {
        var recipe = await db.Recipes.AsNoTracking().SingleOrDefaultAsync(x => x.ProductId == productId, ct);
        if (recipe is null) return null;
        if (!recipe.IsActive) throw new DomainRuleException("La receta del producto está inactiva.");
        return await db.RecipeVersions.AsNoTracking().Where(x => x.RecipeId == recipe.Id && x.EffectiveAtUtc <= at)
            .OrderByDescending(x => x.Number).FirstOrDefaultAsync(ct)
            ?? throw new DomainRuleException("No existe una versión de receta vigente para la fecha de la operación.");
    }
    public Task<List<ProductionOrder>> OrdersAsync(CancellationToken ct) => db.ProductionOrders.Include(x => x.RecipeVersion).AsNoTracking()
        .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
    public Task<ProductionOrder?> OrderAsync(Guid id, CancellationToken ct) => db.ProductionOrders.Include(x => x.RecipeVersion).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<ProductionOrder?> OperationAsync(Guid id, CancellationToken ct) => db.ProductionOrders.Include(x => x.RecipeVersion).SingleOrDefaultAsync(x => x.OperationId == id, ct);
    public Task AddOrderAsync(ProductionOrder order, CancellationToken ct) => db.ProductionOrders.AddAsync(order, ct).AsTask();
    public Task<Product?> ProductAsync(Guid id, CancellationToken ct) => db.Products.Include(x => x.BaseUnit).Include(x => x.BranchSettings).SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<InventoryStock> StockAsync(Guid productId, Guid warehouseId, CancellationToken ct)
    {
        var stock = db.InventoryStocks.Local.FirstOrDefault(x => x.ProductId == productId && x.WarehouseId == warehouseId)
            ?? await db.InventoryStocks.SingleOrDefaultAsync(x => x.ProductId == productId && x.WarehouseId == warehouseId, ct);
        if (stock is not null) return stock;
        stock = new(productId, warehouseId); db.InventoryStocks.Add(stock); return stock;
    }
    public async Task<decimal> AvailableAsync(Guid productId, Guid warehouseId, DateTimeOffset now, CancellationToken ct)
    {
        var stock = await db.InventoryStocks.AsNoTracking().Where(x => x.ProductId == productId && x.WarehouseId == warehouseId)
            .Select(x => (decimal?)x.CurrentStock).SingleOrDefaultAsync(ct) ?? 0;
        var product = await db.Products.AsNoTracking().SingleAsync(x => x.Id == productId, ct);
        if (!product.ManagesExpiry) return stock;
        var today = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(-6)).Date);
        var lots = await db.InventoryExpiryLots.Where(x => x.ProductId == productId && x.WarehouseId == warehouseId
            && x.ExpiryDate >= today).SumAsync(x => x.AvailableQuantity, ct);
        return Math.Min(stock, lots);
    }
    public async Task<RecipeCost?> CostAsync(Guid productId, DateTimeOffset at, CancellationToken ct)
    {
        var candidates = new List<(DateTimeOffset At, RecipeCost Cost)>();
        var baseline = await db.ProductCostBaselines.AsNoTracking().SingleOrDefaultAsync(x => x.ProductId == productId && x.EffectiveAtUtc <= at, ct);
        if (baseline is not null) candidates.Add((baseline.EffectiveAtUtc, new(baseline.UnitCost, "Referencia al iniciar producción")));
        var purchase = await (from item in db.PurchaseItems.AsNoTracking()
            join header in db.Purchases.AsNoTracking() on item.PurchaseId equals header.Id
            where item.ProductId == productId && header.ConfirmedAtUtc != null && header.ConfirmedAtUtc <= at
                && (header.CancelledAtUtc == null || header.CancelledAtUtc > at)
            orderby header.ConfirmedAtUtc descending, header.Id, item.Id
            select new { item.UnitCost, At = header.ConfirmedAtUtc!.Value, header.Number }).FirstOrDefaultAsync(ct);
        if (purchase is not null) candidates.Add((purchase.At, new(purchase.UnitCost, purchase.Number)));
        var production = await (from order in db.ProductionOrders.AsNoTracking()
            join version in db.RecipeVersions on order.RecipeVersionId equals version.Id
            join recipe in db.Recipes on version.RecipeId equals recipe.Id
            where recipe.ProductId == productId && order.Status == ProductionStatus.Confirmed
                && order.ConfirmedAtUtc <= at && order.UnitCost != null
            orderby order.ConfirmedAtUtc descending, order.Id
            select new { Cost = order.UnitCost!.Value, At = order.ConfirmedAtUtc!.Value, order.Number }).FirstOrDefaultAsync(ct);
        if (production is not null) candidates.Add((production.At, new(production.Cost, production.Number)));
        return candidates.Count == 0 ? null : candidates.OrderByDescending(x => x.At).First().Cost;
    }
    public Task AddMovementAsync(InventoryMovement movement, CancellationToken ct) => db.InventoryMovements.AddAsync(movement, ct).AsTask();
}
