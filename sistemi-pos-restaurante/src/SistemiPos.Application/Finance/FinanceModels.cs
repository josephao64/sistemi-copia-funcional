using SistemiPos.Domain.Finance;

namespace SistemiPos.Application.Finance;

public sealed record PayableAccountDto(Guid Id, Guid PurchaseId, Guid SupplierId, string SupplierName, string InvoiceNumber,
    DateTimeOffset InvoiceDateUtc, DateTimeOffset DueDateUtc, decimal Total, decimal Paid, decimal Balance, AccountStatus Status);
public sealed record PayPayableRequest(decimal Amount, FinancialPaymentMethod PaymentMethod, MoneySourceType MoneySource,
    Guid? SourceAccountId, string? Reference, string? AuthorizerUserName = null, string? AuthorizerPassword = null);
public sealed record PayablePaymentDto(Guid Id, DateTimeOffset PaidAtUtc, decimal Amount, FinancialPaymentMethod PaymentMethod,
    MoneySourceType MoneySource, string? Reference, string UserName);
public sealed record PayableAccountDetailDto(Guid Id, Guid PurchaseId, Guid SupplierId, string SupplierName, string InvoiceNumber,
    string PurchaseNumber, Guid? PurchaseOrderId, string? PurchaseOrderNumber, DateTimeOffset InvoiceDateUtc,
    DateTimeOffset DueDateUtc, decimal Total, decimal Paid, decimal Balance, AccountStatus Status,
    IReadOnlyList<PayablePaymentDto> Payments);

public enum ReceivableSection { General, Overdue, Delinquent, WithoutPayments, PartiallyPaid, History }
public sealed record ReceivableQuery(ReceivableSection Section = ReceivableSection.General, Guid? CustomerId = null,
    AccountStatus? Status = null, DateTime? From = null, DateTime? To = null, string? Search = null);
public sealed record ReceivableAccountDto(Guid Id, Guid SaleId, Guid CustomerId, string CustomerName, string SaleNumber,
    DateTimeOffset SaleDateUtc, DateTimeOffset DueDateUtc, decimal Total, decimal Paid, decimal Balance, AccountStatus Status,
    int CreditDays = 0, int GraceDays = 0, DateTimeOffset? LastPaymentAtUtc = null);
public sealed record ReceivableSummaryDto(decimal TotalPending, int PendingCount, int WithoutPaymentsCount,
    int PartiallyPaidCount, int OverdueCount, decimal OverdueAmount, int DelinquentCount, decimal DelinquentAmount);
public sealed record ReceivableListResult(IReadOnlyList<ReceivableAccountDto> Accounts, ReceivableSummaryDto Summary);
public sealed record ReceivablePaymentDto(Guid Id, DateTimeOffset ReceivedAtUtc, decimal Amount,
    FinancialPaymentMethod PaymentMethod, string? Reference, string UserName);
public sealed record ReceivableAccountDetailDto(Guid Id, Guid SaleId, Guid CustomerId, string CustomerName,
    string? CustomerNit, string SaleNumber, DateTimeOffset SaleDateUtc, DateTimeOffset DueDateUtc, decimal Total,
    decimal Paid, decimal Balance, int CreditDays, int GraceDays, AccountStatus Status,
    IReadOnlyList<ReceivablePaymentDto> Payments);
public sealed record CollectReceivableRequest(decimal Amount, FinancialPaymentMethod PaymentMethod, string? Reference,
    string? AuthorizerUserName = null, string? AuthorizerPassword = null);

public sealed record PettyCashDto(Guid Id, string Name, string ResponsibleUserName, decimal FundAmount, decimal Balance, bool IsActive,
    Guid? CurrentCycleId, DateTimeOffset? OpenedAtUtc, DateTimeOffset? ClosedAtUtc, IReadOnlyList<PettyCashMovementDto> Movements,
    IReadOnlyList<PettyCashCategoryDto> Categories);
public sealed record PettyCashCategoryDto(Guid Id, string Name);
public sealed record PettyCashMovementDto(Guid Id, PettyCashMovementType Type, decimal Amount, string Concept, string? Category,
    string? Document, string? Beneficiary, string? Observation, string UserName, DateTimeOffset OccurredAtUtc, string? AuthorizedByUserName,
    bool IsCancelled = false, string? CancellationReason = null);
public sealed record PettyCashMovementRequest(PettyCashMovementType Type, decimal Amount, string Concept, Guid? CategoryId,
    string? Document, string? Beneficiary, string? Observation, string? Reference,
    string? AuthorizerUserName = null, string? AuthorizerPassword = null);
public sealed record PettyCashSettlementRequest(decimal CountedCash, string? Observation);

public interface IFinancialService
{
    Task<IReadOnlyList<PayableAccountDto>> GetPayablesAsync(CancellationToken cancellationToken = default);
    Task<PayableAccountDetailDto?> GetPayableAsync(Guid id, CancellationToken cancellationToken = default);
    Task PayPayableAsync(Guid id, PayPayableRequest request, CancellationToken cancellationToken = default);
    Task<ReceivableListResult> GetReceivablesAsync(ReceivableQuery query, CancellationToken cancellationToken = default);
    Task<ReceivableAccountDetailDto?> GetReceivableAsync(Guid id, CancellationToken cancellationToken = default);
    Task CollectReceivableAsync(Guid id, CollectReceivableRequest request, CancellationToken cancellationToken = default);
    Task<PettyCashDto> GetPettyCashAsync(CancellationToken cancellationToken = default);
    Task OpenPettyCashAsync(decimal initialBalance, CancellationToken cancellationToken = default);
    Task AddPettyCashMovementAsync(PettyCashMovementRequest request, CancellationToken cancellationToken = default);
    Task SettlePettyCashAsync(PettyCashSettlementRequest request, CancellationToken cancellationToken = default);
    Task CancelPettyCashMovementAsync(Guid movementId, string reason, string authorizerUserName, string authorizerPassword, CancellationToken cancellationToken = default);
    Task CancelPayableAsync(Guid id, CancellationToken cancellationToken = default);
    Task CancelReceivableAsync(Guid id, CancellationToken cancellationToken = default);
}
