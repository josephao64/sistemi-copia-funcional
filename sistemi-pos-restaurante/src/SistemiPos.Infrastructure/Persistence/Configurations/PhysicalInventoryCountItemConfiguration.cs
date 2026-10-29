using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Inventory;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class PhysicalInventoryCountItemConfiguration : IEntityTypeConfiguration<PhysicalInventoryCountItem>
{
    public void Configure(EntityTypeBuilder<PhysicalInventoryCountItem> builder)
    {
        builder.ToTable("PhysicalInventoryCountItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.ReferenceStock).HasColumnType("numeric(22,6)");
        builder.Property(x => x.CountedStock).HasColumnType("numeric(22,6)");
        builder.Property(x => x.Difference).HasColumnType("numeric(22,6)");
        builder.Property(x => x.Comment).HasMaxLength(500);
        builder.Property(x => x.CountingStartedBy).HasMaxLength(256);
        builder.Property(x => x.CountedBy).HasMaxLength(256);
        builder.Property(x => x.OperationalStockBeforeAdjustment).HasColumnType("numeric(22,6)");
        builder.Property(x => x.AppliedAdjustment).HasColumnType("numeric(22,6)");
        builder.Property(x => x.OperationalStockAfterAdjustment).HasColumnType("numeric(22,6)");
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);

        builder.HasIndex(x => new { x.PhysicalInventoryCountId, x.ProductId }).IsUnique();
        builder.HasIndex(x => x.ProductId)
            .IsUnique()
            .HasFilter("\"IsActiveParticipation\" = TRUE");

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
