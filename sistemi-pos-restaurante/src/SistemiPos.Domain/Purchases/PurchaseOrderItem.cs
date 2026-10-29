using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Purchases;

public sealed class PurchaseOrderItem : Entity
{
    public Guid PurchaseOrderId { get; set; }
    public Guid ProductId { get; private set; }
    public Product Product { get; set; } = null!;
    public Guid? PresentationId { get; private set; }
    public PurchasePresentation? Presentation { get; set; }
    public Guid? ProductPresentationId { get; private set; }
    public ProductPurchasePresentation? ProductPresentation { get; set; }
    public string PresentationName { get; private set; } = "Unidad";
    public decimal PackageQuantity { get; private set; }
    public int UnitsPerPackage { get; private set; } = 1;
    public decimal BaseQuantityPerPackage { get; private set; } = 1;
    public string? BaseUnitCode { get; private set; }
    public decimal RequestedQuantity { get; private set; }
    public decimal ReceivedQuantity { get; private set; }
    public decimal PendingQuantity => Math.Max(0, RequestedQuantity - ReceivedQuantity);

    private PurchaseOrderItem()
    {
    }

    public PurchaseOrderItem(Guid productId, decimal requestedQuantity)
        : this(productId, requestedQuantity, null, "Unidad", 1)
    {
    }

    public PurchaseOrderItem(Guid productId, decimal packageQuantity, Guid? presentationId,
        string presentationName, int unitsPerPackage)
        : this(productId, packageQuantity, presentationId, null, presentationName, unitsPerPackage, unitsPerPackage, null)
    {
    }

    public PurchaseOrderItem(Guid productId, decimal packageQuantity, Guid? presentationId,
        Guid? productPresentationId, string presentationName, int unitsPerPackage,
        decimal baseQuantityPerPackage, string? baseUnitCode)
    {
        if (productId == Guid.Empty || packageQuantity <= 0)
        {
            throw new DomainRuleException("El producto y la cantidad solicitada son requeridos.");
        }
        if (unitsPerPackage <= 0 || baseQuantityPerPackage <= 0 || string.IsNullOrWhiteSpace(presentationName)
            || (presentationId.HasValue && productPresentationId.HasValue))
        {
            throw new DomainRuleException("La presentación seleccionada no es válida.");
        }

        ProductId = productId;
        SetPresentation(packageQuantity, presentationId, productPresentationId, presentationName,
            unitsPerPackage, baseQuantityPerPackage, baseUnitCode);
    }

    public void UpdatePresentation(decimal packageQuantity, Guid? presentationId,
        string presentationName, int unitsPerPackage)
        => UpdatePresentation(packageQuantity, presentationId, null, presentationName,
            unitsPerPackage, unitsPerPackage, null);

    public void UpdatePresentation(decimal packageQuantity, Guid? presentationId,
        Guid? productPresentationId, string presentationName, int unitsPerPackage,
        decimal baseQuantityPerPackage, string? baseUnitCode)
    {
        if (ReceivedQuantity > 0)
            throw new DomainRuleException("No se puede cambiar una línea que ya tiene recepciones.");
        if (packageQuantity <= 0)
        {
            throw new DomainRuleException("La cantidad solicitada debe ser mayor a cero.");
        }
        if (unitsPerPackage <= 0 || baseQuantityPerPackage <= 0 || string.IsNullOrWhiteSpace(presentationName)
            || (presentationId.HasValue && productPresentationId.HasValue))
        {
            throw new DomainRuleException("La presentación seleccionada no es válida.");
        }

        SetPresentation(packageQuantity, presentationId, productPresentationId, presentationName,
            unitsPerPackage, baseQuantityPerPackage, baseUnitCode);
    }

    public void RecordReceipt(decimal quantity)
    {
        if (quantity < 0)
            throw new DomainRuleException("La cantidad recibida no puede ser negativa.");
        if (quantity == 0) return;
        ReceivedQuantity = decimal.Round(ReceivedQuantity + quantity, 2);
    }

    public void ReverseReceipt(decimal quantity)
    {
        if (quantity < 0 || quantity > ReceivedQuantity)
            throw new DomainRuleException("La cantidad a revertir supera lo recibido en la orden.");
        ReceivedQuantity = decimal.Round(ReceivedQuantity - quantity, 2);
    }

    private void SetPresentation(decimal packageQuantity, Guid? presentationId,
        Guid? productPresentationId, string presentationName, int unitsPerPackage,
        decimal baseQuantityPerPackage, string? baseUnitCode)
    {
        PresentationId = presentationId;
        ProductPresentationId = productPresentationId;
        PresentationName = presentationName.Trim();
        PackageQuantity = decimal.Round(packageQuantity, 2);
        UnitsPerPackage = unitsPerPackage;
        BaseQuantityPerPackage = decimal.Round(baseQuantityPerPackage, 6);
        BaseUnitCode = string.IsNullOrWhiteSpace(baseUnitCode) ? null : baseUnitCode.Trim();
        RequestedQuantity = decimal.Round(PackageQuantity * BaseQuantityPerPackage, 2);
    }
}
