namespace SistemiPos.Domain.Inventory;

public static class ExpiryLotConsumption
{
    public static void Consume(IEnumerable<InventoryExpiryLot> lots, decimal quantity, DateOnly today)
    {
        foreach (var allocation in InventoryLotAllocation.Plan(lots, quantity, true, today))
            allocation.Lot.Consume(allocation.Quantity);
    }
}
