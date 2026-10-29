namespace SistemiPos.Application.Inventory;

public interface IInventoryQueryService
{
    Task<IReadOnlyList<InventoryStockDto>> GetCurrentStockAsync(bool onlyLowStock, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryStockDto>> GetCurrentStockAsync(bool onlyLowStock, Guid? branchId, Guid? warehouseId, CancellationToken cancellationToken = default);
    Task<InventoryMovementPage> GetKardexAsync(Guid? productId, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<InventoryMovementPage> GetKardexAsync(Guid? productId, int page, int pageSize, Guid? warehouseId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryMovementDto>> GetKardexExportAsync(Guid? productId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryMovementDto>> GetKardexExportAsync(Guid? productId, Guid? warehouseId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryExpiryLotDto>> GetExpiryLotsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryExpiryLotDto>> GetExpiryLotsAsync(Guid? warehouseId, CancellationToken cancellationToken = default);
}
