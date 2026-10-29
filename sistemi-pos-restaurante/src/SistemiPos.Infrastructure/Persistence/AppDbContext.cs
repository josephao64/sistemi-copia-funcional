using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Abstractions;
using SistemiPos.Domain.Audit;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Customers;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Finance;
using SistemiPos.Domain.Organizations;
using SistemiPos.Domain.Purchases;
using SistemiPos.Domain.Sales;
using SistemiPos.Domain.Security;
using SistemiPos.Domain.Suppliers;
using SistemiPos.Infrastructure.Security;
using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;
using Npgsql;

namespace SistemiPos.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, AppRole, Guid>(options), IUnitOfWork
{
    public DbSet<SistemiPos.Domain.Restaurant.RestaurantSetup> RestaurantSetups => Set<SistemiPos.Domain.Restaurant.RestaurantSetup>();
    public DbSet<SistemiPos.Domain.Restaurant.OrderWaste> OrderWaste => Set<SistemiPos.Domain.Restaurant.OrderWaste>();
    public DbSet<SistemiPos.Domain.Restaurant.RestaurantOrder> RestaurantOrders => Set<SistemiPos.Domain.Restaurant.RestaurantOrder>();
    public DbSet<SistemiPos.Domain.Restaurant.RestaurantOrderLine> RestaurantOrderLines => Set<SistemiPos.Domain.Restaurant.RestaurantOrderLine>();
    public DbSet<SistemiPos.Domain.Restaurant.RestaurantEvent> RestaurantEvents => Set<SistemiPos.Domain.Restaurant.RestaurantEvent>();
    public DbSet<SistemiPos.Domain.Restaurant.OrderReservation> OrderReservations => Set<SistemiPos.Domain.Restaurant.OrderReservation>();
    public DbSet<SistemiPos.Domain.Restaurant.TableTurn> RestaurantTableTurns => Set<SistemiPos.Domain.Restaurant.TableTurn>();
    public DbSet<SistemiPos.Domain.Restaurant.RestaurantCommand> RestaurantCommands => Set<SistemiPos.Domain.Restaurant.RestaurantCommand>();
    public DbSet<BusinessGroup> BusinessGroups => Set<BusinessGroup>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<UserScopeAssignment> UserScopeAssignments => Set<UserScopeAssignment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<FinancialAuditEntry> FinancialAuditEntries => Set<FinancialAuditEntry>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<OriginBank> OriginBanks => Set<OriginBank>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<CashSession> CashSessions => Set<CashSession>();
    public DbSet<CashCount> CashCounts => Set<CashCount>();
    public DbSet<CashCountLine> CashCountLines => Set<CashCountLine>();
    public DbSet<CashPosClosing> CashPosClosings => Set<CashPosClosing>();
    public DbSet<CashSessionPosDevice> CashSessionPosDevices => Set<CashSessionPosDevice>();
    public DbSet<CashDenomination> CashDenominations => Set<CashDenomination>();
    public DbSet<PosDevice> PosDevices => Set<PosDevice>();
    public DbSet<PaymentMethodDefinition> PaymentMethodDefinitions => Set<PaymentMethodDefinition>();
    public DbSet<CashMovementConcept> CashMovementConcepts => Set<CashMovementConcept>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<SistemiPos.Domain.Recipes.RecipeVersion> RecipeVersions => Set<SistemiPos.Domain.Recipes.RecipeVersion>();
    public DbSet<SistemiPos.Domain.Recipes.ProductionOrder> ProductionOrders => Set<SistemiPos.Domain.Recipes.ProductionOrder>();
    public DbSet<SistemiPos.Domain.Recipes.ProductCostBaseline> ProductCostBaselines => Set<SistemiPos.Domain.Recipes.ProductCostBaseline>();
    public DbSet<SistemiPos.Domain.Recipes.Recipe> Recipes => Set<SistemiPos.Domain.Recipes.Recipe>();
    public DbSet<SistemiPos.Domain.Recipes.RecipeIngredient> RecipeIngredients => Set<SistemiPos.Domain.Recipes.RecipeIngredient>();
    public DbSet<ProductPriceRule> ProductPriceRules => Set<ProductPriceRule>();
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<ProductUnitConversion> ProductUnitConversions => Set<ProductUnitConversion>();
    public DbSet<ProductPurchasePresentation> ProductPurchasePresentations => Set<ProductPurchasePresentation>();
    public DbSet<ProductBranchSetting> ProductBranchSettings => Set<ProductBranchSetting>();
    public DbSet<CatalogChangeLog> CatalogChangeLogs => Set<CatalogChangeLog>();
    public DbSet<VariantType> VariantTypes => Set<VariantType>();
    public DbSet<VariantOption> VariantOptions => Set<VariantOption>();
    public DbSet<ProductVariantOption> ProductVariantOptions => Set<ProductVariantOption>();
    public DbSet<RoleDiscountSetting> RoleDiscountSettings => Set<RoleDiscountSetting>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleCancellation> SaleCancellations => Set<SaleCancellation>();
    public DbSet<SaleRefund> SaleRefunds => Set<SaleRefund>();
    public DbSet<SaleReturn> SaleReturns => Set<SaleReturn>();
    public DbSet<SaleReturnItem> SaleReturnItems => Set<SaleReturnItem>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<SalePayment> SalePayments => Set<SalePayment>();
    public DbSet<DiscountApprovalRequest> DiscountApprovalRequests => Set<DiscountApprovalRequest>();
    public DbSet<DiscountApprovalItem> DiscountApprovalItems => Set<DiscountApprovalItem>();
    public DbSet<FelCertification> FelCertifications => Set<FelCertification>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<CurrencyDefinition> Currencies => Set<CurrencyDefinition>();
    public DbSet<UserDiscountSetting> UserDiscountSettings => Set<UserDiscountSetting>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<InventoryStock> InventoryStocks => Set<InventoryStock>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<InventoryExpiryLot> InventoryExpiryLots => Set<InventoryExpiryLot>();
    public DbSet<InventoryTransfer> InventoryTransfers => Set<InventoryTransfer>();
    public DbSet<InventoryTransferItem> InventoryTransferItems => Set<InventoryTransferItem>();
    public DbSet<PhysicalInventoryCount> PhysicalInventoryCounts => Set<PhysicalInventoryCount>();
    public DbSet<PhysicalInventoryCountCategory> PhysicalInventoryCountCategories => Set<PhysicalInventoryCountCategory>();
    public DbSet<PhysicalInventoryCountItem> PhysicalInventoryCountItems => Set<PhysicalInventoryCountItem>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<PurchaseReceipt> PurchaseReceipts => Set<PurchaseReceipt>();
    public DbSet<PurchaseReceiptItem> PurchaseReceiptItems => Set<PurchaseReceiptItem>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<ProductSupplier> ProductSuppliers => Set<ProductSupplier>();
    public DbSet<SupplierProductPriceHistory> SupplierProductPriceHistory => Set<SupplierProductPriceHistory>();
    public DbSet<PurchasePresentation> PurchasePresentations => Set<PurchasePresentation>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<PayableAccount> PayableAccounts => Set<PayableAccount>();
    public DbSet<PayablePayment> PayablePayments => Set<PayablePayment>();
    public DbSet<ReceivableAccount> ReceivableAccounts => Set<ReceivableAccount>();
    public DbSet<ReceivablePayment> ReceivablePayments => Set<ReceivablePayment>();
    public DbSet<PettyCashFund> PettyCashFunds => Set<PettyCashFund>();
    public DbSet<PettyCashMovement> PettyCashMovements => Set<PettyCashMovement>();
    public DbSet<PettyCashSettlement> PettyCashSettlements => Set<PettyCashSettlement>();
    public DbSet<PettyCashCycle> PettyCashCycles => Set<PettyCashCycle>();
    public DbSet<PettyCashCategory> PettyCashCategories => Set<PettyCashCategory>();

