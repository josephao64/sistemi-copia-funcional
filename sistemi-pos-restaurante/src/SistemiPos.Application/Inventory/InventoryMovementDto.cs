using SistemiPos.Domain.Inventory;

namespace SistemiPos.Application.Inventory;

public sealed record InventoryMovementDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    InventoryMovementType Type,
    decimal Quantity,
    decimal PreviousStock,
    decimal NewStock,
    DateTimeOffset OccurredAtUtc,
    string UserName,
    string? SourceDocument,
    string? Note,
    Guid? WarehouseId = null,
    string? WarehouseName = null,
    decimal? UnitCost = null);
