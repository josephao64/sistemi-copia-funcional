using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Purchases;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.ToTable("Purchases");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Number).HasMaxLength(50).IsRequired();
        builder.Property(x => x.UserName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ConfirmedBy).HasMaxLength(256);
        builder.Property(x => x.Version).IsRowVersion();
        builder.Property(x => x.CancelledBy).HasMaxLength(256);
        builder.Property(x => x.CancellationReason).HasMaxLength(500);
        builder.Property(x => x.Total).HasColumnType("numeric(18,2)");
        builder.Property(x => x.GrossSubtotal).HasColumnType("numeric(18,2)");
        builder.Property(x => x.LineDiscountTotal).HasColumnType("numeric(18,2)");
        builder.Property(x => x.NetSubtotal).HasColumnType("numeric(18,2)");
        builder.Property(x => x.GlobalDiscountType).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.GlobalDiscountValue).HasColumnType("numeric(18,2)");
        builder.Property(x => x.GlobalDiscountAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.InvoiceNumber).HasMaxLength(100);
        builder.Property(x => x.Observations).HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);

        builder.HasIndex(x => x.Number).IsUnique();
        builder.HasIndex(x => x.PurchaseOrderId).HasFilter("\"PurchaseOrderId\" IS NOT NULL");
        builder.HasIndex(x => new { x.SupplierId, x.InvoiceNumber }).IsUnique().HasFilter("\"InvoiceNumber\" IS NOT NULL");
        builder.HasIndex(x => x.BranchId);
        builder.HasIndex(x => x.WarehouseId);

        builder.HasOne(x => x.Supplier)
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
