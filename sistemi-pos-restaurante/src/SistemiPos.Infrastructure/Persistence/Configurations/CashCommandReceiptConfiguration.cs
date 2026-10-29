using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Cash;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class CashCommandReceiptConfiguration : IEntityTypeConfiguration<CashCommandReceipt>
{
    public void Configure(EntityTypeBuilder<CashCommandReceipt> b)
    {
        b.ToTable("CashCommandReceipts");
        b.HasKey(x => x.ScopeKey);
        b.Property(x => x.ScopeKey).HasMaxLength(64);
        b.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired();
        b.HasOne<CashSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
    }
}
