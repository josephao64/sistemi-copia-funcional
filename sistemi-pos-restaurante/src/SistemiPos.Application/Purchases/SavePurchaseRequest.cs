namespace SistemiPos.Application.Purchases;

public sealed record SavePurchaseRequest(Guid SupplierId, DateTimeOffset PurchaseDateUtc, List<PurchaseItemRequest> Items,
    SistemiPos.Domain.Purchases.PurchaseTermsType Terms = SistemiPos.Domain.Purchases.PurchaseTermsType.Cash,
    DateTimeOffset? DueDateUtc = null,
    Guid? BranchId = null,
    Guid? WarehouseId = null);
