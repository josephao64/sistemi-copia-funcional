using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Inventory;

namespace SistemiPos.UnitTests.Inventory;

public sealed class PurchaseLotReversalTests
{
    [Fact]
    public void Reversal_Uses_Original_Purchase_Lot_Even_When_Expired()
    {
        var product = Guid.NewGuid();
        var purchase = Guid.NewGuid();
        var warehouse = Guid.NewGuid();
        var lot = new InventoryExpiryLot(product, new DateOnly(2026, 1, 1), 3, "C1", warehouseId: warehouse);
        var entry = new InventoryMovement(product, InventoryMovementType.PurchaseEntry, 3, 0, 3,
            DateTimeOffset.UtcNow, "tester", "C1", null, purchaseId: purchase, warehouseId: warehouse);
        var allocation = new InventoryMovementLot(entry, lot, 3);
        var reversal = new InventoryMovement(product, InventoryMovementType.PurchaseReversal, 3, 9, 6,
            DateTimeOffset.UtcNow, "tester", "C1", "Error", purchaseId: purchase, warehouseId: warehouse);
        Assert.Same(allocation, Assert.Single(PurchaseLotReversal.Plan(reversal, [allocation], [])));
        Assert.Equal(3, lot.AvailableQuantity);
    }

    [Theory]
    [InlineData("consumed")]
    [InlineData("reversed")]
    [InlineData("missing")]
    [InlineData("warehouse")]
    [InlineData("other-purchase")]
    public void Reversal_Rejects_Unavailable_Or_Untraceable_Original_Lots(string failure)
    {
        var product = Guid.NewGuid();
        var purchase = Guid.NewGuid();
        var warehouse = Guid.NewGuid();
        var lot = new InventoryExpiryLot(product, null, 3, "C1", warehouseId: warehouse);
        if (failure == "consumed") lot.Consume(1);
        var entry = new InventoryMovement(product, InventoryMovementType.PurchaseEntry, 3, 0, 3,
            DateTimeOffset.UtcNow, "tester", "C1", null, purchaseId: purchase, warehouseId: warehouse);
        var allocation = new InventoryMovementLot(entry, lot, 3);
        var reversal = new InventoryMovement(product, InventoryMovementType.PurchaseReversal, 3, 9, 6,
            DateTimeOffset.UtcNow, "tester", "C1", "Error",
            purchaseId: failure == "other-purchase" ? Guid.NewGuid() : purchase,
            warehouseId: failure == "warehouse" ? Guid.NewGuid() : warehouse);
        var balance = lot.AvailableQuantity;
        Assert.Throws<DomainRuleException>(() => PurchaseLotReversal.Plan(reversal,
            failure == "missing" ? [] : [allocation], failure == "reversed" ? [allocation.Id] : []));
        Assert.Equal(balance, lot.AvailableQuantity);
    }
}
