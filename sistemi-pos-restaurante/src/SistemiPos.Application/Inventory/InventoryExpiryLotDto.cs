namespace SistemiPos.Application.Inventory;

public sealed record InventoryExpiryLotDto(Guid ProductId, string ProductName, string CategoryName,
    decimal AvailableQuantity, DateOnly ExpiryDate, int DaysRemaining, string Status,
    Guid LotId = default, string LotNumber = "", Guid? WarehouseId = null, string? WarehouseName = null);
