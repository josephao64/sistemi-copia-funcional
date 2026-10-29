using Microsoft.EntityFrameworkCore;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.IntegrationTests.Infrastructure;

public sealed class AppDbContextModelTests
{
    [Fact]
    public void Optional_Lot_Tracking_Migration_Matches_Current_Model()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=SistemiPosModelTests;Username=postgres;Password=not-used")
            .Options;
        using var context = new AppDbContext(options);
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Contains("20261008120000_AddOptionalProductLotTracking", context.Database.GetMigrations());
    }
    [Fact]
    public void Model_Contains_AuditLog_Entity()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=SistemiPosModelTests;Username=postgres;Password=not-used")
            .Options;

        using var context = new AppDbContext(options);

        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Audit.AuditLog"));
    }

    [Fact]
    public void Model_Contains_Purchasing_And_Inventory_Entities()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=SistemiPosModelTests;Username=postgres;Password=not-used")
            .Options;

        using var context = new AppDbContext(options);

        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Suppliers.Supplier"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Purchases.Purchase"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Purchases.PurchaseItem"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Purchases.PurchaseOrder"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Purchases.PurchaseOrderItem"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Purchases.PurchaseReceipt"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Purchases.PurchaseReceiptItem"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Inventory.InventoryStock"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Inventory.InventoryMovement"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Catalog.ProductPriceRule"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Customers.Customer"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Cash.CashSession"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Cash.CashMovement"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Configuration.SystemSetting"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Configuration.CurrencyDefinition"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Configuration.BankAccount"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Configuration.RoleDiscountSetting"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Configuration.UserDiscountSetting"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Sales.Sale"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Sales.SaleCancellation"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Sales.SaleItem"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Sales.SalePayment"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Sales.SaleRefund"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Sales.SaleReturn"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Sales.SaleReturnItem"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Finance.PettyCashFund"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Finance.PettyCashCycle"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Finance.PettyCashCategory"));
        Assert.NotNull(context.Model.FindEntityType("SistemiPos.Domain.Finance.PettyCashMovement"));
    }

    [Fact]
    public void Model_Defines_Unique_Indexes_For_Document_Integrity()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=SistemiPosModelTests;Username=postgres;Password=not-used")
            .Options;

        using var context = new AppDbContext(options);

        var sale = context.Model.FindEntityType("SistemiPos.Domain.Sales.Sale");
        var cancellation = context.Model.FindEntityType("SistemiPos.Domain.Sales.SaleCancellation");
        var saleReturn = context.Model.FindEntityType("SistemiPos.Domain.Sales.SaleReturn");
        var purchaseOrder = context.Model.FindEntityType("SistemiPos.Domain.Purchases.PurchaseOrder");
        var purchase = context.Model.FindEntityType("SistemiPos.Domain.Purchases.Purchase");
        var purchaseReceipt = context.Model.FindEntityType("SistemiPos.Domain.Purchases.PurchaseReceipt");
        var product = context.Model.FindEntityType("SistemiPos.Domain.Catalog.Product");
        var productPriceRule = context.Model.FindEntityType("SistemiPos.Domain.Catalog.ProductPriceRule");

        Assert.NotNull(sale);
        Assert.NotNull(cancellation);
        Assert.NotNull(saleReturn);
        Assert.NotNull(purchaseOrder);
        Assert.Equal(300, product?.FindProperty("SupplierInvoiceName")?.GetMaxLength());
        Assert.NotNull(productPriceRule);
        Assert.Contains(sale!.GetIndexes(), index => index.IsUnique && index.Properties.Any(x => x.Name == "Number"));
        Assert.Contains(cancellation!.GetIndexes(), index => index.IsUnique && index.Properties.Any(x => x.Name == "Number"));
        Assert.Contains(cancellation.GetIndexes(), index => index.IsUnique && index.Properties.Any(x => x.Name == "SaleId"));
        Assert.Contains(saleReturn!.GetIndexes(), index => index.IsUnique && index.Properties.Any(x => x.Name == "Number"));
        Assert.Contains(purchaseOrder!.GetIndexes(), index => index.IsUnique && index.Properties.Any(x => x.Name == "Number"));
        Assert.Contains(purchase!.GetIndexes(), index => !index.IsUnique && index.Properties.Any(x => x.Name == "PurchaseOrderId"));
        Assert.Contains(purchaseReceipt!.GetIndexes(), index => !index.IsUnique && index.Properties.Any(x => x.Name == "PurchaseOrderId"));
        Assert.Contains(purchaseReceipt.GetIndexes(), index => index.IsUnique && index.Properties.Any(x => x.Name == "PurchaseId"));
        Assert.Contains(productPriceRule!.GetIndexes(), index => index.IsUnique && index.Properties.Select(x => x.Name).SequenceEqual(["ProductId", "Quantity"]));
    }

    [Fact]
    public void Model_Uses_PostgreSql_Xmin_For_Optimistic_Concurrency()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=SistemiPosModelTests;Username=postgres;Password=not-used")
            .Options;

        using var context = new AppDbContext(options);

        var inventoryVersion = context.Model
            .FindEntityType("SistemiPos.Domain.Inventory.InventoryStock")!
            .FindProperty("Version")!;
        var approvalVersion = context.Model
            .FindEntityType("SistemiPos.Domain.Sales.DiscountApprovalRequest")!
            .FindProperty("RowVersion")!;

        Assert.Equal(typeof(uint), inventoryVersion.ClrType);
        Assert.Equal("xmin", inventoryVersion.GetColumnName());
        Assert.True(inventoryVersion.IsConcurrencyToken);
        Assert.Equal(typeof(uint), approvalVersion.ClrType);
        Assert.Equal("xmin", approvalVersion.GetColumnName());
        Assert.True(approvalVersion.IsConcurrencyToken);
    }
}
