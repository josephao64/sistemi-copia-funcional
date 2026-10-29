using Microsoft.EntityFrameworkCore;
using SistemiPos.Domain.Cash;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.IntegrationTests.Infrastructure;

public sealed class PosCashModelTests
{
    [Fact]
    public void CashModelKeepsTerminalUniquenessConcurrencyAndDurableIdempotency()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options);
        var cash = db.Model.FindEntityType(typeof(CashSession))!;
        Assert.True(cash.FindProperty(nameof(CashSession.Version))!.IsConcurrencyToken);
        Assert.Contains(cash.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(["BranchId", "TerminalId"]));
        Assert.True(cash.FindProperty(nameof(CashSession.TerminalId))!.IsNullable);
        var receipt = db.Model.FindEntityType(typeof(CashCommandReceipt))!;
        Assert.Equal("ScopeKey", Assert.Single(receipt.FindPrimaryKey()!.Properties).Name);
        Assert.Equal(64, receipt.FindProperty(nameof(CashCommandReceipt.Fingerprint))!.GetMaxLength());
        var movement = db.Model.FindEntityType(typeof(CashMovement))!;
        Assert.Contains(movement.GetIndexes(), x => x.IsUnique && x.GetDatabaseName() == "IX_CashMovements_AutomaticPayment");
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
