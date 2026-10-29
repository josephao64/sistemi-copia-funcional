using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Suppliers;

namespace SistemiPos.Domain.Purchases;

public sealed class SupplierProductPriceHistory : Entity
{
    public Guid ProductSupplierId { get; private set; }
    public ProductSupplier ProductSupplier { get; set; } = null!;
    public Guid ProductId { get; private set; }
    public Product Product { get; set; } = null!;
    public Guid SupplierId { get; private set; }
    public Supplier Supplier { get; set; } = null!;
    public Guid PurchaseId { get; private set; }
    public Purchase Purchase { get; set; } = null!;
    public DateTimeOffset PurchaseDateUtc { get; private set; }
    public SupplierPackageType PackageType { get; private set; }
    public decimal PackageQuantity { get; private set; }
    public int UnitsPerPackage { get; private set; }
    public decimal PackageCost { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal TotalCost { get; private set; }
    public string PackageName { get; private set; } = "Unidad";
    public decimal LineDiscountAmount { get; private set; }
    public decimal GlobalDiscountAmount { get; private set; }

    private SupplierProductPriceHistory() { }

    public SupplierProductPriceHistory(Guid productSupplierId, Guid productId, Guid supplierId,
        Guid purchaseId, DateTimeOffset purchaseDateUtc, SupplierPackageType packageType,
        decimal packageQuantity, int unitsPerPackage, decimal packageCost,
        decimal? effectiveUnitCost = null, string? packageName = null,
        decimal lineDiscountAmount = 0, decimal globalDiscountAmount = 0)
    {
        if (productSupplierId == Guid.Empty || productId == Guid.Empty || supplierId == Guid.Empty || purchaseId == Guid.Empty)
            throw new DomainRuleException("La referencia del historial de precios no es válida.");
        if (packageQuantity <= 0 || unitsPerPackage <= 0 || packageCost < 0)
            throw new DomainRuleException("Los datos del empaque no son válidos.");
        ProductSupplierId = productSupplierId;
        ProductId = productId;
        SupplierId = supplierId;
        PurchaseId = purchaseId;
        PurchaseDateUtc = purchaseDateUtc;
        PackageType = packageType;
        PackageQuantity = decimal.Round(packageQuantity, 2);
        UnitsPerPackage = unitsPerPackage;
        PackageCost = decimal.Round(packageCost, 2);
        PackageName = string.IsNullOrWhiteSpace(packageName) ? GetPackageName(packageType) : NameNormalizer.Normalize(packageName);
        LineDiscountAmount = decimal.Round(lineDiscountAmount, 2);
        GlobalDiscountAmount = decimal.Round(globalDiscountAmount, 2);
        UnitCost = decimal.Round(effectiveUnitCost ?? packageCost / unitsPerPackage, 6);
        TotalCost = decimal.Round(packageQuantity * unitsPerPackage * UnitCost, 2);
    }

    private static string GetPackageName(SupplierPackageType value) => value switch
    {
        SupplierPackageType.Box => "Caja",
        SupplierPackageType.Package => "Paquete",
        SupplierPackageType.Bundle => "Fardo",
        SupplierPackageType.Dozen => "Docena",
        SupplierPackageType.Other => "Otro",
        _ => "Unidad"
    };
}
