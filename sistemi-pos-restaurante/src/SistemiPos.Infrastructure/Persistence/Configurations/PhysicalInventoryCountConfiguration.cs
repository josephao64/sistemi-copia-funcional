using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Inventory;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class PhysicalInventoryCountConfiguration : IEntityTypeConfiguration<PhysicalInventoryCount>
{
    public void Configure(EntityTypeBuilder<PhysicalInventoryCount> builder)
    {
        builder.ToTable("PhysicalInventoryCounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Scope).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.FinalizedBy).HasMaxLength(256);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);
        builder.HasIndex(x => x.WarehouseId);

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Categories)
            .WithOne(x => x.PhysicalInventoryCount)
            .HasForeignKey(x => x.PhysicalInventoryCountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.PhysicalInventoryCount)
            .HasForeignKey(x => x.PhysicalInventoryCountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
