using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Organizations;

namespace SistemiPos.Domain.Inventory;

public sealed class InventoryMovement : AuditableEntity
{
    public Guid ProductId { get; private set; }
    public Product Product { get; set; } = null!;
    public Guid? WarehouseId { get; private set; }
    public Warehouse? Warehouse { get; set; }
    public InventoryMovementType Type { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal PreviousStock { get; private set; }
    public decimal NewStock { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string? SourceDocument { get; private set; }
    public string? Note { get; private set; }
    public Guid? PhysicalInventoryCountId { get; private set; }
    public PhysicalInventoryCount? PhysicalInventoryCount { get; set; }
    public Guid? PhysicalInventoryCountItemId { get; private set; }
    public PhysicalInventoryCountItem? PhysicalInventoryCountItem { get; set; }
    public Guid? PurchaseOrderId { get; private set; }
    public Guid? PurchaseId { get; private set; }
    public Guid? TransferId { get; private set; }
    public InventoryTransfer? Transfer { get; set; }
    public Guid? TransferItemId { get; private set; }
    public InventoryTransferItem? TransferItem { get; set; }
    public decimal? UnitCost { get; private set; }
    public Guid? RestaurantOrderEventId { get; private set; }
    public Guid? OrderReturnId { get; private set; }
    public void SetOrderReturn(Guid orderId) => OrderReturnId = orderId;

    public void LinkOrderEvent(Guid eventId)
    {
        if (eventId == Guid.Empty || RestaurantOrderEventId.HasValue || Type != InventoryMovementType.PosOrderConsumption)
            throw new DomainRuleException("El movimiento de consumo requiere un evento de pedido válido.");
        RestaurantOrderEventId = eventId;
    }

    public DateOnly? EntryExpiryDate { get; private set; }
    public string? EntryLotNumber { get; private set; }
    public Guid? SelectedLotId { get; private set; }
    public Guid? OriginalMovementId { get; private set; }

    public void SetOrderReturnOrigin(Guid movementId)
    {
        if (Type != InventoryMovementType.OrderReturn || movementId == Guid.Empty)
            throw new DomainRuleException("La devolución requiere un consumo original de pedido.");
        OriginalMovementId = movementId;
    }

    public void SetLotDetails(DateOnly? expiryDate, string? lotNumber = null, Guid? selectedLotId = null)
    {
        EntryExpiryDate = expiryDate;
        EntryLotNumber = lotNumber;
        SelectedLotId = selectedLotId;
    }

    private InventoryMovement()
    {
    }

    public InventoryMovement(
        Guid productId,
        InventoryMovementType type,
        decimal quantity,
        decimal previousStock,
        decimal newStock,
        DateTimeOffset occurredAtUtc,
        string userName,
        string? sourceDocument,
        string? note,
        Guid? physicalInventoryCountId = null,
        Guid? physicalInventoryCountItemId = null,
        Guid? purchaseOrderId = null,
        Guid? purchaseId = null,
        Guid? warehouseId = null,
        Guid? transferId = null,
        Guid? transferItemId = null,
        decimal? unitCost = null)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainRuleException("El producto es requerido.");
        }

        if (quantity <= 0)
        {
            throw new DomainRuleException("La cantidad del movimiento debe ser mayor a cero.");
        }

        if (previousStock < 0 || newStock < 0)
        {
            throw new DomainRuleException("La existencia del movimiento no puede ser negativa.");
        }

        ProductId = productId;
        Type = type;
        Quantity = decimal.Round(quantity, 6);
        PreviousStock = decimal.Round(previousStock, 6);
        NewStock = decimal.Round(newStock, 6);
        OccurredAtUtc = occurredAtUtc;
        UserName = string.IsNullOrWhiteSpace(userName) ? "system" : userName.Trim();
        SourceDocument = string.IsNullOrWhiteSpace(sourceDocument) ? null : sourceDocument.Trim();
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        PhysicalInventoryCountId = physicalInventoryCountId;
        PhysicalInventoryCountItemId = physicalInventoryCountItemId;
        PurchaseOrderId = purchaseOrderId;
        PurchaseId = purchaseId;
        WarehouseId = warehouseId;
        TransferId = transferId;
        TransferItemId = transferItemId;
        UnitCost = unitCost.HasValue ? decimal.Round(unitCost.Value, 6) : null;
    }

    public void AssignWarehouse(Guid warehouseId)
    {
        WarehouseId = warehouseId;
    }

    public void SetTransferDetails(Guid transferId, Guid transferItemId, decimal? unitCost = null)
    {
        TransferId = transferId;
        TransferItemId = transferItemId;
        if (unitCost.HasValue)
        {
            UnitCost = decimal.Round(unitCost.Value, 6);
        }
    }
}
