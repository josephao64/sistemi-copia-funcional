namespace SistemiPos.Application.Inventory;

public interface IPhysicalInventoryCountService
{
    Task<IReadOnlyList<PhysicalInventoryCategoryOption>> GetCategoryOptionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PhysicalInventoryCountListItemDto>> GetHistoryAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreatePhysicalInventoryCountRequest request, CancellationToken cancellationToken = default);
    Task<PhysicalInventoryCountDetailDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BeginPhysicalInventoryCountItemResult> BeginItemAsync(Guid countId, Guid itemId, CancellationToken cancellationToken = default);
    Task<SavePhysicalInventoryCountItemResult> SaveItemAsync(Guid countId, Guid itemId, SavePhysicalInventoryCountItemRequest request, CancellationToken cancellationToken = default);
    Task FinalizeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PhysicalInventoryCountExportDto> GetExportAsync(Guid id, CancellationToken cancellationToken = default);
}
