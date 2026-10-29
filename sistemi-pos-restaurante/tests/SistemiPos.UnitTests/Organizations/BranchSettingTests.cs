using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Organizations;

namespace SistemiPos.UnitTests.Organizations;

public sealed class BranchSettingTests
{
    [Fact]
    public void Constructor_Requires_BranchId()
    {
        Assert.Throws<DomainRuleException>(() => new BranchSetting(Guid.Empty));
    }

    [Fact]
    public void Constructor_Sets_Defaults_Correctly()
    {
        var branchId = Guid.NewGuid();
        var setting = new BranchSetting(branchId);

        Assert.Equal(branchId, setting.BranchId);
        Assert.Null(setting.EstablishmentCode);
        Assert.Null(setting.EstablishmentName);
        Assert.Null(setting.ReceiptFooter);
        Assert.False(setting.OverridePaymentMethods);
        Assert.True(setting.CashPaymentsEnabled);
        Assert.True(setting.TransferPaymentsEnabled);
        Assert.True(setting.PosPaymentsEnabled);
        Assert.True(setting.CreditPaymentsEnabled);
    }

    [Fact]
    public void Update_Modifies_Values()
    {
        var setting = new BranchSetting(Guid.NewGuid());
        setting.Update(
            "EST01",
            "Sucursal Zona 10",
            "Gracias por visitar Zona 10",
            overridePaymentMethods: true,
            cashPaymentsEnabled: true,
            transferPaymentsEnabled: false,
            posPaymentsEnabled: true,
            creditPaymentsEnabled: false);

        Assert.Equal("EST01", setting.EstablishmentCode);
        Assert.Equal("Sucursal Zona 10", setting.EstablishmentName);
        Assert.Equal("Gracias por visitar Zona 10", setting.ReceiptFooter);
        Assert.True(setting.OverridePaymentMethods);
        Assert.True(setting.CashPaymentsEnabled);
        Assert.False(setting.TransferPaymentsEnabled);
        Assert.True(setting.PosPaymentsEnabled);
        Assert.False(setting.CreditPaymentsEnabled);
    }

    [Fact]
    public void ConfigurePaymentMethods_Updates_Payment_Settings()
    {
        var setting = new BranchSetting(Guid.NewGuid());
        setting.ConfigurePaymentMethods(
            overridePayments: true,
            cash: false,
            transfer: true,
            pos: false,
            credit: true);

        Assert.True(setting.OverridePaymentMethods);
        Assert.False(setting.CashPaymentsEnabled);
        Assert.True(setting.TransferPaymentsEnabled);
        Assert.False(setting.PosPaymentsEnabled);
        Assert.True(setting.CreditPaymentsEnabled);
    }
}
