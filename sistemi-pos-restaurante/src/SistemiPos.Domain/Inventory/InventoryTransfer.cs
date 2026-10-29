using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Organizations;

namespace SistemiPos.Domain.Inventory;

public sealed class InventoryTransfer : AuditableEntity
{
    private readonly List<InventoryTransferItem> _items = [];

    public string Number { get; private set; } = string.Empty;
    public Guid OriginWarehouseId { get; private set; }
    public Warehouse OriginWarehouse { get; set; } = null!;
    public Guid DestinationWarehouseId { get; private set; }
    public Warehouse DestinationWarehouse { get; set; } = null!;
    public InventoryTransferStatus Status { get; private set; } = InventoryTransferStatus.Draft;
    public DateTimeOffset RequestedAtUtc { get; private set; }
    public string RequestedBy { get; private set; } = string.Empty;
    public DateTimeOffset? DispatchedAtUtc { get; private set; }
    public string? DispatchedBy { get; private set; }
    public DateTimeOffset? ReceivedAtUtc { get; private set; }
    public string? ReceivedBy { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public string? CancelledBy { get; private set; }
    public string? CancellationReason { get; private set; }
    public string? Observations { get; private set; }
    public uint Version { get; private set; }

    public IReadOnlyCollection<InventoryTransferItem> Items => _items;

    private InventoryTransfer()
    {
    }

    public InventoryTransfer(
        string number,
        Guid originWarehouseId,
        Guid destinationWarehouseId,
        DateTimeOffset requestedAtUtc,
        string requestedBy,
        IEnumerable<InventoryTransferItem> items,
        string? observations = null)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            throw new DomainRuleException("El número de traslado es requerido.");
        }

        if (originWarehouseId == Guid.Empty || destinationWarehouseId == Guid.Empty)
        {
            throw new DomainRuleException("Las bodegas de origen y destino son requeridas.");
        }

        if (originWarehouseId == destinationWarehouseId)
        {
            throw new DomainRuleException("La bodega de origen y destino no pueden ser la misma.");
        }

        var itemList = items?.ToList() ?? [];
        if (itemList.Count == 0)
        {
            throw new DomainRuleException("El traslado debe incluir al menos un producto.");
        }

        if (itemList.GroupBy(x => x.ProductId).Any(g => g.Count() > 1))
        {
            throw new DomainRuleException("Un producto no puede repetirse en las líneas del traslado.");
        }

        Number = number.Trim().ToUpperInvariant();
        OriginWarehouseId = originWarehouseId;
        DestinationWarehouseId = destinationWarehouseId;
        RequestedAtUtc = requestedAtUtc;
        RequestedBy = string.IsNullOrWhiteSpace(requestedBy) ? "system" : requestedBy.Trim();
        Observations = string.IsNullOrWhiteSpace(observations) ? null : observations.Trim();
        Status = InventoryTransferStatus.Draft;

        foreach (var item in itemList)
        {
            item.TransferId = Id;
            _items.Add(item);
        }
    }

    public void Dispatch(string dispatchedBy, DateTimeOffset dispatchedAtUtc)
    {
        if (Status != InventoryTransferStatus.Draft)
        {
            throw new DomainRuleException("Solo un traslado en borrador puede ser despachado.");
        }

        if (string.IsNullOrWhiteSpace(dispatchedBy))
        {
            throw new DomainRuleException("El usuario que despacha es requerido.");
        }

        Status = InventoryTransferStatus.Dispatched;
        DispatchedBy = dispatchedBy.Trim();
        DispatchedAtUtc = dispatchedAtUtc;
    }

    public void Receive(string receivedBy, DateTimeOffset receivedAtUtc, IReadOnlyDictionary<Guid, decimal>? receivedQuantities = null)
    {
        if (Status != InventoryTransferStatus.Dispatched)
        {
            throw new DomainRuleException("Solo un traslado despachado puede ser recibido.");
        }

        if (string.IsNullOrWhiteSpace(receivedBy))
        {
            throw new DomainRuleException("El usuario que recibe es requerido.");
        }

        foreach (var item in _items)
        {
            var quantity = receivedQuantities != null && receivedQuantities.TryGetValue(item.Id, out var qty)
                ? qty
                : item.QuantitySent;

            item.RegisterReceived(quantity);
        }

        Status = InventoryTransferStatus.Received;
        ReceivedBy = receivedBy.Trim();
        ReceivedAtUtc = receivedAtUtc;
    }

    public void Cancel(string cancelledBy, DateTimeOffset cancelledAtUtc, string? reason)
    {
        if (Status != InventoryTransferStatus.Draft)
        {
            throw new DomainRuleException("Solo un traslado en borrador puede ser cancelado.");
        }

        Status = InventoryTransferStatus.Cancelled;
        CancelledBy = string.IsNullOrWhiteSpace(cancelledBy) ? "system" : cancelledBy.Trim();
        CancelledAtUtc = cancelledAtUtc;
        CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }
}
