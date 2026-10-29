using Microsoft.EntityFrameworkCore;
using SistemiPos.Domain.Audit;
using SistemiPos.Domain.Sales;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.IntegrationTests.Infrastructure;
public sealed class PhaseEightModelTests
{
    [Fact]
    public void Fiscal_submission_is_unique_per_sale_and_contextual_audit_is_indexed()
    {
        using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=ModelOnly;Username=postgres;Password=unused").Options);
        var fiscal=db.Model.FindEntityType(typeof(FiscalSubmission))!;
        Assert.Contains(fiscal.GetIndexes(),x=>x.IsUnique&&x.Properties.Select(p=>p.Name).SequenceEqual(["SaleId"]));
        Assert.True(fiscal.FindProperty("Version")!.IsConcurrencyToken);
        Assert.Equal("jsonb",fiscal.FindProperty("PayloadJson")!.GetColumnType());
        var audit=db.Model.FindEntityType(typeof(AuditLog))!;
        Assert.Contains(audit.GetIndexes(),x=>x.Properties.Select(p=>p.Name).SequenceEqual(["BranchId","OccurredAtUtc"]));
    }
}
