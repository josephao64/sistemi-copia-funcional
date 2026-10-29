using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Sales;

public sealed class SaleRefund : AuditableEntity
{
    public Guid SaleId { get; private set; }
    public Guid CashSessionId { get; private set; }
    public Guid? PaymentId { get; private set; }
    public string? OperationId { get; private set; }
    public decimal Amount { get; private set; }
    public RefundMethodType RefundMethod { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string ProcessedByUserName { get; private set; } = string.Empty;
    public string AuthorizedByUserName { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string? Reference { get; private set; }

    private SaleRefund()
    {
    }

    public SaleRefund(
        Guid saleId,
        Guid cashSessionId,
        decimal amount,
        RefundMethodType refundMethod,
        string reason,
        string processedByUserName,
        string authorizedByUserName,
        DateTimeOffset occurredAtUtc,
        string? reference, Guid? paymentId = null, string? operationId = null)
    {
        if (saleId == Guid.Empty || cashSessionId == Guid.Empty || amount <= 0 || string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleException("Los datos del reembolso no son válidos.");
        }

        SaleId = saleId;
        PaymentId = paymentId;
        OperationId = string.IsNullOrWhiteSpace(operationId) ? null : operationId.Trim();
        CashSessionId = cashSessionId;
        Amount = decimal.Round(amount, 2);
        RefundMethod = refundMethod;
        Reason = reason.Trim();
        ProcessedByUserName = string.IsNullOrWhiteSpace(processedByUserName) ? "system" : processedByUserName.Trim();
        AuthorizedByUserName = string.IsNullOrWhiteSpace(authorizedByUserName) ? "system" : authorizedByUserName.Trim();
        OccurredAtUtc = occurredAtUtc;
        Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
    }
}
