using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Purchases;
using SistemiPos.Domain.Suppliers;
using SistemiPos.Domain.Finance;

namespace SistemiPos.Application.Purchases;

public interface IPurchaseRepository
{
    Task<List<Purchase>> GetPurchasesAsync(CancellationToken cancellationToken = default);
    Task<List<Purchase>> GetPurchasesBySupplierAsync(Guid supplierId, CancellationToken cancellationToken = default);
    Task<Purchase?> GetPurchaseAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Purchase?> GetPurchaseByOrderAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default) => Task.FromResult<Purchase?>(null);
    Task<bool> InvoiceExistsForSupplierAsync(Guid supplierId, string invoiceNumber, CancellationToken cancellationToken = default) => Task.FromResult(false);
    Task<Supplier?> GetSupplierAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Product?> GetProductAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddPurchaseAsync(Purchase purchase, CancellationToken cancellationToken = default);
    Task<InventoryStock?> GetStockAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<InventoryStock?> GetStockAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default) => GetStockAsync(productId, cancellationToken);
    Task AddStockAsync(InventoryStock stock, CancellationToken cancellationToken = default);
    Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default);
    Task AddExpiryLotAsync(InventoryExpiryLot lot, CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task<string> GeneratePurchaseNumberAsync(CancellationToken cancellationToken = default);
    Task<List<PurchaseOrder>> GetPurchaseOrdersAsync(CancellationToken cancellationToken = default);
    Task<PurchaseOrder?> GetPurchaseOrderAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddPurchaseOrderAsync(PurchaseOrder order, CancellationToken cancellationToken = default);
    Task AddPurchaseReceiptAsync(PurchaseReceipt receipt, CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task<string> GeneratePurchaseOrderNumberAsync(CancellationToken cancellationToken = default);
    Task<bool> PayableAccountExistsAsync(Guid purchaseId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    Task AddPayableAccountAsync(PayableAccount account, CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task<PayableAccount?> GetPayableAccountAsync(Guid purchaseId, CancellationToken cancellationToken = default);
    Task<List<ProductSupplier>> GetProductSuppliersAsync(Guid? productId = null, CancellationToken cancellationToken = default) => Task.FromResult(new List<ProductSupplier>());
    Task<ProductSupplier?> GetProductSupplierAsync(Guid productId, Guid supplierId, CancellationToken cancellationToken = default) => Task.FromResult<ProductSupplier?>(null);
    Task AddProductSupplierAsync(ProductSupplier productSupplier, CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task<List<SupplierProductPriceHistory>> GetSupplierProductPriceHistoryAsync(Guid productId, Guid? supplierId = null, CancellationToken cancellationToken = default) => Task.FromResult(new List<SupplierProductPriceHistory>());
    Task AddSupplierProductPriceHistoryAsync(SupplierProductPriceHistory history, CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task<List<PurchasePresentation>> GetPurchasePresentationsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<PurchasePresentation>());
    Task<PurchasePresentation?> GetPurchasePresentationAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<PurchasePresentation?>(null);
    Task<ProductPurchasePresentation?> GetProductPurchasePresentationAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<ProductPurchasePresentation?>(null);
    Task AddPurchasePresentationAsync(PurchasePresentation presentation, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
