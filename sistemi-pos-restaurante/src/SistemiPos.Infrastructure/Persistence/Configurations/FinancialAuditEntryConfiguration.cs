using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Audit;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class FinancialAuditEntryConfiguration : IEntityTypeConfiguration<FinancialAuditEntry>
{
    public void Configure(EntityTypeBuilder<FinancialAuditEntry> b)
    {
        b.ToTable("FinancialAuditEntries"); b.HasKey(x => x.Id); b.Property(x => x.UserName).HasMaxLength(256).IsRequired(); b.Property(x => x.Action).HasMaxLength(100).IsRequired();
        b.Property(x => x.DocumentType).HasMaxLength(100); b.Property(x => x.DocumentReference).HasMaxLength(100); b.Property(x => x.Amount).HasColumnType("numeric(18,2)"); b.Property(x => x.AuthorizedByUserName).HasMaxLength(256); b.Property(x => x.Comment).HasMaxLength(500);
        b.HasIndex(x => x.OccurredAtUtc); b.HasIndex(x => new { x.CashSessionId, x.OccurredAtUtc });
    }
}
