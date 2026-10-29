using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SistemiPos.Application.Orders;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Orders;
using SistemiPos.Domain.Organizations;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Orders;

public sealed class OrderRepository(AppDbContext db) : IOrderRepository
{
    public async Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var result = await action();
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (Exception ex) when (IsConflict(ex))
        {
            db.ChangeTracker.Clear();
            throw new DomainRuleException("Otra operación modificó estos datos o utilizó el identificador. Recarga y reintenta con el mismo identificador de operación.");
        }
        catch
        {
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private static bool IsConflict(Exception ex) => ex is DbUpdateConcurrencyException
        || ex is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.UniqueViolation }
        || ex.InnerException is not null && IsConflict(ex.InnerException);

    public Task<List<PosTerminal>> GetTerminalsAsync(Guid branchId, CancellationToken ct) =>
        db.Set<PosTerminal>().AsNoTracking().Where(x => x.BranchId == branchId && x.IsActive).OrderBy(x => x.Code).ToListAsync(ct);
    public Task<PosTerminal?> GetTerminalAsync(Guid id, CancellationToken ct) => db.Set<PosTerminal>().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<Warehouse?> GetWarehouseAsync(Guid id, CancellationToken ct) => db.Warehouses.SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task AddTerminalAsync(PosTerminal terminal, CancellationToken ct) => await db.Set<PosTerminal>().AddAsync(terminal, ct);
    public Task<bool> TerminalCodeExistsAsync(Guid branchId, string code, Guid? excludingId, CancellationToken ct) =>
        db.Set<PosTerminal>().AnyAsync(x => x.BranchId == branchId && x.Code == code && (!excludingId.HasValue || x.Id != excludingId.Value), ct);
    public async Task<bool> HasTerminalOperationsAsync(Guid terminalId, Guid branchId, string currentRegister, string targetRegister, CancellationToken ct) =>
        await db.CashSessions.AnyAsync(x => x.BranchId == branchId && x.Status == CashSessionStatus.Active
            && (x.TerminalId == terminalId || x.RegisterCode.ToUpper() == currentRegister.ToUpper() || x.RegisterCode.ToUpper() == targetRegister.ToUpper()), ct)
        || await db.Set<RestaurantOrder>().AnyAsync(x => x.TerminalId == terminalId && x.BranchId == branchId
            && x.Lines.Any(line => line.Quantity > line.CancelledQuantity + line.SoldQuantity), ct);
    public Task<List<RestaurantOrder>> GetOrdersAsync(Guid branchId, CancellationToken ct) =>
        db.Set<RestaurantOrder>().Include(x => x.Lines).Include(x => x.Events).AsSplitQuery().AsNoTracking()
            .Where(x => x.BranchId == branchId).OrderByDescending(x => x.CreatedAtUtc).Take(200).ToListAsync(ct);
    public Task<RestaurantOrder?> GetOrderAsync(Guid id, CancellationToken ct) =>
        db.Set<RestaurantOrder>().Include(x => x.Lines).Include(x => x.Events).AsSplitQuery().SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task AddOrderAsync(RestaurantOrder order, CancellationToken ct) => await db.Set<RestaurantOrder>().AddAsync(order, ct);
    public async Task TrackNewDetailsAsync(RestaurantOrder order, CancellationToken ct)
    {
        var lineIds = await db.Set<RestaurantOrderLine>().Where(x => x.OrderId == order.Id).Select(x => x.Id).ToListAsync(ct);
        foreach (var line in order.Lines.Where(x => !lineIds.Contains(x.Id))) db.Entry(line).State = EntityState.Added;
        var eventIds = await db.Set<RestaurantOrderEvent>().Where(x => x.OrderId == order.Id).Select(x => x.Id).ToListAsync(ct);
        foreach (var item in order.Events.Where(x => !eventIds.Contains(x.Id))) db.Entry(item).State = EntityState.Added;
    }
    public Task<Product?> GetProductAsync(Guid id, CancellationToken ct) =>
        db.Products.Include(x => x.BranchSettings).Include(x => x.PriceRules)
            .Include(x => x.VariantOptions).ThenInclude(x => x.VariantOption).ThenInclude(x => x.VariantType)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<InventoryStock?> GetStockAsync(Guid productId, Guid warehouseId, CancellationToken ct) =>
        db.InventoryStocks.SingleOrDefaultAsync(x => x.ProductId == productId && x.WarehouseId == warehouseId, ct);
    public Task<Dictionary<Guid, decimal>> GetStocksAsync(Guid warehouseId, CancellationToken ct) =>
        db.InventoryStocks.AsNoTracking().Where(x => x.WarehouseId == warehouseId).ToDictionaryAsync(x => x.ProductId, x => x.CurrentStock, ct);
    public async Task AddMovementAsync(InventoryMovement movement, CancellationToken ct) => await db.InventoryMovements.AddAsync(movement, ct);
}
