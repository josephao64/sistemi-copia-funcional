using SistemiPos.Domain.Inventory;

namespace SistemiPos.Application.Inventory;

public interface IInventoryTransferService
{
    Task<IReadOnlyList<InventoryTransferDto>> GetTransfersAsync(
        Guid? originWarehouseId = null,
        Guid? destinationWarehouseId = null,
        InventoryTransferStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<InventoryTransferDto?> GetTransferByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<InventoryTransferDto> CreateTransferAsync(CreateInventoryTransferRequest request, CancellationToken cancellationToken = default);

    Task<InventoryTransferDto> DispatchTransferAsync(Guid transferId, CancellationToken cancellationToken = default);

    Task<InventoryTransferDto> ReceiveTransferAsync(Guid transferId, ReceiveInventoryTransferRequest? request = null, CancellationToken cancellationToken = default);

    Task<InventoryTransferDto> CancelTransferAsync(Guid transferId, CancelInventoryTransferRequest? request = null, CancellationToken cancellationToken = default);
}
