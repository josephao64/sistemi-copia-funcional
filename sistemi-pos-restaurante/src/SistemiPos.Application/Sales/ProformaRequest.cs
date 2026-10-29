using SistemiPos.Domain.Sales;

namespace SistemiPos.Application.Sales;

public sealed record ProformaRequest(
    IReadOnlyList<ProformaItemRequest> Items,
    Guid? CustomerId,
    string? CustomerName,
    string? CustomerNit,
    string? CustomerAddress);

public sealed record ProformaItemRequest(
    Guid ProductId,
    decimal Quantity,
    SaleDiscountType DiscountType,
    decimal DiscountValue);
