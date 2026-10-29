using SistemiPos.Application.Catalog;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.UnitTests.Catalog;

public sealed class ProductPricingServiceTests
{
    private readonly ProductPricingService _service = new();

    [Theory]
    [InlineData(30, 60)]
    [InlineData(0, 0)]
    public void Calculate_Uses_Branch_BasePrice_Without_Changing_GlobalPrice(decimal localPrice, decimal gross)
    {
        var product = CreateProduct();
        var originalPrice = product.SalePrice;
        var branch = Guid.NewGuid();
        ((ICollection<ProductBranchSetting>)product.BranchSettings).Add(new ProductBranchSetting(product.Id, branch, true, true, localPrice, null));
        var quote = _service.Calculate(product, 2, branch);
        Assert.Equal(localPrice, quote.UnitPrice);
        Assert.Equal(gross, quote.GrossAmount);
        Assert.Equal(originalPrice, product.SalePrice);
        Assert.Equal(originalPrice, _service.Calculate(product, 2, Guid.NewGuid()).UnitPrice);
    }

    [Fact]
    public void Calculate_Branch_BasePrice_Preserves_MultiPrice_Priority_And_Fallback()
    {
        var product = CreateProduct();
        var branch = Guid.NewGuid();
        ((ICollection<ProductBranchSetting>)product.BranchSettings).Add(new ProductBranchSetting(product.Id, branch, true, true, 3, null));
        product.ConfigureMultiPricing(true, MultiPriceMode.MinimumQuantity);
        product.SynchronizePriceRules([new(null, 10, 1, true)]);
        Assert.Equal(3, _service.Calculate(product, 2, branch).UnitPrice);
        Assert.Equal(1, _service.Calculate(product, 10, branch).UnitPrice);
    }

    [Theory]
    [InlineData(5, 1.25, 6.25)]
    [InlineData(10, 1.00, 10.00)]
    [InlineData(15, 1.00, 15.00)]
    [InlineData(20, 0.90, 18.00)]
    [InlineData(37, 0.90, 33.30)]
    [InlineData(50, 0.80, 40.00)]
    public void Calculate_MinimumQuantity_Uses_Highest_Applicable_Active_Rule(
        decimal quantity,
        decimal expectedUnitPrice,
        decimal expectedGrossAmount)
    {
        var product = CreateProduct();
        product.ConfigureMultiPricing(true, MultiPriceMode.MinimumQuantity);
        product.SynchronizePriceRules(
        [
            new(null, 10m, 1m, true),
            new(null, 20m, .90m, true),
            new(null, 50m, .80m, true),
            new(null, 30m, .10m, false)
        ]);

        var quote = _service.Calculate(product, quantity);

        Assert.Equal(expectedUnitPrice, quote.UnitPrice);
        Assert.Equal(expectedGrossAmount, quote.GrossAmount);
    }

    [Fact]
    public void Calculate_ExactQuantity_Only_Applies_On_Exact_Match()
    {
        var product = CreateProduct();
        product.ConfigureMultiPricing(true, MultiPriceMode.ExactQuantity);
        product.SynchronizePriceRules(
        [
            new(null, 10m, 10m, true),
            new(null, 20m, 18m, true),
            new(null, 50m, 40m, true)
        ]);

        var exact = _service.Calculate(product, 10m);
        var nonExact = _service.Calculate(product, 15m);

        Assert.Equal(10m, exact.GrossAmount);
        Assert.Equal(1m, exact.UnitPrice);
        Assert.Equal(MultiPriceMode.ExactQuantity, exact.AppliedMode);
        Assert.Equal(18.75m, nonExact.GrossAmount);
        Assert.Null(nonExact.AppliedRuleId);
    }

    [Fact]
    public void Calculate_When_Disabled_Ignores_Rules_Without_Deleting_Them()
    {
        var product = CreateProduct();
        product.ConfigureMultiPricing(true, MultiPriceMode.MinimumQuantity);
        product.SynchronizePriceRules([new(null, 10m, 1m, true)]);
        product.ConfigureMultiPricing(false, MultiPriceMode.MinimumQuantity);

        var quote = _service.Calculate(product, 10m);

        Assert.Equal(12.50m, quote.GrossAmount);
        Assert.Single(product.PriceRules);
    }

    [Fact]
    public void SynchronizePriceRules_Rejects_Repeated_Quantity()
    {
        var product = CreateProduct();

        Assert.Throws<DomainRuleException>(() => product.SynchronizePriceRules(
        [
            new(null, 10m, 1m, true),
            new(null, 10m, .90m, true)
        ]));
    }

    [Fact]
    public void ProductPriceRule_Rejects_NonPositive_Quantity_And_Negative_Price()
    {
        var productId = Guid.NewGuid();

        Assert.Throws<DomainRuleException>(() => new ProductPriceRule(productId, 0m, 1m));
        Assert.Throws<DomainRuleException>(() => new ProductPriceRule(productId, 1m, -0.01m));
    }

    private static Product CreateProduct() =>
        new("P-001", null, "Producto", null, Guid.NewGuid(), "Marca", "Unidad", 1m, 1m, 1.25m, 1m, true);
}
