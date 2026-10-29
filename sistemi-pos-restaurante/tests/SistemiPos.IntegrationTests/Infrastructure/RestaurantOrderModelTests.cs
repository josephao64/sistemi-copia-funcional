using Microsoft.EntityFrameworkCore;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Orders;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.IntegrationTests.Infrastructure;

public sealed class RestaurantOrderModelTests
{
    [Fact]
    public void Order_Model_Protects_References_Idempotency_Concurrency_And_Component_Precision()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=ModelOnly;Username=postgres;Password=not-used").Options);
        var order = db.Model.FindEntityType(typeof(RestaurantOrder))!;
        Assert.Equal("PosOrders", order.GetTableName());
        Assert.Equal("RestaurantOrders", db.Model.FindEntityType(typeof(SistemiPos.Domain.Restaurant.RestaurantOrder))!.GetTableName());
        var line = db.Model.FindEntityType(typeof(RestaurantOrderLine))!;
        var entry = db.Model.FindEntityType(typeof(RestaurantOrderEvent))!;
        var movement = db.Model.FindEntityType(typeof(InventoryMovement))!;
        Assert.Equal("xmin", order.FindProperty("Version")!.GetColumnName());
        Assert.True(order.FindProperty("Version")!.IsConcurrencyToken);
        Assert.Equal("jsonb", line.FindProperty("RecipeJson")!.GetColumnType());
        Assert.Equal("jsonb", line.FindProperty("ModifiersJson")!.GetColumnType());
        var sale = db.Model.FindEntityType(typeof(SistemiPos.Domain.Sales.Sale))!;
        Assert.True(sale.FindNavigation("Payments")!.IsCollection);
        Assert.Null(sale.FindNavigation("Payment"));
        Assert.True(sale.FindProperty("Version")!.IsConcurrencyToken);
        var cash = db.Model.FindEntityType(typeof(SistemiPos.Domain.Cash.CashSession))!;
        Assert.True(cash.FindProperty("Version")!.IsConcurrencyToken);
        Assert.Contains(cash.GetIndexes(), x => x.IsUnique && x.GetFilter() == "\"Status\" = 1"
            && x.Properties.Select(p => p.Name).SequenceEqual(["BranchId", "RegisterCode"]));
        Assert.Contains(entry.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(["OrderId", "OperationId"]));
        Assert.Contains(movement.GetForeignKeys(), x => x.Properties.Single().Name == "RestaurantOrderEventId" && x.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Contains(movement.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(["RestaurantOrderEventId", "ProductId"]));
        foreach (var (entity, property) in new[] { (typeof(InventoryStock), "CurrentStock"), (typeof(InventoryMovement), "Quantity"),
            (typeof(InventoryMovement), "PreviousStock"), (typeof(InventoryMovement), "NewStock"),
            (typeof(InventoryExpiryLot), "AvailableQuantity"), (typeof(InventoryExpiryLot), "ReceivedQuantity"), (typeof(InventoryMovementLot), "Quantity") })
            Assert.Equal("numeric(22,6)", db.Model.FindEntityType(entity)!.FindProperty(property)!.GetColumnType());
    }
}
