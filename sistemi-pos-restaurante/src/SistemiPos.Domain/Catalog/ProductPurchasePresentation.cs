using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Catalog;

public sealed class ProductPurchasePresentation : AuditableEntity
{
    public Guid ProductId { get; private set; }
    public Product Product { get; set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public Guid? PurchaseUnitId { get; private set; }
    public UnitOfMeasure? PurchaseUnit { get; set; }
    public decimal BaseQuantityPerPackage { get; private set; }
    public bool IsActive { get; private set; } = true;

    private ProductPurchasePresentation()
    {
    }

    public ProductPurchasePresentation(
        Guid productId,
        string name,
        decimal baseQuantityPerPackage,
        Guid? purchaseUnitId = null,
        bool isActive = true)
    {
        if (productId == Guid.Empty)
            throw new DomainRuleException("El producto es requerido.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainRuleException("El nombre de la presentación es requerido.");
        if (baseQuantityPerPackage <= 0)
            throw new DomainRuleException("La cantidad base por paquete debe ser mayor a cero.");

        ProductId = productId;
        Name = NameNormalizer.Normalize(name);
        BaseQuantityPerPackage = baseQuantityPerPackage;
        PurchaseUnitId = purchaseUnitId;
        IsActive = isActive;
    }

    public void Update(string name, decimal baseQuantityPerPackage, Guid? purchaseUnitId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainRuleException("El nombre de la presentación es requerido.");
        if (baseQuantityPerPackage <= 0)
            throw new DomainRuleException("La cantidad base por paquete debe ser mayor a cero.");

        Name = NameNormalizer.Normalize(name);
        BaseQuantityPerPackage = baseQuantityPerPackage;
        PurchaseUnitId = purchaseUnitId;
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}
