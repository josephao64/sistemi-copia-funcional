namespace SistemiPos.Application.Sales;

using SistemiPos.Domain.Sales;

public sealed record SaleItemRequest(
    Guid ProductId,
    decimal Quantity,
    decimal DiscountPercent,
    SaleDiscountType DiscountType = SaleDiscountType.Percentage,
    decimal? DiscountValue = null,
    string? DiscountAuthorizedByUserName = null,
    DateTimeOffset? DiscountAuthorizedAtUtc = null,
    string? DiscountComment = null,
    Guid? OrderLineId = null);
