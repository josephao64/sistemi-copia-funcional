using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Organizations;

namespace SistemiPos.Domain.Purchases;

public sealed class PurchaseReceipt : AuditableEntity
{
    private readonly List<PurchaseReceiptItem> _items = [];
    public string Number { get; private set; } = string.Empty;
    public Guid PurchaseOrderId { get; private set; }
    public Guid PurchaseId { get; private set; }
    public Purchase Purchase { get; set; } = null!;
    public Guid SupplierId { get; private set; }
    public Guid? BranchId { get; private set; }
    public Branch? Branch { get; set; }
    public Guid? WarehouseId { get; private set; }
    public Warehouse? Warehouse { get; set; }
    public string SupplierDocumentNumber { get; private set; } = string.Empty;
    public DateTimeOffset SupplierDocumentDateUtc { get; private set; }
    public DateTimeOffset ReceivedAtUtc { get; private set; }
    public PurchaseTermsType Terms { get; private set; }
    public int? CreditDays { get; private set; }
    public DateTimeOffset? DueDateUtc { get; private set; }
    public string? Observations { get; private set; }
    public decimal Total { get; private set; }
    public IReadOnlyCollection<PurchaseReceiptItem> Items => _items;
    private PurchaseReceipt() { }

    public PurchaseReceipt(string number, Guid purchaseOrderId, Guid purchaseId, Guid supplierId,
        string supplierDocumentNumber, DateTimeOffset supplierDocumentDateUtc, DateTimeOffset receivedAtUtc,
        PurchaseTermsType terms, int? creditDays, DateTimeOffset? dueDateUtc, string? observations,
        IEnumerable<PurchaseReceiptItem> items,
        Guid? branchId = null, Guid? warehouseId = null)
    {
        BranchId = branchId;
        WarehouseId = warehouseId;
        var values = items.ToList();
        if (string.IsNullOrWhiteSpace(number) || purchaseOrderId == Guid.Empty || purchaseId == Guid.Empty || supplierId == Guid.Empty)
            throw new DomainRuleException("La identificación de la recepción no es válida.");
        if (string.IsNullOrWhiteSpace(supplierDocumentNumber))
            throw new DomainRuleException("El número de factura o documento es requerido.");
        if (terms is not (PurchaseTermsType.Cash or PurchaseTermsType.Credit))
            throw new DomainRuleException("La condición de compra no es válida.");
        if (terms == PurchaseTermsType.Credit && (creditDays is null or <= 0 || dueDateUtc is null))
            throw new DomainRuleException("Los días y el vencimiento son requeridos para una compra a crédito.");
        if (values.Count == 0 || values.Sum(x => x.Subtotal) <= 0)
            throw new DomainRuleException("La recepción debe incluir al menos un producto con cantidad y costo válidos.");
        if (values.GroupBy(x => x.ProductId).Any(x => x.Count() > 1))
            throw new DomainRuleException("Un producto no puede repetirse en la recepción.");
        Number = number.Trim(); PurchaseOrderId = purchaseOrderId; PurchaseId = purchaseId; SupplierId = supplierId;
        SupplierDocumentNumber = supplierDocumentNumber.Trim(); SupplierDocumentDateUtc = supplierDocumentDateUtc;
        ReceivedAtUtc = receivedAtUtc; Terms = terms; CreditDays = terms == PurchaseTermsType.Credit ? creditDays : null;
        DueDateUtc = terms == PurchaseTermsType.Credit ? dueDateUtc : null;
        Observations = string.IsNullOrWhiteSpace(observations) ? null : observations.Trim();
        _items.AddRange(values); Total = decimal.Round(values.Sum(x => x.Subtotal), 2);
    }
}

public sealed class PurchaseReceiptItem : Entity
{
    public Guid PurchaseReceiptId { get; set; }
    public Guid ProductId { get; private set; }
    public Product Product { get; set; } = null!;
    public Guid? PurchaseOrderItemId { get; private set; }
    public decimal RequestedQuantity { get; private set; }
    public decimal ReceivedQuantity { get; private set; }
    public decimal ActualUnitCost { get; private set; }
    public bool IsAdditional { get; private set; }
    public decimal Subtotal { get; private set; }
    private PurchaseReceiptItem() { }

    public PurchaseReceiptItem(Guid productId, Guid? purchaseOrderItemId, decimal requestedQuantity,
        decimal receivedQuantity, decimal actualUnitCost, bool isAdditional)
    {
        if (productId == Guid.Empty || requestedQuantity < 0 || receivedQuantity < 0 || actualUnitCost < 0)
            throw new DomainRuleException("Los datos de la línea de recepción no son válidos.");
        if (isAdditional && (purchaseOrderItemId.HasValue || requestedQuantity != 0 || receivedQuantity <= 0))
            throw new DomainRuleException("La línea adicional de recepción no es válida.");
        if (!isAdditional && !purchaseOrderItemId.HasValue)
            throw new DomainRuleException("La línea solicitada debe relacionarse con la orden.");
        ProductId = productId; PurchaseOrderItemId = purchaseOrderItemId;
        RequestedQuantity = decimal.Round(requestedQuantity, 2); ReceivedQuantity = decimal.Round(receivedQuantity, 2);
        ActualUnitCost = decimal.Round(actualUnitCost, 6); IsAdditional = isAdditional;
        Subtotal = decimal.Round(ReceivedQuantity * ActualUnitCost, 2);
    }
}
