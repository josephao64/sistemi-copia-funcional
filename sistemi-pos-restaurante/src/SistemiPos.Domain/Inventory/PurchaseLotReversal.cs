using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Inventory;

public static class PurchaseLotReversal
{
    public static IReadOnlyList<InventoryMovementLot> Plan(InventoryMovement movement,
        IEnumerable<InventoryMovementLot> original, IEnumerable<Guid> reversedAllocationIds)
    {
        var allocations = original.Where(x => x.Quantity > 0 && x.Movement.Type == InventoryMovementType.PurchaseEntry
            && x.Movement.PurchaseId == movement.PurchaseId && x.Movement.ProductId == movement.ProductId
            && x.Movement.WarehouseId == movement.WarehouseId).ToList();
        var reversed = reversedAllocationIds.ToHashSet();
        if (!movement.PurchaseId.HasValue || allocations.Sum(x => x.Quantity) != movement.Quantity
            || allocations.Any(x => reversed.Contains(x.Id)))
            throw new DomainRuleException("No se pueden revertir los lotes: falta trazabilidad de la compra o ya fue revertida. Requiere conciliación.");
        if (allocations.GroupBy(x => x.LotId).Any(x => x.Sum(y => y.Quantity) > x.First().Lot.AvailableQuantity))
            throw new DomainRuleException("No se puede anular la compra: los lotes originales ya fueron consumidos o trasladados.");
        return allocations;
    }
}
