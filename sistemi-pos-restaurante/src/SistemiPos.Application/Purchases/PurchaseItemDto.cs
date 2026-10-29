using SistemiPos.Domain.Purchases;

namespace SistemiPos.Application.Purchases;

public sealed record PurchaseItemDto(Guid ProductId, string ProductName, decimal Quantity, decimal UnitCost, decimal Subtotal,
    string? SupplierInvoiceName = null, string? SystemDescription = null, string? SupplierDescription = null,
    SupplierPackageType PackageType = SupplierPackageType.Unit, decimal PackageQuantity = 0,
    int UnitsPerPackage = 1, decimal PackageCost = 0, string? SupplierProductCode = null,
    string PackageName = "Unidad", decimal GrossSubtotal = 0,
    PurchaseDiscountType DiscountType = PurchaseDiscountType.None, decimal DiscountValue = 0,
    decimal LineDiscountAmount = 0, decimal GlobalDiscountAmount = 0,
    decimal? BaseQuantityPerPackage = null, string? BaseUnitCode = null);
