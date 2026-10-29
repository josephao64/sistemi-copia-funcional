namespace SistemiPos.Application.Purchases;

public interface IPurchaseService
{
    Task<IReadOnlyList<PurchaseDto>> GetPurchasesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseDto>> GetPurchasesBySupplierAsync(Guid supplierId, CancellationToken cancellationToken = default);
    Task<PurchaseDto?> GetPurchaseAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> InvoiceExistsForSupplierAsync(Guid supplierId, string invoiceNumber, CancellationToken cancellationToken = default);
    Task<Guid> CreatePurchaseAsync(SavePurchaseRequest request, CancellationToken cancellationToken = default);
    Task<Guid> CreateDirectPurchaseAsync(CreateDirectPurchaseRequest request, CancellationToken cancellationToken = default);
    Task ConfirmPurchaseAsync(Guid id, CancellationToken cancellationToken = default);
    Task CancelPurchaseAsync(Guid id, string reason, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseOrderDto>> GetPurchaseOrdersAsync(CancellationToken cancellationToken = default);
    Task<PurchaseOrderDto?> GetPurchaseOrderAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest request, CancellationToken cancellationToken = default);
    Task UpdatePurchaseOrderAsync(Guid id, UpdatePurchaseOrderRequest request, CancellationToken cancellationToken = default);
    Task SubmitPurchaseOrderAsync(Guid id, CancellationToken cancellationToken = default);
    Task ReceivePurchaseOrderAsync(Guid id, ReceivePurchaseOrderRequest request, CancellationToken cancellationToken = default);
    Task CancelPurchaseOrderAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductSupplierDto>> GetProductSuppliersAsync(Guid? productId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SupplierProductPriceHistoryDto>> GetSupplierProductPriceHistoryAsync(Guid productId, Guid? supplierId = null, CancellationToken cancellationToken = default);
    Task<Guid> SaveProductSupplierAsync(SaveProductSupplierRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchasePresentationDto>> GetPurchasePresentationsAsync(CancellationToken cancellationToken = default);
    Task<PurchasePresentationDto> CreatePurchasePresentationAsync(string name, int units, CancellationToken cancellationToken = default);
}
