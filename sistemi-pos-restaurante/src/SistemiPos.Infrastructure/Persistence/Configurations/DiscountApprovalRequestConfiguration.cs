using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Sales;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class DiscountApprovalRequestConfiguration : IEntityTypeConfiguration<DiscountApprovalRequest>
{
    public void Configure(EntityTypeBuilder<DiscountApprovalRequest> builder)
    {
        builder.ToTable("DiscountApprovalRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RequestedByUserName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ResolvedByUserName).HasMaxLength(256);
        builder.Property(x => x.DecisionComment).HasMaxLength(500);
        builder.Property(x => x.GrossTotal).HasColumnType("numeric(18,2)");
        builder.Property(x => x.DiscountTotal).HasColumnType("numeric(18,2)");
        builder.Property(x => x.NetTotal).HasColumnType("numeric(18,2)");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);
        builder.HasIndex(x => new { x.Status, x.RequestedAtUtc });
        builder.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.DiscountApprovalRequestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<SistemiPos.Domain.Cash.CashSession>().WithMany().HasForeignKey(x => x.CashSessionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DiscountApprovalItemConfiguration : IEntityTypeConfiguration<DiscountApprovalItem>
{
    public void Configure(EntityTypeBuilder<DiscountApprovalItem> builder)
    {
        builder.ToTable("DiscountApprovalItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProductName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Quantity).HasColumnType("numeric(18,4)");
        builder.Property(x => x.UnitPrice).HasColumnType("numeric(18,2)");
        builder.Property(x => x.DiscountValue).HasColumnType("numeric(18,2)");
        builder.Property(x => x.GrossAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.DiscountAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.Comment).HasMaxLength(500);
    }
}
