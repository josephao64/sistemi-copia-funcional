namespace SistemiPos.Application.Catalog;

public sealed record SaveProductPriceRuleRequest(
    Guid? Id,
    decimal Quantity,
    decimal Price,
    bool IsActive);
