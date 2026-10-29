using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Inventory;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Inventory;

namespace SistemiPos.UnitTests.Inventory;

public sealed class InventoryExpiryAlertTests
{
    private static readonly DateOnly Today = new(2026, 10, 9);
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 5, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(-1, "Caducado")]
    [InlineData(0, "Caduca hoy")]
    [InlineData(3, "Próximo a caducar")]
    [InlineData(4, null)]
    public async Task Alerts_Respect_Local_Date_And_Inclusive_Product_Threshold(int days, string? status)
    {
        var product = CreateProduct(3);
        var lot = new InventoryExpiryLot(product.Id, Today.AddDays(days), 2, "C1", Now, lotNumber: "LOTE-01") { Product = product };
        var service = new InventoryQueryService(new FakeRepository([lot]), new FixedClock());
        var alerts = await service.GetExpiryLotsAsync();
        if (status is null) Assert.Empty(alerts);
        else
        {
            var alert = Assert.Single(alerts);
            Assert.Equal(status, alert.Status);
            Assert.Equal(days, alert.DaysRemaining);
            Assert.Equal(lot.Id, alert.LotId);
            Assert.Equal("LOTE-01", alert.LotNumber);
        }
    }

    [Fact]
    public async Task Alerts_Identify_Separate_Lots_And_Exclude_Empty_Or_Out_Of_Range_Lots()
    {
        var product = CreateProduct(3);
        var other = CreateProduct(1);
        var warehouseId = Guid.NewGuid();
        var first = new InventoryExpiryLot(product.Id, Today.AddDays(2), 2, "C1", Now, warehouseId, lotNumber: "L1") { Product = product };
        var second = new InventoryExpiryLot(product.Id, Today.AddDays(2), 3, "C2", Now.AddHours(1), warehouseId, lotNumber: "L2") { Product = product };
        var empty = new InventoryExpiryLot(product.Id, Today, 1, "C3", Now) { Product = product };
        empty.Consume(1);
        var outOfRange = new InventoryExpiryLot(other.Id, Today.AddDays(2), 1, "C4", Now) { Product = other };
        var service = new InventoryQueryService(new FakeRepository([second, outOfRange, empty, first]), new FixedClock());
        var alerts = await service.GetExpiryLotsAsync(warehouseId);
        Assert.Equal(new[] { "L1", "L2" }, alerts.Select(x => x.LotNumber));
        Assert.Equal(new[] { 2m, 3m }, alerts.Select(x => x.AvailableQuantity));
        Assert.All(alerts, x => Assert.Equal("Bodega principal", x.WarehouseName));
        alerts = await service.GetExpiryLotsAsync();
        Assert.Equal(2, alerts.Count);
    }

    private static Product CreateProduct(int alertDays)
    {
        var category = new Category("Alimentos", null);
        var product = new Product("P-01", null, "Producto", null, category.Id, "Marca", "u", 0, 0, 1, 0, false) { Category = category };
        product.ConfigureExpiry(true, alertDays);
        return product;
    }

    private sealed class FixedClock : IDateTimeProvider { public DateTimeOffset UtcNow => Now; }

    private sealed class FakeRepository(List<InventoryExpiryLot> lots) : IInventoryRepository
    {
        public Task<List<InventoryExpiryLot>> GetExpiryLotsAsync(Guid? productId, Guid? warehouseId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(lots.Where(x => (!productId.HasValue || x.ProductId == productId) && (!warehouseId.HasValue || x.WarehouseId == warehouseId)).ToList());
        public Task<IReadOnlyDictionary<Guid, string>> GetWarehouseNamesAsync(IReadOnlyCollection<Guid> warehouseIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(warehouseIds.ToDictionary(x => x, _ => "Bodega principal"));
        public Task<InventoryMovementPage> GetMovementPageAsync(Guid? productId, int page, int pageSize, Guid? warehouseId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<InventoryMovementDto>> GetMovementsForExportAsync(Guid? productId, Guid? warehouseId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<InventoryStockDto>> GetStocksAsync(bool onlyLowStock, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<InventoryStockDto>> GetStocksAsync(bool onlyLowStock, Guid? branchId, Guid? warehouseId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<InventoryStock?> GetStockAsync(Guid productId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<InventoryStock?> GetStockAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddExpiryLotAsync(InventoryExpiryLot lot, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
