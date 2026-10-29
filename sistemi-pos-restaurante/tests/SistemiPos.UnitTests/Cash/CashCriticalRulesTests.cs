using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Finance;

namespace SistemiPos.UnitTests.Cash;

public sealed class CashCriticalRulesTests
{
    [Fact]
    public void Opening_Currency_Snapshot_Is_Immutable_And_Preserved_After_Closing()
    {
        var setting = SistemiPos.Domain.Configuration.SystemSetting.CreateDefault();
        var session = new CashSession("C1", "tester", DateTimeOffset.UtcNow, 10);
        session.CaptureCurrency(setting);
        var snapshot = session.CurrencySnapshotJson;
        Assert.Equal(setting.CurrencyCode, session.CurrencyCode);
        Assert.Throws<DomainRuleException>(() => session.CaptureCurrency(setting));
        session.Close(DateTimeOffset.UtcNow, 10);
        Assert.Equal(snapshot, session.CurrencySnapshotJson);
    }
    [Fact]
    public void ExpectedCash_Includes_Collections_And_Provider_Payments()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new CashSession("ignored", "cashier", now, 100m);
        session.AddMovement(new CashMovement(CashMovementType.SaleCash, 50m, "Venta", "cashier", now));
        session.AddMovement(new CashMovement(CashMovementType.CollectionCash, 25m, "Abono", "cashier", now));
        session.AddMovement(new CashMovement(CashMovementType.SupplierPayment, 30m, "Proveedor", "cashier", now));
        Assert.Equal(145m, session.CalculateExpectedCash());
    }

    [Fact]
    public void ClosedSession_Is_Immutable()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new CashSession("ignored", "cashier", now, 100m);
        session.Close(now.AddHours(1), 100m);
        Assert.Throws<DomainRuleException>(() => session.AddMovement(new CashMovement(CashMovementType.OtherIncome, 1m, "Ingreso", "cashier", now)));
    }

    [Fact]
    public void PettyCash_Does_Not_Allow_Negative_Balance()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var fund = new PettyCashFund("Caja chica Administración", userId, "user");
        var cycle = fund.Open(50m, userId, "user", now);
        Assert.Throws<DomainRuleException>(() => fund.AddMovement(new PettyCashMovement(PettyCashMovementType.Expense, 60m, "Gasto", null, null, null, null, "user", now, cycleId: cycle.Id)));
    }

    [Fact]
    public void PettyCash_Complete_Flow_Separates_Cycles_And_Enforces_Responsible()
    {
        var now = new DateTimeOffset(2026, 9, 2, 14, 0, 0, TimeSpan.Zero);
        var responsibleId = Guid.NewGuid();
        var fund = new PettyCashFund("Caja chica Sucursal Jalapa", responsibleId, "responsable");
        var firstCycle = fund.Open(100m, responsibleId, "responsable", now);
        var expense = fund.AddMovement(new PettyCashMovement(PettyCashMovementType.Expense, 25m, "Papelería", "Oficina", "F-1", "Proveedor", null, "responsable", now.AddMinutes(5), cycleId: firstCycle.Id));

        Assert.Equal(75m, fund.Balance);
        Assert.Equal(firstCycle.Id, expense.CycleId);
        Assert.Throws<DomainRuleException>(() => fund.AddMovement(new PettyCashMovement(PettyCashMovementType.Expense, 1m, "No autorizado", null, null, null, null, "otro", now, cycleId: firstCycle.Id)));

        fund.Close(firstCycle, 75m, responsibleId, "responsable", now.AddHours(1), "Cuadrada");
        var secondCycle = fund.Open(40m, responsibleId, "responsable", now.AddHours(2));
        var replenishment = fund.AddMovement(new PettyCashMovement(PettyCashMovementType.Replenishment, 10m, "Reposición", null, null, null, null, "responsable", now.AddHours(2).AddMinutes(5), cycleId: secondCycle.Id));

        Assert.NotEqual(firstCycle.Id, secondCycle.Id);
        Assert.Equal(secondCycle.Id, replenishment.CycleId);
        Assert.Equal(50m, fund.Balance);
    }
}
