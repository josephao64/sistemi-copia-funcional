namespace SistemiPos.Application.Catalog;

public sealed record ProductPriceRuleDto(
    Guid Id,
    decimal Quantity,
    decimal Price,
    bool IsActive);
