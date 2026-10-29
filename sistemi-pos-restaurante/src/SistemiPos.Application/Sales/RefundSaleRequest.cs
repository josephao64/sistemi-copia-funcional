using SistemiPos.Domain.Sales;

namespace SistemiPos.Application.Sales;

public sealed record RefundSaleRequest(
    Guid CashSessionId,
    decimal Amount,
    RefundMethodType RefundMethod,
    string Reason,
    string AuthorizedByUserName,
    string? Reference,
    string? AuthorizerPassword = null,
    Guid? PaymentId = null,
    string? OperationId = null);
