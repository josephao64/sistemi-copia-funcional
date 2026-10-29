using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Inventory;

namespace SistemiPos.UnitTests.Inventory;

public sealed class ExpiryLotConsumptionTests
{
    [Fact]
    public void Consumes_Nearest_Expiry_Across_Lots_Without_Using_Expired_Stock()
    {
        var today = new DateOnly(2026, 9, 22);
        var productId = Guid.NewGuid();
        var expired = new InventoryExpiryLot(productId, today.AddDays(-1), 100, "Old");
        var first = new InventoryExpiryLot(productId, today, 5, "First");
        var second = new InventoryExpiryLot(productId, today.AddDays(10), 10, "Second");

        ExpiryLotConsumption.Consume([second, expired, first], 8, today);

        Assert.Equal(100, expired.AvailableQuantity);
        Assert.Equal(0, first.AvailableQuantity);
        Assert.Equal(7, second.AvailableQuantity);
    }

    [Fact]
    public void Insufficient_Valid_Stock_Does_Not_Partially_Consume_Lots()
    {
        var today = new DateOnly(2026, 9, 22);
        var lot = new InventoryExpiryLot(Guid.NewGuid(), today, 5, "Purchase");
        Assert.Throws<DomainRuleException>(() => ExpiryLotConsumption.Consume([lot], 6, today));
        Assert.Equal(5, lot.AvailableQuantity);
    }
}
