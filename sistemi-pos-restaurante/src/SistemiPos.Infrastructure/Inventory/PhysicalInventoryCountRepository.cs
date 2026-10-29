using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Inventory;
using SistemiPos.Domain.Inventory;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Inventory;

public sealed class PhysicalInventoryCountRepository(AppDbContext dbContext) : IPhysicalInventoryCountRepository
{
    public Task<List<PhysicalInventoryCount>> GetCountsAsync(CancellationToken cancellationToken = default) =>
        CountQuery(false).ToListAsync(cancellationToken);

    public Task<PhysicalInventoryCount?> GetCountAsync(Guid id, bool tracking, CancellationToken cancellationToken = default) =>
        CountQuery(tracking).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<PhysicalInventoryCountItem?> GetItemAsync(Guid countId, Guid itemId, CancellationToken cancellationToken = default) =>
        dbContext.PhysicalInventoryCountItems
            .Include(x => x.PhysicalInventoryCount)
            .Include(x => x.Product)
            .ThenInclude(x => x.Category)
            .SingleOrDefaultAsync(x => x.PhysicalInventoryCountId == countId && x.Id == itemId, cancellationToken);

    public Task<List<PhysicalInventoryCategoryOption>> GetCategoryOptionsAsync(CancellationToken cancellationToken = default) =>
        dbContext.Categories
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new PhysicalInventoryCategoryOption(x.Id, x.Name))
            .ToListAsync(cancellationToken);

    public Task<List<Guid>> GetEligibleProductIdsAsync(
        PhysicalInventoryCountScope scope,
        IReadOnlyCollection<Guid> categoryIds,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Products.AsNoTracking().Where(x => x.IsActive && !x.ManagesVariants);
        if (scope == PhysicalInventoryCountScope.Categories)
        {
            query = query.Where(x => categoryIds.Contains(x.CategoryId));
        }

        return query.Select(x => x.Id).ToListAsync(cancellationToken);
    }

    public Task<bool> HasActiveParticipationAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default) =>
        dbContext.PhysicalInventoryCountItems
            .AsNoTracking()
            .AnyAsync(x => x.IsActiveParticipation && productIds.Contains(x.ProductId), cancellationToken);

    public Task<List<InventoryStock>> GetStocksForUpdateAsync(
        IReadOnlyCollection<Guid> productIds,
        Guid? warehouseId = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.InventoryStocks.Where(x => productIds.Contains(x.ProductId));
        if (warehouseId.HasValue)
        {
            query = query.Where(x => x.WarehouseId == warehouseId.Value);
        }
        return query.ToListAsync(cancellationToken);
    }

    public Task AddAsync(PhysicalInventoryCount count, CancellationToken cancellationToken = default) =>
        dbContext.PhysicalInventoryCounts.AddAsync(count, cancellationToken).AsTask();

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await operation(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private IQueryable<PhysicalInventoryCount> CountQuery(bool tracking)
    {
        IQueryable<PhysicalInventoryCount> query = dbContext.PhysicalInventoryCounts
            .Include(x => x.Categories)
            .ThenInclude(x => x.Category)
            .Include(x => x.Items)
            .ThenInclude(x => x.Product)
            .ThenInclude(x => x.Category)
            .AsSplitQuery();
        return tracking ? query : query.AsNoTracking();
    }
}
