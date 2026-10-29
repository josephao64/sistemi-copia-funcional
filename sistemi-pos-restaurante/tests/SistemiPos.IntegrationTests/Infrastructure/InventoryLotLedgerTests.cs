using Microsoft.EntityFrameworkCore;
using Npgsql;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Inventory;
using SistemiPos.Infrastructure.Persistence;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Purchases;
using SistemiPos.Infrastructure.Purchases;
using SistemiPos.Domain.Common;
using SistemiPos.Domain.Purchases;
using SistemiPos.Domain.Suppliers;

namespace SistemiPos.IntegrationTests.Infrastructure;

public sealed class InventoryLotLedgerTests
{
    [PostgresFact]
    public async Task Ledger_Persists_Fifo_Trace_Returns_And_Rejects_Concurrent_Stock_Updates()
    {
        var source = Environment.GetEnvironmentVariable("SISTEMI_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(source), "Configure SISTEMI_TEST_CONNECTION para esta prueba PostgreSQL aislada.");
        var adminBuilder = new NpgsqlConnectionStringBuilder(source) { Database = "postgres", Pooling = false };
        var databaseName = "sistemi_lot_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(adminBuilder.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin))
            await create.ExecuteNonQueryAsync();
        var testBuilder = new NpgsqlConnectionStringBuilder(source) { Database = databaseName, Pooling = false };
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(testBuilder.ConnectionString).Options;
        try
        {
            Guid productId;
            await using (var db = new AppDbContext(options))
            {
                await db.Database.MigrateAsync();
                var category = new Category("Lotes", null);
                var product = new Product("LOT-1", null, "Producto", null, category.Id, "Marca", "g", 0, 0, 1, 0, false);
                productId = product.Id;
                var stock = new InventoryStock(productId);
                db.Categories.Add(category); db.Products.Add(product); db.InventoryStocks.Add(stock);
                await db.SaveChangesAsync();
                stock.ReplaceForSystem(10);
                var firstEntry = new InventoryMovement(productId, InventoryMovementType.PurchaseEntry,
                    10, 0, 10, DateTimeOffset.UtcNow.AddDays(-2), "test", "C1", null);
                firstEntry.SetLotDetails(null, "PROV-01");
                db.InventoryMovements.Add(firstEntry);
                await db.SaveChangesAsync();
                stock.ReplaceForSystem(30);
                var secondEntry = new InventoryMovement(productId, InventoryMovementType.PurchaseEntry,
                    20, 10, 30, DateTimeOffset.UtcNow.AddDays(-1), "test", "C2", null);
                secondEntry.SetLotDetails(null, "PROV-02");
                db.InventoryMovements.Add(secondEntry);
                await db.SaveChangesAsync();
            }
            await using (var first = new AppDbContext(options))
            await using (var stale = new AppDbContext(options))
            {
                var stock1 = await first.InventoryStocks.SingleAsync();
                var stock2 = await stale.InventoryStocks.SingleAsync();
                stock1.ReplaceForSystem(15); stock2.ReplaceForSystem(29);
                first.InventoryMovements.Add(new InventoryMovement(productId, InventoryMovementType.SaleExit,
                    15, 30, 15, DateTimeOffset.UtcNow, "test", "V1", null));
                stale.InventoryMovements.Add(new InventoryMovement(productId, InventoryMovementType.SaleExit,
                    1, 30, 29, DateTimeOffset.UtcNow, "test", "V2", null));
                await first.SaveChangesAsync();
                await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
            }
            await using (var db = new AppDbContext(options))
            {
                Assert.Equal(2, await db.InventoryExpiryLots.CountAsync());
                var lots = await db.InventoryExpiryLots.OrderBy(x => x.ReceivedAtUtc).ToListAsync();
                Assert.Equal(new[] { "PROV-01", "PROV-02" }, lots.Select(x => x.LotNumber));
                Assert.Equal(new decimal?[] { 10, 20 }, lots.Select(x => x.ReceivedQuantity));
                Assert.Equal(new[] { 0m, 15m }, lots.Select(x => x.AvailableQuantity));
                Assert.All(lots, x => Assert.NotNull(x.ReceivedAtUtc));
                var quantities = await db.InventoryMovementLots.Where(x => x.Quantity < 0).Select(x => x.Quantity).ToListAsync();
                Assert.Equal(new[] { -10m, -5m }, quantities.OrderBy(x => x));
                var stock = await db.InventoryStocks.SingleAsync();
                Assert.Equal(15, stock.CurrentStock);
                stock.ReplaceForSystem(30);
                db.InventoryMovements.Add(new InventoryMovement(productId, InventoryMovementType.ReturnEntry,
                    15, 15, 30, DateTimeOffset.UtcNow, "test", "V1", null));
                await db.SaveChangesAsync();
                Assert.Equal(30, await db.InventoryExpiryLots.SumAsync(x => x.AvailableQuantity));
                Assert.Equal(2, await db.InventoryMovementLots.CountAsync(x => x.ReversesAllocationId != null));
            }
            await using (var db = new AppDbContext(options))
            {
                var product = await db.Products.SingleAsync();
                product.ConfigureExpiry(true, 10);
                var stock = await db.InventoryStocks.SingleAsync();
                stock.ReplaceForSystem(31);
                db.InventoryMovements.Add(new InventoryMovement(productId, InventoryMovementType.PurchaseEntry,
                    1, 30, 31, DateTimeOffset.UtcNow, "test", "BAD", null));
                await Assert.ThrowsAsync<DomainRuleException>(() => db.SaveChangesAsync());
            }
            await using (var db = new AppDbContext(options))
                Assert.Equal(30, (await db.InventoryStocks.SingleAsync()).CurrentStock);

            Guid untrackedId;
            await using (var db = new AppDbContext(options))
            {
                var category = await db.Categories.SingleAsync();
                var product = new Product("NO-LOT", null, "Sin lotes", null, category.Id, "Marca", "u", 0, 0, 1, 5, false);
                product.ConfigureLotTracking(false);
                untrackedId = product.Id;
                var stock = new InventoryStock(untrackedId);
                db.Products.Add(product);
                db.InventoryStocks.Add(stock);
                await db.SaveChangesAsync();
                stock.ReplaceForSystem(10);
                db.InventoryMovements.Add(new InventoryMovement(untrackedId, InventoryMovementType.PurchaseEntry,
                    10, 0, 10, DateTimeOffset.UtcNow, "test", "SIN-LOTE-C", null));
                await db.SaveChangesAsync();
            }
            await using (var db = new AppDbContext(options))
            {
                Assert.False((await db.Products.SingleAsync(x => x.Id == untrackedId)).ManagesLots);
                var stock = await db.InventoryStocks.SingleAsync(x => x.ProductId == untrackedId);
                stock.ReplaceForSystem(6);
                db.InventoryMovements.Add(new InventoryMovement(untrackedId, InventoryMovementType.SaleExit,
                    4, 10, 6, DateTimeOffset.UtcNow, "test", "SIN-LOTE-V", null));
                await db.SaveChangesAsync();
                stock.ReplaceForSystem(10);
                db.InventoryMovements.Add(new InventoryMovement(untrackedId, InventoryMovementType.ReturnEntry,
                    4, 6, 10, DateTimeOffset.UtcNow, "test", "SIN-LOTE-V", null));
                await db.SaveChangesAsync();
                Assert.Equal(10, stock.CurrentStock);
                Assert.Equal(3, await db.InventoryMovements.CountAsync(x => x.ProductId == untrackedId));
                Assert.False(await db.InventoryExpiryLots.AnyAsync(x => x.ProductId == untrackedId));
                Assert.False(await db.InventoryMovementLots.AnyAsync(x => x.Movement.ProductId == untrackedId));
            }
            await VerifyPurchaseReversalAsync(options, productId);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task VerifyPurchaseReversalAsync(DbContextOptions<AppDbContext> options, Guid productId)
    {
        Guid supplierId;
        Guid purchaseId;
        PurchaseService Service(AppDbContext db) => new(new PurchaseRepository(db), db, new TestUser(), new TestClock());
        CreateDirectPurchaseRequest Request(string invoice) => new(supplierId, DateTimeOffset.UtcNow,
            invoice, "123", PurchaseTermsType.Credit, 30, null,
            [new DirectPurchaseItemRequest(productId, 2, 1, 5, SupplierPackageType.Unit, "Producto", null)]);
        await using (var db = new AppDbContext(options))
        {
            var supplier = new Supplier("Proveedor reversión", "123", null, null, null, null, null);
            supplierId = supplier.Id;
            db.Suppliers.Add(supplier);
            await db.SaveChangesAsync();
            purchaseId = await Service(db).CreateDirectPurchaseAsync(Request("REV-1"));
        }
        await using (var db = new AppDbContext(options))
            await Service(db).CancelPurchaseAsync(purchaseId, "Factura incorrecta");
        await using (var db = new AppDbContext(options))
        {
            var purchase = await db.Purchases.SingleAsync(x => x.Id == purchaseId);
            Assert.Equal(DocumentStatus.Cancelled, purchase.Status);
            Assert.Equal("Factura incorrecta", purchase.CancellationReason);
            Assert.Equal(30, (await db.InventoryStocks.SingleAsync(x => x.ProductId == productId)).CurrentStock);
            var payable = await db.PayableAccounts.SingleAsync(x => x.PurchaseId == purchaseId);
            Assert.Equal(SistemiPos.Domain.Finance.AccountStatus.Cancelled, payable.Status);
            Assert.Equal(0, payable.Balance);
            var reversal = await db.InventoryMovementLots.Include(x => x.Movement).SingleAsync(x =>
                x.Movement.PurchaseId == purchaseId && x.Movement.Type == InventoryMovementType.PurchaseReversal);
            Assert.Equal(-2, reversal.Quantity);
            Assert.NotNull(reversal.ReversesAllocationId);
            Assert.Equal(0, (await db.InventoryExpiryLots.SingleAsync(x => x.Id == reversal.LotId)).AvailableQuantity);
            db.ChangeTracker.CascadeDeleteTiming = Microsoft.EntityFrameworkCore.ChangeTracking.CascadeTiming.OnSaveChanges;
            db.Purchases.Remove(purchase);
            await Assert.ThrowsAsync<DomainRuleException>(() => db.SaveChangesAsync());
        }
        // The database constraint also protects against requests that passed the pre-check concurrently.
        await using (var db = new AppDbContext(options))
        {
            var duplicate = new Purchase("C-DUP", supplierId, DateTimeOffset.UtcNow, "test",
                [new PurchaseItem(productId, 1, 1)], invoiceNumber: "REV-1");
            duplicate.Confirm();
            db.Purchases.Add(duplicate);
            await Assert.ThrowsAsync<DomainRuleException>(() => db.SaveChangesAsync());
        }
        Guid consumedPurchaseId;
        await using (var db = new AppDbContext(options))
            consumedPurchaseId = await Service(db).CreateDirectPurchaseAsync(Request("REV-2"));
        await using (var db = new AppDbContext(options))
        {
            var allocation = await db.InventoryMovementLots.Include(x => x.Movement).SingleAsync(x => x.Movement.PurchaseId == consumedPurchaseId);
            var stock = await db.InventoryStocks.SingleAsync(x => x.ProductId == productId);
            stock.ReplaceForSystem(31);
            var movement = new InventoryMovement(productId, InventoryMovementType.NegativeAdjustment,
                1, 32, 31, DateTimeOffset.UtcNow, "test", "BAJA", "Consumido");
            movement.SetLotDetails(null, selectedLotId: allocation.LotId);
            db.InventoryMovements.Add(movement);
            await db.SaveChangesAsync();
        }
        await using (var db = new AppDbContext(options))
            await Assert.ThrowsAsync<DomainRuleException>(() => Service(db).CancelPurchaseAsync(consumedPurchaseId, "Error"));
        await using (var db = new AppDbContext(options))
        {
            Assert.Equal(DocumentStatus.Confirmed, (await db.Purchases.SingleAsync(x => x.Id == consumedPurchaseId)).Status);
            Assert.Equal(31, (await db.InventoryStocks.SingleAsync(x => x.ProductId == productId)).CurrentStock);
            Assert.Equal(10, (await db.PayableAccounts.SingleAsync(x => x.PurchaseId == consumedPurchaseId)).Balance);
            Assert.False(await db.InventoryMovements.AnyAsync(x => x.PurchaseId == consumedPurchaseId
                && x.Type == InventoryMovementType.PurchaseReversal));
        }
    }

    private sealed class TestClock : IDateTimeProvider { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
    private sealed class TestUser : ICurrentUserAccessor
    {
        public Guid? UserId => null;
        public string UserName => "test";
        public bool IsAuthenticated => true;
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SISTEMI_TEST_CONNECTION")))
            Skip = "Requiere SISTEMI_TEST_CONNECTION; crea y elimina una base PostgreSQL temporal.";
    }
}
