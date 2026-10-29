using SistemiPos.Domain.Inventory;

namespace SistemiPos.Application.Inventory;

public sealed record CreateInventoryTransferItemRequest(
    Guid ProductId,
    decimal Quantity,
    decimal? AssignedUnitCost = null,
    Guid? OriginLotId = null,
    string? LotNumber = null,
    DateOnly? ExpiryDate = null);

public sealed record CreateInventoryTransferRequest(
    Guid OriginWarehouseId,
    Guid DestinationWarehouseId,
    IReadOnlyList<CreateInventoryTransferItemRequest> Items,
    string? Observations = null);

public sealed record ReceiveTransferLineRequest(
    Guid ItemId,
    decimal QuantityReceived);

public sealed record ReceiveInventoryTransferRequest(
    IReadOnlyList<ReceiveTransferLineRequest>? Lines = null);

public sealed record CancelInventoryTransferRequest(
    string? Reason = null);

public sealed record InventoryTransferItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string ProductCode,
    decimal QuantitySent,
    decimal QuantityReceived,
    decimal UnitCostOrigin,
    decimal AssignedUnitCost,
    Guid? OriginLotId,
    string? LotNumber,
    DateOnly? ExpiryDate);

public sealed record InventoryTransferDto(
    Guid Id,
    string Number,
    Guid OriginWarehouseId,
    string OriginWarehouseName,
    Guid DestinationWarehouseId,
    string DestinationWarehouseName,
    InventoryTransferStatus Status,
    DateTimeOffset RequestedAtUtc,
    string RequestedBy,
    DateTimeOffset? DispatchedAtUtc,
    string? DispatchedBy,
    DateTimeOffset? ReceivedAtUtc,
    string? ReceivedBy,
    DateTimeOffset? CancelledAtUtc,
    string? CancelledBy,
    string? CancellationReason,
    string? Observations,
    IReadOnlyList<InventoryTransferItemDto> Items);
