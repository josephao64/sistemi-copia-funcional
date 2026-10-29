using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Sales;

namespace SistemiPos.UnitTests.Sales;

public sealed class MixedPaymentTests
{
    private static Sale Sale(decimal total, params SalePayment[] payments) => new("V-TEST", Guid.NewGuid(), null,
        DateTimeOffset.UtcNow, "tester", [new SaleItem(Guid.NewGuid(), "Producto", 1, total, 0, 0, true)],
        payments[0], "GTQ", "Quetzal", "Q", 2, ".", ",", "Before", payments: payments);

    [Fact]
    public void Mixed_Payments_Apply_50_Cash_39_Card_And_Keep_Change_Separate()
    {
        var cash = SalePayment.CreateCash(50, 60, "tester");
        var card = SalePayment.CreatePos(39, CardType.Debit, null, "AUTH", "VOUCHER", 39, CardPaymentStatus.Approved, "tester", Guid.NewGuid());
        var sale = Sale(89, cash, card);
        Assert.Equal(PaymentMethodType.Mixed, sale.Payment.PaymentMethod);
        Assert.Equal(89, sale.Payments.Sum(x => x.TotalAmount));
        Assert.Equal(10, cash.ChangeAmount);
        Assert.Equal(2, sale.Payments.Count);
    }

    [Fact]
    public void Mixed_Payments_Reject_Insufficient_Excess_And_Credit()
    {
        Assert.Throws<DomainRuleException>(() => Sale(89, SalePayment.CreateCash(50, 50, "tester"), SalePayment.CreateCash(38, 38, "tester")));
        Assert.Throws<DomainRuleException>(() => Sale(89, SalePayment.CreateCash(50, 50, "tester"), SalePayment.CreateCash(40, 40, "tester")));
        Assert.Throws<DomainRuleException>(() => Sale(89, SalePayment.CreateCash(50, 50, "tester"), SalePayment.CreateCredit(39, "tester")));
    }

    [Fact]
    public void Configured_NonCash_Method_Preserves_Its_Name_And_Does_Not_Increase_Cash()
    {
        var definition = new SistemiPos.Domain.Configuration.PaymentMethodDefinition("VALE", "Vale", SistemiPos.Domain.Configuration.PaymentFinancialBehavior.OtherNonCash);
        var payment = SalePayment.CreateOther(39, definition, "tester", "VALE-001");
        var sale = Sale(89, SalePayment.CreateCash(50, 50, "tester"), payment);
        definition.Update("Nombre nuevo", SistemiPos.Domain.Configuration.PaymentFinancialBehavior.OtherNonCash, false);
        Assert.Equal("Vale", payment.DefinitionName);
        var session = new SistemiPos.Domain.Cash.CashSession("C1", "tester", DateTimeOffset.UtcNow, 0);
        session.AddMovement(new(SistemiPos.Domain.Cash.CashMovementType.SaleCash, 50, "Venta", "tester", DateTimeOffset.UtcNow));
        session.AddMovement(new(SistemiPos.Domain.Cash.CashMovementType.SaleOther, 39, "Venta", "tester", DateTimeOffset.UtcNow));
        session.Close(DateTimeOffset.UtcNow, 50);
        Assert.Equal(50, session.ExpectedCash); Assert.Equal(89, session.TotalSold); Assert.Equal(39, session.TotalOtherSales);
        Assert.Equal(89, sale.Total);
    }

    [Fact]
    public void Division_Assigns_The_Residual_To_The_Last_Person()
    {
        Assert.Equal(new[] { 33.33m, 33.33m, 33.34m }, SaleAccountPart.Divide(100, 3));
    }

    [Fact]
    public void Every_Division_Must_Have_Its_Exact_Payments_And_Cannot_Be_Replaced()
    {
        var sale = Sale(100, SalePayment.CreateCash(40, 40, "tester"), SalePayment.CreateCash(60, 60, "tester"));
        Assert.Throws<DomainRuleException>(() => sale.SplitAccount([50, 50], [1, 2]));
        sale.SplitAccount([40, 60], [1, 2]);
        Assert.Equal(2, sale.AccountParts.Count);
        Assert.All(sale.Payments, x => Assert.NotNull(x.AccountPartId));
        Assert.Throws<DomainRuleException>(() => sale.SplitAccount([100], [1, 1]));
    }
}
