using SistemiPos.Domain.Catalog;

namespace SistemiPos.Application.Catalog;

public interface IProductPricingService
{
    ProductPriceQuote Calculate(Product product, decimal quantity, Guid? branchId = null);
}

public sealed record ProductPriceQuote(
    decimal UnitPrice,
    decimal GrossAmount,
    Guid? AppliedRuleId,
    MultiPriceMode? AppliedMode);
