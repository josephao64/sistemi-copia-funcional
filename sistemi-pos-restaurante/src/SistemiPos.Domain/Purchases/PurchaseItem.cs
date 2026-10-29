using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Catalog;

namespace SistemiPos.Domain.Purchases;

public sealed class PurchaseItem : Entity
{
    public Guid PurchaseId { get; set; }
    public Guid ProductId { get; private set; }
    public Product Product { get; set; } = null!;
    public decimal Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal Subtotal { get; private set; }
    public SupplierPackageType PackageType { get; private set; } = SupplierPackageType.Unit;
    public decimal PackageQuantity { get; private set; }
    public decimal? BaseQuantityPerPackage { get; private set; }
    public string? BaseUnitCode { get; private set; }
    public Guid? ProductPresentationId { get; private set; }
    public int UnitsPerPackage { get; private set; } = 1;
    public decimal PackageCost { get; private set; }
    public string PackageName { get; private set; } = "Unidad";
    public decimal GrossSubtotal { get; private set; }
    public PurchaseDiscountType LineDiscountType { get; private set; }
    public decimal LineDiscountValue { get; private set; }
    public decimal LineDiscountAmount { get; private set; }
    public decimal GlobalDiscountAmount { get; private set; }
    public string? SupplierProductName { get; private set; }
    public string? SupplierProductCode { get; private set; }

    private PurchaseItem()
    {
    }

    public PurchaseItem(Guid productId, decimal quantity, decimal unitCost,
        SupplierPackageType packageType = SupplierPackageType.Unit, decimal? packageQuantity = null,
        int unitsPerPackage = 1, decimal? packageCost = null, string? supplierProductName = null,
        string? supplierProductCode = null, string? packageName = null,
        PurchaseDiscountType lineDiscountType = PurchaseDiscountType.None,
        decimal lineDiscountValue = 0, decimal globalDiscountAmount = 0,
        decimal? baseQuantityPerPackage = null, string? baseUnitCode = null, Guid? productPresentationId = null)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainRuleException("El producto es requerido.");
        }

        if (quantity <= 0 || unitCost < 0)
        {
            throw new DomainRuleException("La cantidad debe ser mayor a cero y el costo no puede ser negativo.");
        }

        var factor = baseQuantityPerPackage ?? unitsPerPackage;
        if (factor <= 0 || quantity != decimal.Round(quantity, 2))
            throw new DomainRuleException("El factor o la precisión de la cantidad base no son válidos.");
        BaseQuantityPerPackage = factor;
        BaseUnitCode = baseUnitCode;
        ProductPresentationId = productPresentationId;
        ProductId = productId;
        Quantity = decimal.Round(quantity, 2);
        UnitCost = decimal.Round(unitCost, 6);
        PackageType = packageType;
        PackageQuantity = decimal.Round(packageQuantity ?? quantity, 2);
        UnitsPerPackage = unitsPerPackage;
        PackageCost = decimal.Round(packageCost ?? unitCost, 2);
        PackageName = string.IsNullOrWhiteSpace(packageName) ? GetPackageName(packageType) : NameNormalizer.Normalize(packageName);
        SupplierProductName = NameNormalizer.NormalizeOptional(supplierProductName);
        SupplierProductCode = string.IsNullOrWhiteSpace(supplierProductCode) ? null : supplierProductCode.Trim();
        if (!Enum.IsDefined(packageType) || PackageQuantity <= 0 || unitsPerPackage <= 0 || PackageCost < 0
            || Quantity != decimal.Round(PackageQuantity * factor, 2))
            throw new DomainRuleException("Los datos del empaque no corresponden con las unidades recibidas.");
        if (!Enum.IsDefined(lineDiscountType) || lineDiscountValue < 0 || globalDiscountAmount < 0)
            throw new DomainRuleException("Los descuentos de la línea no son válidos.");
        GrossSubtotal = decimal.Round(PackageQuantity * PackageCost, 2);
        LineDiscountType = lineDiscountType;
        LineDiscountValue = decimal.Round(lineDiscountValue, 2);
        LineDiscountAmount = lineDiscountType switch
        {
            PurchaseDiscountType.None => 0,
            PurchaseDiscountType.Percentage when lineDiscountValue <= 100 => decimal.Round(GrossSubtotal * lineDiscountValue / 100m, 2),
            PurchaseDiscountType.Amount when lineDiscountValue <= GrossSubtotal => decimal.Round(lineDiscountValue, 2),
            _ => throw new DomainRuleException("El descuento de la línea supera el valor permitido.")
        };
        var netBeforeGlobal = GrossSubtotal - LineDiscountAmount;
        if (globalDiscountAmount > netBeforeGlobal)
            throw new DomainRuleException("El descuento global asignado supera el total de la línea.");
        GlobalDiscountAmount = decimal.Round(globalDiscountAmount, 2);
        Subtotal = decimal.Round(netBeforeGlobal - GlobalDiscountAmount, 2);
        UnitCost = decimal.Round(Subtotal / Quantity, 6);
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
