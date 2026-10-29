using SistemiPos.Domain.Inventory;

namespace SistemiPos.Application.Inventory;

public interface IInventoryTransferRepository
{
    Task<List<InventoryTransfer>> GetTransfersAsync(
        Guid? originWarehouseId = null,
        Guid? destinationWarehouseId = null,
        InventoryTransferStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<InventoryTransfer?> GetTransferByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<InventoryTransfer?> GetTransferByNumberAsync(string number, CancellationToken cancellationToken = default);

    Task AddTransferAsync(InventoryTransfer transfer, CancellationToken cancellationToken = default);

    Task<string> GenerateTransferNumberAsync(CancellationToken cancellationToken = default);

    Task<InventoryStock?> GetStockAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default);

    Task AddStockAsync(InventoryStock stock, CancellationToken cancellationToken = default);

    Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default);
}
