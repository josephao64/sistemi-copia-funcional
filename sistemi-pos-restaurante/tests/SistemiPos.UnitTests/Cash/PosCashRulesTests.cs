using System.Text.Json;
using SistemiPos.Application.Cash;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Sales;

namespace SistemiPos.UnitTests.Cash;

public sealed class PosCashRulesTests
{
    [Theory]
    [InlineData("-0.01")]
    [InlineData("1.001")]
    public void OpeningRejectsNegativeOrExcessPrecision(string value) => Assert.Throws<DomainRuleException>(() =>
        new CashSession("C1", "cashier", DateTimeOffset.UtcNow, decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void ZeroOpeningPersistsFixedUtcAndTerminalAcrossMidnight()
    {
        var opened = new DateTimeOffset(2026, 10, 24, 23, 0, 0, TimeSpan.Zero);
        var session = new CashSession("C1", "cashier", opened, 0);
        var terminal = Guid.NewGuid();
        session.BindTerminal(terminal, "America/Guatemala");
        session.Close(opened.AddHours(29), 0, reconciledByUserName: "cashier");
        Assert.Equal(opened, session.OpenedAtUtc);
        Assert.Equal(terminal, session.TerminalId);
        Assert.Equal("America/Guatemala", session.TimeZoneSnapshot);
        Assert.Equal(29, (session.ClosedAtUtc!.Value - session.OpenedAtUtc).TotalHours);
        Assert.Equal("cashier", session.ClosedByUserName);
        Assert.Throws<DomainRuleException>(() => session.BindTerminal(Guid.NewGuid(), "UTC"));
    }

    [Fact]
    public void CashReceived100ForSale70IsNet70AndExpectedExampleIs270()
    {
        var now = DateTimeOffset.UtcNow;
        var payment = SalePayment.CreateCash(70, 100, "cashier");
        Assert.Equal(30, payment.ChangeAmount);
        Assert.Equal(70, payment.TotalAmount);
        var session = new CashSession("C1", "cashier", now, 200);
        foreach (var (type, amount) in new[] { (CashMovementType.SaleCash, payment.TotalAmount),
            (CashMovementType.SalePos, 120m), (CashMovementType.SaleTransfer, 80m),
            (CashMovementType.OtherIncome, 30m), (CashMovementType.Expense, 20m), (CashMovementType.RefundCash, 10m) })
            session.AddMovement(new(type, amount, "Prueba", "cashier", now));
        Assert.Equal(270, session.CalculateExpectedCash());
        session.Close(now.AddDays(1), 270);
        Assert.Equal(0, session.Difference);
        Assert.Equal(270, session.ExpectedCash);
    }

    [Fact]
    public void OpeningAmountMustBePresentInJson() => Assert.Throws<JsonException>(() =>
        JsonSerializer.Deserialize<PosCashOpen>("{\"terminalId\":\"00000000-0000-0000-0000-000000000001\"}", new JsonSerializerOptions(JsonSerializerDefaults.Web)));

    [Fact]
    public void BranchPolicyOverridesCompanyAndDefaultWithoutInventedThresholds()
    {
        var branch = Guid.NewGuid(); var company = Guid.NewGuid();
        var options = new PosCashOptions();
        Assert.Null(options.For(company, branch).DifferenceApprovalThreshold);
        options.Companies[company.ToString()] = new() { DifferenceApprovalThreshold = 20 };
        Assert.Equal(20, options.For(company, branch).DifferenceApprovalThreshold);
        options.Branches[branch.ToString()] = new() { DifferenceApprovalThreshold = 10 };
        Assert.Equal(10, options.For(company, branch).DifferenceApprovalThreshold);
    }
}
