using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Organizations;
using SistemiPos.Domain.Suppliers;

namespace SistemiPos.Domain.Purchases;

public sealed class Purchase : AuditableEntity
{
    private readonly List<PurchaseItem> _items = [];

    public string Number { get; private set; } = string.Empty;
    public Guid SupplierId { get; private set; }
    public Supplier Supplier { get; set; } = null!;
    public Guid? BranchId { get; private set; }
    public Branch? Branch { get; set; }
    public Guid? WarehouseId { get; private set; }
    public Warehouse? Warehouse { get; set; }
    public DateTimeOffset PurchaseDateUtc { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;
    public DateTimeOffset? ConfirmedAtUtc { get; private set; }
    public string? ConfirmedBy { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public string? CancelledBy { get; private set; }
    public string? CancellationReason { get; private set; }
    public decimal Total { get; private set; }
    public decimal GrossSubtotal { get; private set; }
    public decimal LineDiscountTotal { get; private set; }
    public decimal NetSubtotal { get; private set; }
    public PurchaseDiscountType GlobalDiscountType { get; private set; }
    public decimal GlobalDiscountValue { get; private set; }
    public decimal GlobalDiscountAmount { get; private set; }
    public PurchaseTermsType Terms { get; private set; } = PurchaseTermsType.Cash;
    public DateTimeOffset? DueDateUtc { get; private set; }
    public Guid? PurchaseOrderId { get; private set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public string? InvoiceNumber { get; private set; }
    public string? SupplierName { get; private set; }
    public string? SupplierNit { get; private set; }
    public string? Observations { get; private set; }
    public IReadOnlyCollection<PurchaseItem> Items => _items;

    private Purchase()
    {
    }

    public Purchase(string number, Guid supplierId, DateTimeOffset purchaseDateUtc, string userName, IEnumerable<PurchaseItem> items,
        PurchaseTermsType terms = PurchaseTermsType.Cash, DateTimeOffset? dueDateUtc = null,
        Guid? purchaseOrderId = null, string? invoiceNumber = null, string? observations = null,
        string? supplierName = null, string? supplierNit = null,
        PurchaseDiscountType globalDiscountType = PurchaseDiscountType.None, decimal globalDiscountValue = 0,
        Guid? branchId = null, Guid? warehouseId = null)
    {
        BranchId = branchId;
        WarehouseId = warehouseId;
        if (string.IsNullOrWhiteSpace(number))
        {
            throw new DomainRuleException("El número de compra es requerido.");
        }

        if (supplierId == Guid.Empty)
        {
            throw new DomainRuleException("El proveedor es requerido.");
        }

        Number = number.Trim();
        SupplierId = supplierId;
        PurchaseDateUtc = purchaseDateUtc;
        UserName = string.IsNullOrWhiteSpace(userName) ? "system" : userName.Trim();
        if (terms == PurchaseTermsType.Credit && dueDateUtc is null)
            throw new DomainRuleException("La fecha de vencimiento es requerida para compras a crédito.");
        if (dueDateUtc < purchaseDateUtc) throw new DomainRuleException("El vencimiento no puede ser anterior a la compra.");
        Terms = terms;
        DueDateUtc = terms == PurchaseTermsType.Credit ? dueDateUtc : null;
        PurchaseOrderId = purchaseOrderId;
        InvoiceNumber = string.IsNullOrWhiteSpace(invoiceNumber) ? null : invoiceNumber.Trim();
        SupplierName = string.IsNullOrWhiteSpace(supplierName) ? null : supplierName.Trim();
        SupplierNit = string.IsNullOrWhiteSpace(supplierNit) ? null : supplierNit.Trim();
        Observations = string.IsNullOrWhiteSpace(observations) ? null : observations.Trim();
        if (!Enum.IsDefined(globalDiscountType) || globalDiscountValue < 0)
            throw new DomainRuleException("El descuento global no es válido.");
        GlobalDiscountType = globalDiscountType;
        GlobalDiscountValue = decimal.Round(globalDiscountValue, 2);

        ReplaceItems(items);
    }

    public void ReplaceItems(IEnumerable<PurchaseItem> items)
    {
        EnsureDraft();
        var newItems = items.ToList();
        if (newItems.Count == 0)
        {
            throw new DomainRuleException("La compra debe incluir al menos un producto.");
        }

        _items.Clear();
        _items.AddRange(newItems);
        GrossSubtotal = decimal.Round(_items.Sum(x => x.GrossSubtotal), 2);
        LineDiscountTotal = decimal.Round(_items.Sum(x => x.LineDiscountAmount), 2);
        NetSubtotal = GrossSubtotal - LineDiscountTotal;
        GlobalDiscountAmount = decimal.Round(_items.Sum(x => x.GlobalDiscountAmount), 2);
        Total = decimal.Round(_items.Sum(x => x.Subtotal), 2);
    }

    public void Confirm(DateTimeOffset? confirmedAtUtc = null, string? confirmedBy = null)
    {
        EnsureDraft();

        Status = DocumentStatus.Confirmed;
        ConfirmedAtUtc = confirmedAtUtc ?? PurchaseDateUtc;
        ConfirmedBy = string.IsNullOrWhiteSpace(confirmedBy) ? UserName : confirmedBy.Trim();
    }

    public void AssignDestination(Guid branchId, Guid warehouseId)
    {
        EnsureDraft();

        if (branchId == Guid.Empty || warehouseId == Guid.Empty)
        {
            throw new DomainRuleException("La sucursal y la bodega de destino son requeridas.");
        }

        BranchId = branchId;
        WarehouseId = warehouseId;
    }

    public void EnsureCanCancel(string reason)
    {
        if (Status != DocumentStatus.Confirmed)
            throw new DomainRuleException("Solo se puede anular una compra confirmada.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw new DomainRuleException("Indique un motivo de anulación de hasta 500 caracteres.");
    }

    public void Cancel(string reason, DateTimeOffset cancelledAtUtc, string userName)
    {
        EnsureCanCancel(reason);
        Status = DocumentStatus.Cancelled;
        CancellationReason = reason.Trim();
        CancelledAtUtc = cancelledAtUtc;
        CancelledBy = string.IsNullOrWhiteSpace(userName) ? "system" : userName.Trim();
    }

    public void EnsureDraft()
    {
        if (Status != DocumentStatus.Draft)
            throw new DomainRuleException("La compra ya fue confirmada o anulada y no puede modificarse.");
    }
}
