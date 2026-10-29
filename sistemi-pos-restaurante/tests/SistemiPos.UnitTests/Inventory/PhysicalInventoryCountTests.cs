using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Inventory;
using SistemiPos.Domain.Inventory;

namespace SistemiPos.UnitTests.Inventory;

public sealed class PhysicalInventoryCountTests
{
    [Fact]
    public void SaveCount_Preserves_Reference_And_Requires_Recount_When_Stock_Version_Changes()
    {
        var item = new PhysicalInventoryCountItem(Guid.NewGuid(), Guid.NewGuid());
        item.BeginCounting(40m, 1, DateTimeOffset.UtcNow, "counter");

        var saved = item.SaveCount(38, "Faltante pendiente de revisar", 1, DateTimeOffset.UtcNow, "counter");

        Assert.True(saved);
        Assert.Equal(40m, item.ReferenceStock);
        Assert.Equal(38m, item.CountedStock);
        Assert.Equal(-2m, item.Difference);
        Assert.Equal(PhysicalInventoryCountItemStatus.Counted, item.Status);

        item.BeginCounting(37m, 2, DateTimeOffset.UtcNow, "counter");
        saved = item.SaveCount(37, null, 3, DateTimeOffset.UtcNow, "counter");

        Assert.False(saved);
        Assert.Equal(PhysicalInventoryCountItemStatus.RecountRequired, item.Status);
        Assert.Null(item.CountedStock);
        Assert.Null(item.Difference);
    }

    [Fact]
    public async Task FinalizeAsync_Applies_Saved_Difference_To_Current_Operational_Stock()
    {
        var productId = Guid.NewGuid();
        var count = new PhysicalInventoryCount("Inventario General", PhysicalInventoryCountScope.AllInventory);
        count.IncludeProduct(productId);
        var item = Assert.Single(count.Items);
        item.BeginCounting(40m, 0, DateTimeOffset.UtcNow, "counter");
        Assert.True(item.SaveCount(38, "Producto dañado", 0, DateTimeOffset.UtcNow, "counter"));

        var stock = new InventoryStock(productId);
        stock.ReplaceForSystem(37m);
        var countRepository = new FakePhysicalInventoryCountRepository(count, stock);
        var inventoryRepository = new FakeInventoryRepository(stock);
        var service = new PhysicalInventoryCountService(
            countRepository,
            inventoryRepository,
            new FakeUnitOfWork(),
            new FakeCurrentUserAccessor(),
            new FakeDateTimeProvider(new DateTimeOffset(2026, 9, 1, 14, 0, 0, TimeSpan.Zero)));

        await service.FinalizeAsync(count.Id);

        Assert.Equal(35m, stock.CurrentStock);
        Assert.Equal(PhysicalInventoryCountStatus.Finalized, count.Status);
        Assert.Equal(37m, item.OperationalStockBeforeAdjustment);
        Assert.Equal(-2m, item.AppliedAdjustment);
        Assert.Equal(35m, item.OperationalStockAfterAdjustment);
        var movement = Assert.Single(inventoryRepository.Movements);
        Assert.Equal(InventoryMovementType.NegativeAdjustment, movement.Type);
        Assert.Equal(2m, movement.Quantity);
        Assert.Equal(count.Id, movement.PhysicalInventoryCountId);
        Assert.Equal(item.Id, movement.PhysicalInventoryCountItemId);
    }

    private sealed class FakePhysicalInventoryCountRepository(PhysicalInventoryCount count, InventoryStock stock) : IPhysicalInventoryCountRepository
    {
        public Task<List<PhysicalInventoryCount>> GetCountsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<PhysicalInventoryCount> { count });
        public Task<PhysicalInventoryCount?> GetCountAsync(Guid id, bool tracking, CancellationToken cancellationToken = default) => Task.FromResult<PhysicalInventoryCount?>(id == count.Id ? count : null);
        public Task<PhysicalInventoryCountItem?> GetItemAsync(Guid countId, Guid itemId, CancellationToken cancellationToken = default) => Task.FromResult(count.Items.SingleOrDefault(x => x.Id == itemId));
        public Task<List<PhysicalInventoryCategoryOption>> GetCategoryOptionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<PhysicalInventoryCategoryOption>());
        public Task<List<Guid>> GetEligibleProductIdsAsync(PhysicalInventoryCountScope scope, IReadOnlyCollection<Guid> categoryIds, CancellationToken cancellationToken = default) => Task.FromResult(count.Items.Select(x => x.ProductId).ToList());
        public Task<bool> HasActiveParticipationAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<List<InventoryStock>> GetStocksForUpdateAsync(IReadOnlyCollection<Guid> productIds, Guid? warehouseId = null, CancellationToken cancellationToken = default) => Task.FromResult(new List<InventoryStock> { stock });
        public Task AddAsync(PhysicalInventoryCount value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default) => operation(cancellationToken);
    }

    private sealed class FakeInventoryRepository(InventoryStock stock) : IInventoryRepository
    {
        public List<InventoryMovement> Movements { get; } = [];
        public List<InventoryExpiryLot> ExpiryLots { get; } = [];
        public Task<InventoryMovementPage> GetMovementPageAsync(Guid? productId, int page, int pageSize, Guid? warehouseId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new InventoryMovementPage([], 0, 1, pageSize));
        public Task<List<InventoryMovementDto>> GetMovementsForExportAsync(Guid? productId, Guid? warehouseId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<InventoryMovementDto>());
        public Task<List<InventoryStockDto>> GetStocksAsync(bool onlyLowStock, CancellationToken cancellationToken = default) => Task.FromResult(new List<InventoryStockDto>());
        public Task<List<InventoryStockDto>> GetStocksAsync(bool onlyLowStock, Guid? branchId, Guid? warehouseId, CancellationToken cancellationToken = default) => Task.FromResult(new List<InventoryStockDto>());
        public Task<InventoryStock?> GetStockAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<InventoryStock?>(productId == stock.ProductId ? stock : null);
        public Task<InventoryStock?> GetStockAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default) => Task.FromResult<InventoryStock?>(productId == stock.ProductId ? stock : null);
        public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default) { Movements.Add(movement); return Task.CompletedTask; }
        public Task AddExpiryLotAsync(InventoryExpiryLot lot, CancellationToken cancellationToken = default) { ExpiryLots.Add(lot); return Task.CompletedTask; }
        public Task<List<InventoryExpiryLot>> GetExpiryLotsAsync(Guid? productId, Guid? warehouseId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(ExpiryLots.Where(x => !productId.HasValue || x.ProductId == productId.Value).ToList());
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public string UserName => "inventory.user";
    }

    private sealed class FakeDateTimeProvider(DateTimeOffset value) : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => value;
    }
}
