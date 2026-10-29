using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Customers;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Sales;
using SistemiPos.Domain.Finance;
using SistemiPos.Domain.Audit;
using SistemiPos.Domain.Organizations;

namespace SistemiPos.Application.Sales;

public interface ISaleRepository
{
    Task<List<Sale>> GetSalesAsync(CancellationToken cancellationToken = default);
    Task<Sale?> GetSaleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsClientOperationIdAsync(string clientOperationId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    async Task<Sale?> GetByClientOperationIdAsync(string clientOperationId, CancellationToken ct = default) =>
        (await GetSalesAsync(ct)).FirstOrDefault(x => x.ClientOperationId == clientOperationId.Trim());
    Task<string> GenerateSaleNumberAsync(CancellationToken cancellationToken = default);
    Task<string> GenerateCancellationNumberAsync(CancellationToken cancellationToken = default);
    Task<string> GenerateReturnNumberAsync(CancellationToken cancellationToken = default);
    Task<CashSession?> GetCashSessionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Customer?> GetActiveCustomerByNormalizedNitAsync(string normalizedNit, CancellationToken cancellationToken = default);
    Task<Customer?> GetActiveCustomerByPhoneAsync(string phone, CancellationToken cancellationToken = default) => Task.FromResult<Customer?>(null);
    Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken = default);
    Task<Product?> GetProductAsync(Guid id, CancellationToken cancellationToken = default);
    Task<InventoryStock?> GetStockAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<InventoryStock?> GetStockAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default) => GetStockAsync(productId, cancellationToken);
    Task<Warehouse?> GetDefaultWarehouseForBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult<Warehouse?>(null);
    Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default);
    Task<List<InventoryExpiryLot>> GetExpiryLotsAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult(new List<InventoryExpiryLot>());
    Task AddCashMovementAsync(CashMovement movement, CancellationToken cancellationToken = default);
    Task<SystemSetting> GetSystemSettingAsync(CancellationToken cancellationToken = default);
    Task<BankAccount?> GetBankAccountAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddSaleAsync(Sale sale, CancellationToken cancellationToken = default);
    Task AddDiscountApprovalAsync(DiscountApprovalRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task<DiscountApprovalRequest?> GetDiscountApprovalAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<DiscountApprovalRequest?>(null);
    Task<List<DiscountApprovalRequest>> GetPendingDiscountApprovalsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<DiscountApprovalRequest>());
    Task AddCancellationAsync(SaleCancellation cancellation, CancellationToken cancellationToken = default);
    Task AddReturnAsync(SaleReturn saleReturn, CancellationToken cancellationToken = default);
    Task AddRefundAsync(SaleRefund refund, CancellationToken cancellationToken = default);
    Task<SaleRefund?> GetRefundByOperationAsync(Guid saleId, string operationId, CancellationToken ct = default) => Task.FromResult<SaleRefund?>(null);
    Task<SaleReturn?> GetReturnByOperationAsync(Guid saleId, string operationId, CancellationToken ct = default) => Task.FromResult<SaleReturn?>(null);
    Task<Dictionary<Guid, decimal>> GetReturnedQuantitiesByProductAsync(Guid saleId, CancellationToken cancellationToken = default);
    Task<decimal> GetRefundedTotalAsync(Guid saleId, CancellationToken cancellationToken = default);
    Task<decimal> GetRefundedPaymentTotalAsync(Guid paymentId, CancellationToken cancellationToken = default) => Task.FromResult(0m);
    Task<decimal?> GetUserDiscountLimitAsync(string userName, CancellationToken cancellationToken = default);
    Task<bool> UserHasPermissionAsync(string userName, string permissionCode, CancellationToken cancellationToken = default);
    Task<PosDevice?> GetPosDeviceAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<PosDevice?>(null);
    Task AddReceivableAccountAsync(ReceivableAccount account, CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task<CashSession?> GetActiveCashSessionByUserAsync(string userName, CancellationToken cancellationToken = default) => Task.FromResult<CashSession?>(null);
    Task<ReceivableAccount?> GetReceivableAccountBySaleAsync(Guid saleId, CancellationToken cancellationToken = default) => Task.FromResult<ReceivableAccount?>(null);
    Task AddFinancialAuditAsync(FinancialAuditEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task<bool> IsPaymentMethodEnabledAsync(PaymentMethodType paymentMethod, CancellationToken cancellationToken = default) => Task.FromResult(true);
    Task<List<PaymentMethodDefinition>> GetPaymentDefinitionsAsync(CancellationToken ct = default) => Task.FromResult(new List<PaymentMethodDefinition>());
}
