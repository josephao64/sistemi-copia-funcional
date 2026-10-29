namespace SistemiPos.Application.Inventory;

public sealed record InventoryStockDto(
    Guid ProductId,
    string InternalCode,
    string ProductName,
    string CategoryName,
    decimal CurrentStock,
    decimal MinimumStock,
    bool IsLowStock,
    Guid? WarehouseId = null,
    string? WarehouseName = null,
    Guid? BranchId = null,
    string? BranchName = null,
    bool ManagesLots = true);

