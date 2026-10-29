using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Organizations;
using SistemiPos.Domain.Sales;
using SistemiPos.Infrastructure.Sales.PdfJobs;
using SistemiPos.Infrastructure.Security;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class SalePdfJobConfiguration : IEntityTypeConfiguration<SalePdfJob>
{
    public void Configure(EntityTypeBuilder<SalePdfJob> b)
    {
        b.ToTable("SalePdfJobs"); b.HasKey(x => x.Id);
        b.Property(x => x.RequestKey).HasMaxLength(80).IsRequired();
        b.Property(x => x.SaleNumber).HasMaxLength(50).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.HasIndex(x => new { x.RequestedByUserId, x.BranchId, x.SaleId, x.RequestKey }).IsUnique();
        b.HasOne<Sale>().WithMany().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class SalePdfOutboxConfiguration : IEntityTypeConfiguration<SalePdfOutbox>
{
    public void Configure(EntityTypeBuilder<SalePdfOutbox> b)
    {
        b.ToTable("SalePdfOutbox"); b.HasKey(x => x.Id);
        b.HasIndex(x => x.JobId).IsUnique();
        b.HasIndex(x => x.AvailableAtUtc).HasFilter("\"PublishedAtUtc\" IS NULL");
        b.HasOne<SalePdfJob>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
    }
}
