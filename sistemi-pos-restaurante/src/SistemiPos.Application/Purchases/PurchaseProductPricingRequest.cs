using SistemiPos.Domain.Catalog;

namespace SistemiPos.Application.Purchases;

public sealed record PurchaseProductPricingRequest(
    decimal SalePrice,
    bool MultiPriceEnabled,
    IReadOnlyList<PurchasePriceRuleRequest> PriceRules);

public sealed record PurchasePriceRuleRequest(decimal Quantity, decimal Price);
