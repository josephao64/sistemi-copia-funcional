namespace SistemiPos.Application.Inventory;

public sealed record InventoryLotRow(Guid Id, string Number, DateTimeOffset? ReceivedAtUtc,
    DateOnly? ExpiryDate, decimal? ReceivedQuantity, decimal AvailableQuantity, bool IsOpeningBalance);
public sealed record InventoryLotTraceRow(DateTimeOffset Date, string Type, string? Document,
    string LotNumber, decimal Quantity, string UserName);
public sealed record ProductLotsDto(Guid ProductId, Guid WarehouseId, string ProductName, string Unit,
    string WarehouseName, bool ManagesExpiry, decimal Stock, decimal Unassigned,
    IReadOnlyList<InventoryLotRow> Lots, IReadOnlyList<InventoryLotTraceRow> Movements,
    bool ManagesLots = true);

public interface IInventoryLotService
{
    Task<ProductLotsDto> GetAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default);
    Task AddOpeningAsync(Guid productId, Guid warehouseId, decimal quantity, DateOnly? expiryDate,
        string? lotNumber, CancellationToken cancellationToken = default);
    Task DiscardAsync(Guid productId, Guid warehouseId, Guid lotId, decimal quantity, string reason,
        CancellationToken cancellationToken = default);
}
