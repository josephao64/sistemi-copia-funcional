using SistemiPos.Domain.Purchases;

namespace SistemiPos.Application.Purchases;

public sealed record CreateDirectPurchaseRequest(
    Guid SupplierId,
    DateTimeOffset SupplierDocumentDateUtc,
    string SupplierDocumentNumber,
    string SupplierNit,
    PurchaseTermsType Terms,
    int? CreditDays,
    string? Observations,
    IReadOnlyList<DirectPurchaseItemRequest> Items,
    PurchaseDiscountType GlobalDiscountType = PurchaseDiscountType.None,
    decimal GlobalDiscountValue = 0,
    Guid? BranchId = null,
    Guid? WarehouseId = null);

public sealed record DirectPurchaseItemRequest(
    Guid ProductId,
    decimal PackageQuantity,
    int UnitsPerPackage,
    decimal PackageCost,
    SupplierPackageType PackageType,
    string SupplierProductName,
    string? SupplierProductCode,
    DateOnly? ExpiryDate = null,
    Guid PresentationId = default,
    PurchaseDiscountType DiscountType = PurchaseDiscountType.None,
    decimal DiscountValue = 0,
    PurchaseProductPricingRequest? Pricing = null,
    string? LotNumber = null);
