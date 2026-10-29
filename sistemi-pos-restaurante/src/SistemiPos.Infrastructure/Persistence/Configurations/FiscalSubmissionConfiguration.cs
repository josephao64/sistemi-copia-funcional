using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Sales;

namespace SistemiPos.Infrastructure.Persistence.Configurations;
internal sealed class FiscalSubmissionConfiguration : IEntityTypeConfiguration<FiscalSubmission>
{
    public void Configure(EntityTypeBuilder<FiscalSubmission> b)
    {
        b.ToTable("FiscalSubmissions"); b.HasKey(x => x.Id);
        b.HasIndex(x => x.SaleId).IsUnique(); b.HasIndex(x => x.IdempotencyKey).IsUnique();
        b.HasIndex(x => new { x.BranchId, x.Status });
        b.Property(x => x.IdempotencyKey).HasMaxLength(100);
        b.Property(x => x.PayloadJson).HasColumnType("jsonb");
        b.Property(x => x.Message).HasMaxLength(500);
        b.Property(x => x.Version).IsRowVersion();
        b.HasOne<Sale>().WithOne().HasForeignKey<FiscalSubmission>(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
    }
}
