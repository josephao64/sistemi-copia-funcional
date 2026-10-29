using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Inventory;

namespace SistemiPos.UnitTests.Inventory;

public sealed class InventoryLotAllocationTests
{
    private static readonly DateTimeOffset Arrival = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 22);

    [Fact]
    public void Fefo_Consumes_Nearest_Expiry_Even_When_Received_After_Other_Lots()
    {
        var id = Guid.NewGuid();
        var laterExpiry = new InventoryExpiryLot(id, Today.AddDays(20), 20, "C1", Arrival);
        var nearestExpiry = new InventoryExpiryLot(id, Today.AddDays(2), 5, "C2", Arrival.AddHours(1));
        var plan = InventoryLotAllocation.Plan([laterExpiry, nearestExpiry], 8, true, Today);
        Assert.Equal(nearestExpiry.Id, plan[0].Lot.Id);
        Assert.Equal(5, plan[0].Quantity);
        Assert.Equal(laterExpiry.Id, plan[1].Lot.Id);
        Assert.Equal(3, plan[1].Quantity);
    }

    [Fact]
    public void Fifo_Spans_Lots_In_Receipt_Order()
    {
        var id = Guid.NewGuid();
        var first = new InventoryExpiryLot(id, null, 5, "C1", Arrival);
        var second = new InventoryExpiryLot(id, null, 10, "C2", Arrival.AddHours(1));
        var plan = InventoryLotAllocation.Plan([second, first], 8, false, Today);
        Assert.Equal(first.Id, plan[0].Lot.Id);
        Assert.Equal(5, plan[0].Quantity);
        Assert.Equal(second.Id, plan[1].Lot.Id);
        Assert.Equal(3, plan[1].Quantity);
        Assert.Equal(5, first.AvailableQuantity);
    }

    [Fact]
    public void Fefo_Uses_Fifo_For_Equal_Expiry_And_Excludes_Expired_Lots()
    {
        var id = Guid.NewGuid();
        var expired = new InventoryExpiryLot(id, Today.AddDays(-1), 100, "C0", Arrival.AddDays(-5));
        var first = new InventoryExpiryLot(id, Today, 5, "C1", Arrival);
        var second = new InventoryExpiryLot(id, Today, 10, "C2", Arrival.AddHours(1));
        var plan = InventoryLotAllocation.Plan([second, expired, first], 8, true, Today);
        Assert.Equal(first.Id, plan[0].Lot.Id);
        Assert.DoesNotContain(plan, x => x.Lot.Id == expired.Id);
    }

    [Fact]
    public void Expired_Lot_Requires_Explicit_Selection_For_Disposal()
    {
        var lot = new InventoryExpiryLot(Guid.NewGuid(), Today.AddDays(-1), 10, "C0", Arrival);
        Assert.Throws<DomainRuleException>(() => InventoryLotAllocation.Plan([lot], 1, true, Today));
        var plan = InventoryLotAllocation.Plan([lot], 1, true, Today, lot.Id, allowExpired: true);
        Assert.Equal(lot.Id, Assert.Single(plan).Lot.Id);
    }

    [Fact]
    public void Return_Cannot_Exceed_Original_Quantity()
    {
        var lot = new InventoryExpiryLot(Guid.NewGuid(), null, 10, "C0", Arrival);
        lot.Consume(3);
        lot.Restore(3);
        Assert.Equal(10, lot.AvailableQuantity);
        Assert.Throws<DomainRuleException>(() => lot.Restore(1));
    }
}
