using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Organizations;
using SistemiPos.Domain.Suppliers;

namespace SistemiPos.Domain.Purchases;

public sealed class PurchaseOrder : AuditableEntity
{
    private readonly List<PurchaseOrderItem> _items = [];
    private readonly List<Purchase> _purchases = [];
    private readonly List<PurchaseReceipt> _receipts = [];
    public string Number { get; private set; } = string.Empty;
    public Guid SupplierId { get; private set; }
    public Supplier Supplier { get; set; } = null!;
    public Guid? BranchId { get; private set; }
    public Branch? Branch { get; set; }
    public Guid? WarehouseId { get; private set; }
    public Warehouse? Warehouse { get; set; }
    public DateTimeOffset RequestedAtUtc { get; private set; }
    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public string? SubmittedBy { get; private set; }
    public DateTimeOffset? ReceivedAtUtc { get; private set; }
    public string? ReceivedBy { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string? Observations { get; private set; }
    public PurchaseOrderStatus Status { get; private set; } = PurchaseOrderStatus.Draft;
    public uint Version { get; private set; }
    public IReadOnlyCollection<Purchase> Purchases => _purchases;
    public IReadOnlyCollection<PurchaseReceipt> Receipts => _receipts;
    public IReadOnlyCollection<PurchaseOrderItem> Items => _items;
    private PurchaseOrder() { }

    public PurchaseOrder(string number, Guid supplierId, DateTimeOffset requestedAtUtc, string userName,
        IEnumerable<PurchaseOrderItem> items, string? observations = null,
        Guid? branchId = null, Guid? warehouseId = null)
    {
        BranchId = branchId;
        WarehouseId = warehouseId;
        if (string.IsNullOrWhiteSpace(number) || supplierId == Guid.Empty)
            throw new DomainRuleException("El número y el proveedor de la orden son requeridos.");
        var values = ValidateItems(items);
        Number = number.Trim(); SupplierId = supplierId; RequestedAtUtc = requestedAtUtc;
        UserName = string.IsNullOrWhiteSpace(userName) ? "system" : userName.Trim();
        Observations = Normalize(observations); _items.AddRange(values);
    }

    public void AssignDestination(Guid branchId, Guid warehouseId)
    {
        EnsureEditable();
        if (branchId == Guid.Empty || warehouseId == Guid.Empty)
            throw new DomainRuleException("La sucursal y la bodega de destino son requeridas.");
        BranchId = branchId;
        WarehouseId = warehouseId;
    }

    public void Update(Guid supplierId, IEnumerable<PurchaseOrderItem> items, string? observations)
    {
        EnsureEditable();
        if (supplierId == Guid.Empty) throw new DomainRuleException("El proveedor es requerido.");
        var values = ValidateItems(items);
        foreach (var existingItem in _items.ToList())
        {
            var updatedItem = values.FirstOrDefault(x => x.ProductId == existingItem.ProductId);
            if (updatedItem is null)
            {
                _items.Remove(existingItem);
                continue;
            }

            existingItem.UpdatePresentation(updatedItem.PackageQuantity, updatedItem.PresentationId,
                updatedItem.ProductPresentationId, updatedItem.PresentationName, updatedItem.UnitsPerPackage,
                updatedItem.BaseQuantityPerPackage, updatedItem.BaseUnitCode);
        }

        var existingProductIds = _items.Select(x => x.ProductId).ToHashSet();
        _items.AddRange(values.Where(x => !existingProductIds.Contains(x.ProductId)));
        SupplierId = supplierId;
        Observations = Normalize(observations);
    }

    public void Submit(DateTimeOffset submittedAtUtc, string userName)
    {
        EnsureEditable(); Status = PurchaseOrderStatus.Requested;
        if (!SubmittedAtUtc.HasValue)
        {
            SubmittedAtUtc = submittedAtUtc;
            SubmittedBy = string.IsNullOrWhiteSpace(userName) ? "system" : userName.Trim();
        }
    }

    public void RecordReceipt(DateTimeOffset receivedAtUtc, string userName,
        IReadOnlyDictionary<Guid, decimal> receivedByItem)
    {
        if (Status is not (PurchaseOrderStatus.Requested or PurchaseOrderStatus.PartiallyReceived))
            throw new DomainRuleException("La orden no admite nuevas recepciones.");
        ModifiedAtUtc = receivedAtUtc;
        ModifiedBy = userName;
        foreach (var item in _items)
            item.RecordReceipt(receivedByItem.GetValueOrDefault(item.Id));

        if (_items.All(x => x.PendingQuantity == 0))
        {
            ReceivedAtUtc = receivedAtUtc;
            ReceivedBy = string.IsNullOrWhiteSpace(userName) ? "system" : userName.Trim();
            Status = PurchaseOrderStatus.Received;
        }
        else
        {
            Status = PurchaseOrderStatus.PartiallyReceived;
        }
    }

    public void CompleteReceipt(DateTimeOffset receivedAtUtc, string userName) =>
        RecordReceipt(receivedAtUtc, userName, _items.ToDictionary(x => x.Id, x => x.PendingQuantity));

    public void ReverseReceipt(PurchaseReceipt receipt, DateTimeOffset reversedAtUtc, string userName)
    {
        if (receipt.PurchaseOrderId != Id || Status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Cancelled)
            throw new DomainRuleException("La recepción no puede revertirse en esta orden.");
        var quantities = receipt.Items.Where(x => x.PurchaseOrderItemId.HasValue)
            .ToDictionary(x => x.PurchaseOrderItemId!.Value, x => x.ReceivedQuantity);
        if (quantities.Any(x => _items.All(item => item.Id != x.Key) || _items.Single(item => item.Id == x.Key).ReceivedQuantity < x.Value))
            throw new DomainRuleException("La recepción no coincide con los saldos recibidos de la orden.");
        foreach (var item in _items)
            item.ReverseReceipt(quantities.GetValueOrDefault(item.Id));
        ModifiedAtUtc = reversedAtUtc;
        ModifiedBy = userName;
        Status = _items.All(x => x.PendingQuantity == 0) ? PurchaseOrderStatus.Received
            : _items.Any(x => x.ReceivedQuantity > 0) ? PurchaseOrderStatus.PartiallyReceived : PurchaseOrderStatus.Requested;
        if (Status != PurchaseOrderStatus.Received)
        {
            ReceivedAtUtc = null;
            ReceivedBy = null;
        }
    }

    public void Cancel()
    {
        if (_receipts.Count > 0 || Status is PurchaseOrderStatus.PartiallyReceived or PurchaseOrderStatus.Received or PurchaseOrderStatus.Cancelled)
            throw new DomainRuleException("La orden no puede cancelarse en su estado actual.");
        Status = PurchaseOrderStatus.Cancelled;
    }

    private void EnsureEditable()
    {
        if (_receipts.Count > 0 || Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Requested))
            throw new DomainRuleException("La orden no puede editarse en su estado actual.");
    }

    private static List<PurchaseOrderItem> ValidateItems(IEnumerable<PurchaseOrderItem> items)
    {
        var values = items.ToList();
        if (values.Count == 0) throw new DomainRuleException("La orden debe incluir al menos un producto.");
        if (values.GroupBy(x => x.ProductId).Any(x => x.Count() > 1))
            throw new DomainRuleException("El producto ya forma parte de la orden.");
        return values;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
