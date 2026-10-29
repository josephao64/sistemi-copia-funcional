using SistemiPos.Domain.Purchases;

namespace SistemiPos.Application.Purchases;

public sealed record ReceivePurchaseOrderRequest(
    IReadOnlyList<ReceivePurchaseOrderItemRequest> Items,
    string SupplierDocumentNumber = "",
    DateTimeOffset SupplierDocumentDateUtc = default,
    PurchaseTermsType Terms = PurchaseTermsType.Cash,
    int? CreditDays = null,
    string? Observations = null,
    PurchaseDiscountType GlobalDiscountType = PurchaseDiscountType.None,
    decimal GlobalDiscountValue = 0,
    string SupplierNit = "");

public sealed record ReceivePurchaseOrderItemRequest(
    Guid ItemId,
    decimal Quantity,
    decimal ActualUnitCost = 0,
    Guid ProductId = default,
    bool IsAdditional = false,
    DateOnly? ExpiryDate = null,
    SupplierPackageType PackageType = SupplierPackageType.Unit,
    decimal PackageQuantity = 0,
    int UnitsPerPackage = 1,
    decimal PackageCost = 0,
    string SupplierProductName = "",
    string? SupplierProductCode = null,
    Guid PresentationId = default,
    PurchaseDiscountType DiscountType = PurchaseDiscountType.None,
    decimal DiscountValue = 0,
    PurchaseProductPricingRequest? Pricing = null,
    string? LotNumber = null);
