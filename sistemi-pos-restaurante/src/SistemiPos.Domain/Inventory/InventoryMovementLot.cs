using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Inventory;

public sealed class InventoryMovementLot : Entity
{
    public Guid MovementId { get; private set; }
    public InventoryMovement Movement { get; set; } = null!;
    public Guid LotId { get; private set; }
    public InventoryExpiryLot Lot { get; set; } = null!;
    public decimal Quantity { get; private set; }
    public Guid? ReversesAllocationId { get; private set; }

    private InventoryMovementLot() { }

    public InventoryMovementLot(InventoryMovement movement, InventoryExpiryLot lot, decimal quantity,
        Guid? reversesAllocationId = null)
    {
        if (quantity == 0 || movement.ProductId != lot.ProductId || movement.WarehouseId != lot.WarehouseId)
            throw new DomainRuleException("La asignación de lote no corresponde al movimiento.");
        Movement = movement; MovementId = movement.Id; Lot = lot; LotId = lot.Id;
        Quantity = quantity; ReversesAllocationId = reversesAllocationId;
    }
}
