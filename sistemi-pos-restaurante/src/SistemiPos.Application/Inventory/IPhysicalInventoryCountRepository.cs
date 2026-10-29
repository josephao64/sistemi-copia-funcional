using SistemiPos.Domain.Inventory;

namespace SistemiPos.Application.Inventory;

public interface IPhysicalInventoryCountRepository
{
    Task<List<PhysicalInventoryCount>> GetCountsAsync(CancellationToken cancellationToken = default);
    Task<PhysicalInventoryCount?> GetCountAsync(Guid id, bool tracking, CancellationToken cancellationToken = default);
    Task<PhysicalInventoryCountItem?> GetItemAsync(Guid countId, Guid itemId, CancellationToken cancellationToken = default);
    Task<List<PhysicalInventoryCategoryOption>> GetCategoryOptionsAsync(CancellationToken cancellationToken = default);
    Task<List<Guid>> GetEligibleProductIdsAsync(PhysicalInventoryCountScope scope, IReadOnlyCollection<Guid> categoryIds, CancellationToken cancellationToken = default);
    Task<bool> HasActiveParticipationAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default);
    Task<List<InventoryStock>> GetStocksForUpdateAsync(IReadOnlyCollection<Guid> productIds, Guid? warehouseId = null, CancellationToken cancellationToken = default);
    Task AddAsync(PhysicalInventoryCount count, CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
}
