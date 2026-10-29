using SistemiPos.Domain.Purchases;

namespace SistemiPos.Application.Purchases;

public sealed record SaveProductSupplierRequest(Guid ProductId, Guid SupplierId,
    string SupplierProductName, string? SupplierProductCode, SupplierPackageType PackageType,
    int UnitsPerPackage, PurchaseTermsType? Terms, int? CreditDays);

public sealed record ProductSupplierDto(Guid Id, Guid ProductId, string ProductName,
    Guid SupplierId, string SupplierName, string SupplierProductName, string? SupplierProductCode,
    SupplierPackageType PackageType, int UnitsPerPackage, decimal LastPackageCost,
    decimal LastUnitCost, DateTimeOffset? LastPurchaseDateUtc, PurchaseTermsType? Terms, int? CreditDays,
    string PackageName = "Unidad");

public sealed record SupplierProductPriceHistoryDto(Guid Id, Guid ProductId, string ProductName,
    Guid SupplierId, string SupplierName, DateTimeOffset PurchaseDateUtc,
    SupplierPackageType PackageType, decimal PackageQuantity, int UnitsPerPackage,
    decimal PackageCost, decimal UnitCost, decimal TotalCost, string PackageName = "Unidad",
    decimal LineDiscountAmount = 0, decimal GlobalDiscountAmount = 0);
