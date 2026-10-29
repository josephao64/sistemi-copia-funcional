using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.UnitTests.Catalog;

public sealed class ProductBranchSettingTests
{
    [Fact]
    public void ProductBranchSetting_Allows_Price_Override()
    {
        var productId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        var setting = new ProductBranchSetting(productId, branchId, true, true, 25.50m, 5m);

        Assert.True(setting.IsEnabled);
        Assert.True(setting.IsVisible);
        Assert.Equal(25.50m, setting.SalePrice);
        Assert.Equal(5m, setting.MinimumStock);
    }

    [Fact]
    public void ProductBranchSetting_Inherits_GlobalPrice_When_SalePrice_Is_Null()
    {
        var product = new Product("P-001", "123456", "Hamburguesa Clásica", null, Guid.NewGuid(), "Comida", "unidad", 15m, 12m, 35.00m, 5m, true);
        var branchId = Guid.NewGuid();

        var setting = new ProductBranchSetting(product.Id, branchId, true, true, null, null);

        decimal effectivePrice = setting.SalePrice ?? product.SalePrice;
        decimal effectiveMinStock = setting.MinimumStock ?? product.MinimumStock;

        Assert.Equal(35.00m, effectivePrice);
        Assert.Equal(5m, effectiveMinStock);
    }

    [Fact]
    public void ProductBranchSetting_Rejects_Negative_Prices_Or_Stock()
    {
        var productId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        Assert.Throws<DomainRuleException>(() =>
            new ProductBranchSetting(productId, branchId, true, true, -10m, 5m));

        Assert.Throws<DomainRuleException>(() =>
            new ProductBranchSetting(productId, branchId, true, true, 10m, -1m));
    }

    [Fact]
    public void ProductBranchSetting_Can_Be_Disabled_Per_Branch_Without_Affecting_Product()
    {
        var product = new Product("P-001", "123456", "Hamburguesa Clásica", null, Guid.NewGuid(), "Comida", "unidad", 15m, 12m, 35.00m, 5m, true);
        var branchCentral = Guid.NewGuid();
        var branchNorte = Guid.NewGuid();

        var settingCentral = new ProductBranchSetting(product.Id, branchCentral, isEnabled: true, isVisible: true, null, null);
        var settingNorte = new ProductBranchSetting(product.Id, branchNorte, isEnabled: false, isVisible: false, null, null);

        Assert.True(product.IsActive);
        Assert.True(settingCentral.IsEnabled);
        Assert.False(settingNorte.IsEnabled);
        Assert.False(settingNorte.IsVisible);
    }
}
