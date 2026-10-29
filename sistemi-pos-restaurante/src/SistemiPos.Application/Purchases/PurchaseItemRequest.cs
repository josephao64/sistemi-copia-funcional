namespace SistemiPos.Application.Purchases;

public sealed record PurchaseItemRequest(Guid ProductId, decimal Quantity, decimal UnitCost);
