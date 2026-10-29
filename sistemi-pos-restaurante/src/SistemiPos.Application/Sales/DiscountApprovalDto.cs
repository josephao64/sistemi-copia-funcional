using SistemiPos.Domain.Sales;

namespace SistemiPos.Application.Sales;

public sealed record DiscountApprovalDto(
    Guid Id,
    Guid CashSessionId,
    string RequestedByUserName,
    DateTimeOffset RequestedAtUtc,
    DiscountApprovalStatus Status,
    string? ResolvedByUserName,
    DateTimeOffset? ResolvedAtUtc,
    string? DecisionComment,
    decimal GrossTotal,
    decimal DiscountTotal,
    decimal NetTotal,
    IReadOnlyList<DiscountApprovalItemDto> Items);

public sealed record DiscountApprovalItemDto(
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    SaleDiscountType DiscountType,
    decimal DiscountValue,
    decimal GrossAmount,
    decimal DiscountAmount,
    string? Comment);