    public DbSet<InventoryMovementLot> InventoryMovementLots => Set<InventoryMovementLot>();
    public DbSet<CompanySetting> CompanySettings => Set<CompanySetting>();
    public DbSet<BranchSetting> BranchSettings => Set<BranchSetting>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ProtectPurchaseHistoryAsync(CancellationToken.None).GetAwaiter().GetResult();
        SistemiPos.Infrastructure.Inventory.InventoryLotLedger.PrepareAsync(this, CancellationToken.None).GetAwaiter().GetResult();
        try { return base.SaveChanges(acceptAllChangesOnSuccess); }
        catch (DbUpdateException ex) when (IsDuplicateSupplierInvoice(ex))
        { throw new DomainRuleException("La factura ya fue registrada para este proveedor."); }
        catch (DbUpdateException ex) when (IsDuplicateActiveCash(ex))
        { throw new DomainRuleException("La caja física o el usuario ya tienen una sesión activa."); }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await ProtectPurchaseHistoryAsync(cancellationToken);
        await SistemiPos.Infrastructure.Inventory.InventoryLotLedger.PrepareAsync(this, cancellationToken);
        try { return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
        catch (DbUpdateException ex) when (IsDuplicateSupplierInvoice(ex))
        { throw new DomainRuleException("La factura ya fue registrada para este proveedor."); }
        catch (DbUpdateException ex) when (IsDuplicateActiveCash(ex))
        { throw new DomainRuleException("La caja física o el usuario ya tienen una sesión activa."); }
    }

