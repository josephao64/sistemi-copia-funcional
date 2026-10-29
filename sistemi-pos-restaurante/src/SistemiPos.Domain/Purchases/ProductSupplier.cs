using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Suppliers;

namespace SistemiPos.Domain.Purchases;

public sealed class ProductSupplier : AuditableEntity
{
    public Guid ProductId { get; private set; }
    public Product Product { get; set; } = null!;
    public Guid SupplierId { get; private set; }
    public Supplier Supplier { get; set; } = null!;
    public string SupplierProductName { get; private set; } = string.Empty;
    public string? SupplierProductCode { get; private set; }
    public SupplierPackageType PackageType { get; private set; }
    public string PackageName { get; private set; } = "Unidad";
    public int UnitsPerPackage { get; private set; }
    public decimal LastPackageCost { get; private set; }
    public decimal LastUnitCost { get; private set; }
    public DateTimeOffset? LastPurchaseDateUtc { get; private set; }
    public PurchaseTermsType? Terms { get; private set; }
    public int? CreditDays { get; private set; }

    private ProductSupplier() { }

    public ProductSupplier(Guid productId, Guid supplierId, string supplierProductName,
        string? supplierProductCode, SupplierPackageType packageType, int unitsPerPackage,
        PurchaseTermsType? terms = null, int? creditDays = null) =>
        Update(supplierProductName, supplierProductCode, packageType, unitsPerPackage, terms, creditDays, productId, supplierId);

    public void Update(string supplierProductName, string? supplierProductCode,
        SupplierPackageType packageType, int unitsPerPackage, PurchaseTermsType? terms, int? creditDays) =>
        Update(supplierProductName, supplierProductCode, packageType, unitsPerPackage, terms, creditDays, ProductId, SupplierId);

    public void SetPackageName(string packageName) =>
        PackageName = string.IsNullOrWhiteSpace(packageName) ? "Unidad" : NameNormalizer.Normalize(packageName);

    public void RegisterPurchase(decimal packageCost, DateTimeOffset purchasedAtUtc,
        PurchaseTermsType terms, int? creditDays)
    {
        if (packageCost < 0) throw new DomainRuleException("El costo por empaque no puede ser negativo.");
        LastPackageCost = decimal.Round(packageCost, 2);
        LastUnitCost = decimal.Round(packageCost / UnitsPerPackage, 6);
        LastPurchaseDateUtc = purchasedAtUtc;
        Terms = terms;
        CreditDays = terms == PurchaseTermsType.Credit ? creditDays : null;
    }

    private void Update(string supplierProductName, string? supplierProductCode,
        SupplierPackageType packageType, int unitsPerPackage, PurchaseTermsType? terms,
        int? creditDays, Guid productId, Guid supplierId)
    {
        if (productId == Guid.Empty || supplierId == Guid.Empty)
            throw new DomainRuleException("El producto y el proveedor son requeridos.");
        if (string.IsNullOrWhiteSpace(supplierProductName))
            throw new DomainRuleException("El nombre usado por el proveedor es requerido.");
        if (!Enum.IsDefined(packageType)) throw new DomainRuleException("El tipo de empaque no es válido.");
        if (unitsPerPackage <= 0) throw new DomainRuleException("Las unidades por empaque deben ser mayores a cero.");
        if (terms == PurchaseTermsType.Credit && creditDays is not > 0)
            throw new DomainRuleException("Los días de crédito deben ser mayores a cero.");
        ProductId = productId;
        SupplierId = supplierId;
        SupplierProductName = NameNormalizer.Normalize(supplierProductName);
        SupplierProductCode = string.IsNullOrWhiteSpace(supplierProductCode) ? null : supplierProductCode.Trim();
        PackageType = packageType;
        PackageName = packageType switch
        {
            SupplierPackageType.Box => "Caja",
            SupplierPackageType.Package => "Paquete",
            SupplierPackageType.Bundle => "Fardo",
            SupplierPackageType.Dozen => "Docena",
            SupplierPackageType.Other => "Otro",
            _ => "Unidad"
        };
        UnitsPerPackage = unitsPerPackage;
        Terms = terms;
        CreditDays = terms == PurchaseTermsType.Credit ? creditDays : null;
    }
}
