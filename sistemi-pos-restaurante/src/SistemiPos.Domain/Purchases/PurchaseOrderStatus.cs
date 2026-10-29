namespace SistemiPos.Domain.Purchases;

public enum PurchaseOrderStatus
{
    Requested = 1,
    PartiallyReceived = 2,
    Received = 3,
    Cancelled = 4,
    Draft = 5
}