    private static bool IsDuplicateSupplierInvoice(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Purchases_SupplierId_InvoiceNumber" };
    private static bool IsDuplicateActiveCash(DbUpdateException exception) => exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_CashSessions_BranchId_RegisterCode" or "IX_CashSessions_CashierUserName" or "IX_CashSessions_CashierUserId" or "IX_CashSessions_BranchId_TerminalId" };

    private async Task ProtectPurchaseHistoryAsync(CancellationToken cancellationToken)
    {
        if (ChangeTracker.Entries<SistemiPos.Domain.Audit.AuditLog>().Any(x=>x.State is EntityState.Modified or EntityState.Deleted))
            throw new DomainRuleException("La auditoría de operaciones es histórica y no puede sobrescribirse.");
        var fiscalFields=new HashSet<string> { "SaleId","CompanyId","BranchId","IdempotencyKey","PayloadJson" };
        if(ChangeTracker.Entries<FiscalSubmission>().Any(x=>x.State==EntityState.Deleted
            || (x.State==EntityState.Modified&&x.Properties.Any(p=>p.IsModified&&fiscalFields.Contains(p.Metadata.Name)))))
            throw new DomainRuleException("El documento fiscal conserva su venta, ubicación y contenido original.");
        if (ChangeTracker.Entries().Any(x => x.State == EntityState.Deleted && x.Entity is Sale or SaleItem or SalePayment
            or SaleRefund or SaleReturn or SaleReturnItem or SaleCancellation or SaleAccountPart or CashSession or CashMovement or CashCount or CashPosClosing))
            throw new DomainRuleException("Los documentos financieros y de caja conservan su histórico y no pueden eliminarse físicamente.");
        if (ChangeTracker.Entries<SistemiPos.Domain.Orders.RestaurantOrder>().Any(x => x.State == EntityState.Deleted)
            || ChangeTracker.Entries<SistemiPos.Domain.Orders.RestaurantOrderEvent>().Any(x => x.State is EntityState.Deleted or EntityState.Modified))
            throw new DomainRuleException("Los pedidos y sus eventos históricos no pueden eliminarse ni sobrescribirse.");
        var removedOrderIds = ChangeTracker.Entries<SistemiPos.Domain.Orders.RestaurantOrderLine>()
            .Where(x => x.State == EntityState.Deleted).Select(x => x.Entity.OrderId).Distinct().ToList();
        if (removedOrderIds.Count > 0 && await Set<SistemiPos.Domain.Orders.RestaurantOrder>()
            .AnyAsync(x => removedOrderIds.Contains(x.Id) && x.IsConfirmed, cancellationToken))
            throw new DomainRuleException("Las líneas de un pedido confirmado no pueden eliminarse.");
        var historyFields = new HashSet<string> { "OrderId", "ProductId", "ProductName", "Quantity", "UnitPrice",
            "GrossAmount", "TaxPercent", "IsTaxExempt", "RecipeJson", "RecipeRevision", "ModifiersJson", "ReplacesLineId", "RevisionOperationId" };
        var changedOrderIds = ChangeTracker.Entries<SistemiPos.Domain.Orders.RestaurantOrderLine>()
            .Where(x => x.State == EntityState.Modified && x.Properties.Any(p => p.IsModified && historyFields.Contains(p.Metadata.Name)))
            .Select(x => x.Entity.OrderId).Distinct().ToList();
        if (changedOrderIds.Count > 0 && await Set<SistemiPos.Domain.Orders.RestaurantOrder>()
            .AnyAsync(x => changedOrderIds.Contains(x.Id) && x.IsConfirmed, cancellationToken))
            throw new DomainRuleException("Los precios, cantidades y recetas históricos del pedido no pueden sobrescribirse.");
        if (ChangeTracker.Entries<SistemiPos.Domain.Restaurant.OrderWaste>().Any(x => x.State is EntityState.Modified or EntityState.Deleted)
            || ChangeTracker.Entries<SistemiPos.Domain.Restaurant.RestaurantEvent>().Any(x => x.State is EntityState.Modified or EntityState.Deleted)
            || ChangeTracker.Entries<SistemiPos.Domain.Restaurant.RestaurantOrder>().Any(x => x.State == EntityState.Deleted))
            throw new DomainRuleException("Los pedidos y su historial no pueden eliminarse ni reescribirse.");
        if (ChangeTracker.Entries<SistemiPos.Domain.Recipes.RecipeVersion>().Any(x => x.State is EntityState.Modified or EntityState.Deleted)
            || ChangeTracker.Entries<SistemiPos.Domain.Recipes.ProductionOrder>().Any(x => x.State == EntityState.Deleted
                || (x.State == EntityState.Modified && x.Property(y => y.Status).OriginalValue == SistemiPos.Domain.Recipes.ProductionStatus.Confirmed)))
            throw new DomainRuleException("Las versiones de receta y producciones confirmadas son históricas e inmutables.");
        if (ChangeTracker.Entries<Purchase>().Any(x => x.State == EntityState.Deleted
                && (x.Entity.Status != DocumentStatus.Draft || x.Property(y => y.Status).OriginalValue != DocumentStatus.Draft))
            || ChangeTracker.Entries<PurchaseReceipt>().Any(x => x.State == EntityState.Deleted)
            || ChangeTracker.Entries<PurchaseReceiptItem>().Any(x => x.State == EntityState.Deleted))
            throw new DomainRuleException("Las compras recibidas y sus recepciones no pueden eliminarse físicamente.");
        var purchaseIds = ChangeTracker.Entries<PurchaseItem>().Where(x => x.State == EntityState.Deleted)
            .Select(x => x.Entity.PurchaseId).Distinct().ToList();
        if (purchaseIds.Count > 0 && await Purchases.AnyAsync(x => purchaseIds.Contains(x.Id)
                && x.Status != DocumentStatus.Draft, cancellationToken))
            throw new DomainRuleException("El detalle de una compra recibida no puede eliminarse físicamente.");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasSequence<long>("ProductInternalCodeSequence");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        Configurations.RestaurantConfiguration.Configure(modelBuilder);
    }
}
