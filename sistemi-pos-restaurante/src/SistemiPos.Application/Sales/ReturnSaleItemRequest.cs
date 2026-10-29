namespace SistemiPos.Application.Sales;

public sealed record ReturnSaleItemRequest(
    Guid ProductId,
    decimal Quantity,
    bool ReturnedToInventory,
    Guid? LotId = null);
