using SistemiPos.Domain.Inventory;

namespace SistemiPos.Application.Inventory;

public interface IInventoryRepository
{
    Task<InventoryMovementPage> GetMovementPageAsync(Guid? productId, int page, int pageSize, Guid? warehouseId = null, CancellationToken cancellationToken = default);
    Task<List<InventoryMovementDto>> GetMovementsForExportAsync(Guid? productId, Guid? warehouseId = null, CancellationToken cancellationToken = default);
    Task<List<InventoryStockDto>> GetStocksAsync(bool onlyLowStock, CancellationToken cancellationToken = default);
    Task<List<InventoryStockDto>> GetStocksAsync(bool onlyLowStock, Guid? branchId, Guid? warehouseId, CancellationToken cancellationToken = default);
    Task<InventoryStock?> GetStockAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<InventoryStock?> GetStockAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default);
    Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default);
    Task AddExpiryLotAsync(InventoryExpiryLot lot, CancellationToken cancellationToken = default);
    Task<List<InventoryExpiryLot>> GetExpiryLotsAsync(Guid? productId, Guid? warehouseId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, string>> GetWarehouseNamesAsync(IReadOnlyCollection<Guid> warehouseIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
}
