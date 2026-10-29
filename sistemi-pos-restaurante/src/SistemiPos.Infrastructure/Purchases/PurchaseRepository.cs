using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Purchases;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Purchases;
using SistemiPos.Domain.Finance;
using SistemiPos.Domain.Suppliers;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Purchases;

public sealed class PurchaseRepository(AppDbContext dbContext, SistemiPos.Application.Abstractions.IOperationContextAccessor? operationContext = null) : IPurchaseRepository
{
    public Task<List<Purchase>> GetPurchasesAsync(CancellationToken cancellationToken = default) =>
        dbContext.Purchases
            .Include(x => x.Supplier)
            .Include(x => x.Branch)
            .Include(x => x.Warehouse)
            .Include(x => x.Items)
            .ThenInclude(x => x.Product)
            .Include(x => x.PurchaseOrder).ThenInclude(x => x!.Receipts)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<List<Purchase>> GetPurchasesBySupplierAsync(Guid supplierId, CancellationToken cancellationToken = default) =>
        dbContext.Purchases
            .Include(x => x.Supplier)
            .Include(x => x.Branch)
            .Include(x => x.Warehouse)
            .Include(x => x.Items)
            .ThenInclude(x => x.Product)
            .Include(x => x.PurchaseOrder).ThenInclude(x => x!.Receipts)
            .Where(x => x.SupplierId == supplierId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<Purchase?> GetPurchaseAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Purchases
            .Include(x => x.Supplier)
            .Include(x => x.Branch)
            .Include(x => x.Warehouse)
            .Include(x => x.Items)
            .ThenInclude(x => x.Product)
            .Include(x => x.PurchaseOrder).ThenInclude(x => x!.Receipts)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Purchase?> GetPurchaseByOrderAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default) =>
        dbContext.Purchases
            .Include(x => x.Supplier)
            .Include(x => x.Items)
            .ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x => x.PurchaseOrderId == purchaseOrderId, cancellationToken);

    public Task<bool> InvoiceExistsForSupplierAsync(Guid supplierId, string invoiceNumber, CancellationToken cancellationToken = default) =>
        dbContext.Purchases.AnyAsync(x => x.SupplierId == supplierId && x.InvoiceNumber == invoiceNumber.Trim(), cancellationToken);

    public Task<Supplier?> GetSupplierAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Suppliers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Product?> GetProductAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Products
            .Include(x => x.Category)
            .Include(x => x.BaseUnit)
            .Include(x => x.PriceRules)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task AddPurchaseAsync(Purchase purchase, CancellationToken cancellationToken = default) =>
        dbContext.Purchases.AddAsync(purchase, cancellationToken).AsTask();

    public Task<bool> PayableAccountExistsAsync(Guid purchaseId, CancellationToken cancellationToken = default) =>
        dbContext.PayableAccounts.AnyAsync(x => x.PurchaseId == purchaseId, cancellationToken);

    public Task AddPayableAccountAsync(PayableAccount account, CancellationToken cancellationToken = default) =>
        dbContext.PayableAccounts.AddAsync(account, cancellationToken).AsTask();

    public Task<PayableAccount?> GetPayableAccountAsync(Guid purchaseId, CancellationToken cancellationToken = default) =>
        dbContext.PayableAccounts.Include(x => x.Payments).SingleOrDefaultAsync(x => x.PurchaseId == purchaseId, cancellationToken);

    public Task<InventoryStock?> GetStockAsync(Guid productId, CancellationToken cancellationToken = default) =>
        dbContext.InventoryStocks.FirstOrDefaultAsync(x => x.ProductId == productId
            && (operationContext == null || !operationContext.CurrentWarehouseId.HasValue || x.WarehouseId == operationContext.CurrentWarehouseId), cancellationToken);

    public Task<InventoryStock?> GetStockAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default) =>
        dbContext.InventoryStocks.FirstOrDefaultAsync(x => x.ProductId == productId && x.WarehouseId == warehouseId, cancellationToken);

    public Task AddStockAsync(InventoryStock stock, CancellationToken cancellationToken = default)
    {
        if (!stock.WarehouseId.HasValue && operationContext?.CurrentWarehouseId is Guid warehouseId)
            stock.AssignWarehouse(warehouseId);
        return dbContext.InventoryStocks.AddAsync(stock, cancellationToken).AsTask();
    }

    public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default) =>
        dbContext.InventoryMovements.AddAsync(movement, cancellationToken).AsTask();

    public Task AddExpiryLotAsync(InventoryExpiryLot lot, CancellationToken cancellationToken = default) =>
        dbContext.InventoryExpiryLots.AddAsync(lot, cancellationToken).AsTask();

    public async Task<string> GeneratePurchaseNumberAsync(CancellationToken cancellationToken = default)
    {
        var lastNumber = await dbContext.Purchases
            .OrderByDescending(x => x.Number)
            .Select(x => x.Number)
            .FirstOrDefaultAsync(cancellationToken);

        var nextSequence = 1;
        if (!string.IsNullOrWhiteSpace(lastNumber) && lastNumber.StartsWith("C-"))
        {
            var suffix = lastNumber[2..];
            if (int.TryParse(suffix, out var parsed))
            {
                nextSequence = parsed + 1;
            }
        }

        return $"C-{nextSequence:000000}";
    }

    public Task<List<PurchaseOrder>> GetPurchaseOrdersAsync(CancellationToken cancellationToken = default) =>
        dbContext.PurchaseOrders
            .Include(x => x.Supplier)
            .Include(x => x.Branch)
            .Include(x => x.Warehouse)
            .Include(x => x.Items)
            .ThenInclude(x => x.Product)
            .Include(x => x.Purchases)
            .Include(x => x.Receipts).ThenInclude(x => x.Purchase)
            .Include(x => x.Receipts).ThenInclude(x => x.Items).ThenInclude(x => x.Product)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<PurchaseOrder?> GetPurchaseOrderAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.PurchaseOrders
            .Include(x => x.Supplier)
            .Include(x => x.Branch)
            .Include(x => x.Warehouse)
            .Include(x => x.Items)
            .ThenInclude(x => x.Product)
            .Include(x => x.Purchases)
            .Include(x => x.Receipts).ThenInclude(x => x.Purchase)
            .Include(x => x.Receipts).ThenInclude(x => x.Items).ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task AddPurchaseOrderAsync(PurchaseOrder order, CancellationToken cancellationToken = default) =>
        dbContext.PurchaseOrders.AddAsync(order, cancellationToken).AsTask();

    public Task AddPurchaseReceiptAsync(PurchaseReceipt receipt, CancellationToken cancellationToken = default) =>
        dbContext.PurchaseReceipts.AddAsync(receipt, cancellationToken).AsTask();

    public Task<List<ProductSupplier>> GetProductSuppliersAsync(Guid? productId = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.ProductSuppliers.Include(x => x.Product).Include(x => x.Supplier).AsQueryable();
        if (productId.HasValue) query = query.Where(x => x.ProductId == productId.Value);
        return query.ToListAsync(cancellationToken);
    }

    public Task<ProductSupplier?> GetProductSupplierAsync(Guid productId, Guid supplierId, CancellationToken cancellationToken = default) =>
        dbContext.ProductSuppliers.Include(x => x.Product).Include(x => x.Supplier)
            .FirstOrDefaultAsync(x => x.ProductId == productId && x.SupplierId == supplierId, cancellationToken);

    public Task AddProductSupplierAsync(ProductSupplier productSupplier, CancellationToken cancellationToken = default) =>
        dbContext.ProductSuppliers.AddAsync(productSupplier, cancellationToken).AsTask();

    public Task<List<SupplierProductPriceHistory>> GetSupplierProductPriceHistoryAsync(Guid productId, Guid? supplierId = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.SupplierProductPriceHistory.Include(x => x.Product).Include(x => x.Supplier)
            .Where(x => x.ProductId == productId);
        if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId.Value);
        return query.ToListAsync(cancellationToken);
    }

    public Task AddSupplierProductPriceHistoryAsync(SupplierProductPriceHistory history, CancellationToken cancellationToken = default) =>
        dbContext.SupplierProductPriceHistory.AddAsync(history, cancellationToken).AsTask();

    public Task<List<PurchasePresentation>> GetPurchasePresentationsAsync(CancellationToken cancellationToken = default) =>
        dbContext.PurchasePresentations.Where(x => x.IsActive).OrderBy(x => x.Units).ThenBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<PurchasePresentation?> GetPurchasePresentationAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.PurchasePresentations.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken);

    public Task<ProductPurchasePresentation?> GetProductPurchasePresentationAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.ProductPurchasePresentations.Include(x => x.Product).ThenInclude(x => x.BaseUnit)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task AddPurchasePresentationAsync(PurchasePresentation presentation, CancellationToken cancellationToken = default) =>
        dbContext.PurchasePresentations.AddAsync(presentation, cancellationToken).AsTask();

    public async Task<string> GeneratePurchaseOrderNumberAsync(CancellationToken cancellationToken = default)
    {
        var lastNumber = await dbContext.PurchaseOrders
            .OrderByDescending(x => x.Number)
            .Select(x => x.Number)
            .FirstOrDefaultAsync(cancellationToken);
        var nextSequence = 1;
        if (!string.IsNullOrWhiteSpace(lastNumber)
            && lastNumber.StartsWith("PC-")
            && int.TryParse(lastNumber[3..], out var parsed))
        {
            nextSequence = parsed + 1;
        }

        return $"PC-{nextSequence:000000}";
    }
}
