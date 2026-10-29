using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SistemiPos.Application.Restaurant;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Organizations;
using SistemiPos.Domain.Recipes;
using SistemiPos.Domain.Restaurant;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Restaurant;

public sealed class RestaurantRepository(AppDbContext db) : IRestaurantRepository
{
    private List<Product>? _products;
    public async Task<T> TransactionAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try { var result = await action(); await transaction.CommitAsync(ct); return result; }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException || ex is PostgresException { SqlState: "40001" or "40P01" or "23505" }
            || ex.InnerException is PostgresException { SqlState: "40001" or "40P01" or "23505" })
        { db.ChangeTracker.Clear(); throw new DomainRuleException("Otra operación modificó el pedido, mesa o inventario. Recarga y reintenta con el mismo identificador."); }
    }
    public Task<RestaurantSetup?> SetupAsync(Guid branch, CancellationToken ct) => db.RestaurantSetups.SingleOrDefaultAsync(x => x.BranchId == branch, ct);
    private IQueryable<RestaurantOrder> Orders() => db.RestaurantOrders.Include(x => x.Lines).Include(x => x.Events).AsSplitQuery();
    public Task<List<RestaurantOrder>> OrdersAsync(Guid branch, CancellationToken ct) => Orders().Where(x => x.BranchId == branch).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
    public Task<RestaurantOrder?> OrderAsync(Guid id, CancellationToken ct) => Orders().SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<List<Product>> ProductsAsync(CancellationToken ct) => _products ??= await db.Products.Include(x => x.BaseUnit).Include(x => x.Category)
        .Include(x => x.BranchSettings).Include(x => x.PriceRules).Include(x => x.VariantOptions).ThenInclude(x => x.VariantOption).ThenInclude(x => x.VariantType).AsSplitQuery().ToListAsync(ct);
    public Task<List<Warehouse>> WarehousesAsync(Guid branch, CancellationToken ct) => db.Warehouses.Where(x => x.BranchId == branch && x.IsActive).OrderBy(x => x.Name).ToListAsync(ct);
    public Task<List<TableTurn>> TurnsAsync(Guid branch, CancellationToken ct) => db.RestaurantTableTurns.Where(x => x.BranchId == branch).OrderByDescending(x => x.OpenedAtUtc).ToListAsync(ct);
    public async Task<List<OrderReservation>> ReservationsAsync(Guid warehouse, Guid? order, CancellationToken ct)
    {
        var persisted = await db.OrderReservations.Where(x => x.WarehouseId == warehouse && (!order.HasValue || x.OrderId == order)).ToListAsync(ct);
        return persisted.Concat(db.OrderReservations.Local.Where(x => x.WarehouseId == warehouse && (!order.HasValue || x.OrderId == order))).DistinctBy(x => x.Id).ToList();
    }
    public Task<InventoryStock?> StockAsync(Guid product, Guid warehouse, CancellationToken ct) => db.InventoryStocks.SingleOrDefaultAsync(x => x.ProductId == product && x.WarehouseId == warehouse, ct);
    public async Task<List<InventoryExpiryLot>> LotsAsync(Guid product, Guid warehouse, CancellationToken ct)
    {
        var lots = await db.InventoryExpiryLots.Where(x => x.ProductId == product && x.WarehouseId == warehouse).ToListAsync(ct);
        return lots.Concat(db.InventoryExpiryLots.Local.Where(x => x.ProductId == product && x.WarehouseId == warehouse)).DistinctBy(x => x.Id).ToList();
    }
    public Task<RecipeVersion?> RecipeAsync(Guid product, CancellationToken ct) => (from version in db.RecipeVersions join recipe in db.Recipes on version.RecipeId equals recipe.Id
        where recipe.ProductId == product && recipe.IsActive orderby version.Number descending select version).FirstOrDefaultAsync(ct);
    public async Task<IReadOnlyDictionary<Guid, RecipeSnapshot>> ActiveRecipeSnapshotsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct)
    {
        var ids = productIds.ToArray();
        var versions = await (from version in db.RecipeVersions
            join recipe in db.Recipes on version.RecipeId equals recipe.Id
            where recipe.IsActive && ids.Contains(recipe.ProductId)
            select new { recipe.ProductId, Version = version }).ToListAsync(ct);
        return versions.GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.Version.Number).First().Version.Read());
    }
    public Task<RestaurantCommand?> CommandAsync(Guid operation, CancellationToken ct) => db.RestaurantCommands.SingleOrDefaultAsync(x => x.OperationId == operation, ct);
    public Task<RestaurantOrder?> ExternalAsync(Guid branch, Guid channel, string reference, CancellationToken ct) => db.RestaurantOrders.SingleOrDefaultAsync(x => x.BranchId == branch && x.ChannelId == channel && x.ExternalReference == reference, ct);
    public async Task<string> NumberAsync(CancellationToken ct) => $"PED-{await db.Database.SqlQueryRaw<long>("SELECT nextval('\"RestaurantOrderNumberSequence\"') AS \"Value\"").SingleAsync(ct):D8}";
    public async Task<decimal> TaxAsync(CancellationToken ct) => await db.SystemSettings.Select(x => x.TaxPercent).FirstOrDefaultAsync(ct);
    public void Add(object entity) => db.Add(entity);
    public async Task SaveAsync(CancellationToken ct)
    {
        // New child GUIDs are assigned in the domain. Mark them explicitly when added to an existing aggregate.
        foreach (var entry in db.ChangeTracker.Entries<RestaurantOrder>().ToList())
        {
            var ids = await db.RestaurantOrderLines.Where(x => x.OrderId == entry.Entity.Id).Select(x => x.Id).ToListAsync(ct);
            foreach (var line in entry.Entity.Lines.Where(x => !ids.Contains(x.Id))) db.Entry(line).State = EntityState.Added;
            var eventIds = await db.RestaurantEvents.Where(x => x.OrderId == entry.Entity.Id).Select(x => x.Id).ToListAsync(ct);
            foreach (var ev in entry.Entity.Events.Where(x => !eventIds.Contains(x.Id))) db.Entry(ev).State = EntityState.Added;
        }
        await db.SaveChangesAsync(ct);
    }
}
