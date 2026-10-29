using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Organizations;

namespace SistemiPos.UnitTests.Organizations;

public sealed class CompanySettingTests
{
    [Fact]
    public void Constructor_Requires_CompanyId()
    {
        Assert.Throws<DomainRuleException>(() => new CompanySetting(Guid.Empty, "Empresa Demo"));
    }

    [Fact]
    public void Constructor_Requires_BusinessName()
    {
        Assert.Throws<DomainRuleException>(() => new CompanySetting(Guid.NewGuid(), "   "));
    }

    [Fact]
    public void Constructor_Rejects_Invalid_Currency_Separators()
    {
        Assert.Throws<DomainRuleException>(() => new CompanySetting(
            Guid.NewGuid(),
            "Empresa Demo",
            decimalSeparator: ",",
            thousandsSeparator: ","));
    }

    [Fact]
    public void Constructor_Rejects_Negative_Tax_Or_Discount()
    {
        Assert.Throws<DomainRuleException>(() => new CompanySetting(
            Guid.NewGuid(),
            "Empresa Demo",
            taxPercent: -5m));

        Assert.Throws<DomainRuleException>(() => new CompanySetting(
            Guid.NewGuid(),
            "Empresa Demo",
            defaultMaxDiscountPercent: -1m));
    }

    [Fact]
    public void Valid_Creation_Sets_Default_Values_Properly()
    {
        var companyId = Guid.NewGuid();
        var setting = new CompanySetting(
            companyId,
            " Mi Empresa Demo ",
            businessNit: "123456-7",
            taxPercent: 12m);

        Assert.Equal(companyId, setting.CompanyId);
        Assert.Equal("Mi Empresa Demo", setting.BusinessName);
        Assert.Equal("123456-7", setting.BusinessNit);
        Assert.Equal("GTQ", setting.CurrencyCode);
        Assert.Equal("Q", setting.CurrencySymbol);
        Assert.True(setting.CashPaymentsEnabled);
        Assert.True(setting.TransferPaymentsEnabled);
        Assert.True(setting.PosPaymentsEnabled);
        Assert.True(setting.CreditPaymentsEnabled);
    }

    [Fact]
    public void Update_Modifies_Company_Settings()
    {
        var setting = new CompanySetting(Guid.NewGuid(), "Original");
        setting.Update(
            "Nuevo Nombre",
            businessNit: "987654-3",
            businessAddress: "Calle 1, Ciudad",
            businessPhone: "5555-5555",
            businessLogoUrl: "/logo.png",
            currencyCode: "USD",
            currencyName: "Dólar",
            currencySymbol: "$",
            currencyDecimals: 2,
            decimalSeparator: ".",
            thousandsSeparator: ",",
            currencySymbolPosition: CurrencySymbolPosition.Before,
            taxPercent: 10m,
            showTaxOnReceipt: false,
            receiptFooter: "Gracias por venir",
            cashPaymentsEnabled: true,
            transferPaymentsEnabled: false,
            posPaymentsEnabled: true,
            creditPaymentsEnabled: false,
            discountsEnabled: true,
            defaultMaxDiscountPercent: 15m);

        Assert.Equal("Nuevo Nombre", setting.BusinessName);
        Assert.Equal("USD", setting.CurrencyCode);
        Assert.Equal("$", setting.CurrencySymbol);
        Assert.Equal(10m, setting.TaxPercent);
        Assert.False(setting.ShowTaxOnReceipt);
        Assert.False(setting.TransferPaymentsEnabled);
        Assert.True(setting.DiscountsEnabled);
        Assert.Equal(15m, setting.DefaultMaxDiscountPercent);
    }

    [Fact]
    public void ConfigurePaymentMethods_Updates_Flags()
    {
        var setting = new CompanySetting(Guid.NewGuid(), "Empresa");
        setting.ConfigurePaymentMethods(
            cashPaymentsEnabled: true,
            transferPaymentsEnabled: false,
            posPaymentsEnabled: true,
            creditPaymentsEnabled: false);

        Assert.True(setting.CashPaymentsEnabled);
        Assert.False(setting.TransferPaymentsEnabled);
        Assert.True(setting.PosPaymentsEnabled);
        Assert.False(setting.CreditPaymentsEnabled);
    }

    [Fact]
    public void SelectCurrency_Applies_CurrencyDefinition()
    {
        var setting = new CompanySetting(Guid.NewGuid(), "Empresa");
        var currency = new CurrencyDefinition("EUR", "Euro", "€", 2, ",", ".", CurrencySymbolPosition.After);

        setting.SelectCurrency(currency);

        Assert.Equal("EUR", setting.CurrencyCode);
        Assert.Equal("Euro", setting.CurrencyName);
        Assert.Equal("€", setting.CurrencySymbol);
        Assert.Equal(CurrencySymbolPosition.After, setting.CurrencySymbolPosition);
    }
}
