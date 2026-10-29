using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Configuration;

namespace SistemiPos.UnitTests.Configuration;

public sealed class SystemSettingTests
{
    [Fact]
    public void Update_Rejects_Equal_Currency_Separators()
    {
        var setting = SystemSetting.CreateDefault();

        Assert.Throws<DomainRuleException>(() =>
            setting.Update(
                "Mi negocio",
                "123",
                "Dirección",
                "5555",
                "https://example.com/logo.png",
                "GTQ",
                "Quetzal",
                "Q",
                2,
                ".",
                ".",
                CurrencySymbolPosition.Before,
                12m,
                true,
                "Gracias",
                true,
                true,
                true,
                true,
                10m));
    }

    [Fact]
    public void Update_Keeps_Cash_Enabled_When_Other_Payment_Methods_Are_Disabled()
    {
        var setting = SystemSetting.CreateDefault();

        setting.Update(
            "Mi negocio", "123", "Dirección", "5555", null, "GTQ", "Quetzal", "Q", 2, ".", ",",
            CurrencySymbolPosition.Before, 12m, true, "Gracias", false, false, false, true, 10m);

        Assert.True(setting.CashPaymentsEnabled);
    }

    [Fact]
    public void ConfigurePaymentMethods_Uses_Checkbox_Values()
    {
        var setting = SystemSetting.CreateDefault();

        setting.ConfigurePaymentMethods(transferPaymentsEnabled: false, posPaymentsEnabled: false, creditPaymentsEnabled: false);
        Assert.False(setting.TransferPaymentsEnabled);
        Assert.False(setting.PosPaymentsEnabled);
        Assert.False(setting.CreditPaymentsEnabled);

        setting.ConfigurePaymentMethods(transferPaymentsEnabled: true, posPaymentsEnabled: true, creditPaymentsEnabled: true);
        Assert.True(setting.TransferPaymentsEnabled);
        Assert.True(setting.PosPaymentsEnabled);
        Assert.True(setting.CreditPaymentsEnabled);
        Assert.True(setting.CashPaymentsEnabled);
    }
}
