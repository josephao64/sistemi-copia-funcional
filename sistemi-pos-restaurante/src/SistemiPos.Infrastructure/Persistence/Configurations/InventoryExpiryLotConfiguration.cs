using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Inventory;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class InventoryExpiryLotConfiguration : IEntityTypeConfiguration<InventoryExpiryLot>
{
    public void Configure(EntityTypeBuilder<InventoryExpiryLot> builder)
    {
        builder.ToTable("InventoryExpiryLots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LotNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ReceivedQuantity).HasColumnType("numeric(22,6)");
        builder.Property(x => x.ReceivedAtUtc);
        builder.Property(x => x.IsOpeningBalance);
        builder.Property(x => x.UnitCost).HasColumnType("numeric(22,6)");
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasOne<SistemiPos.Domain.Organizations.Warehouse>().WithMany()
            .HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryExpiryLot>().WithMany()
            .HasForeignKey(x => x.OriginLotId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.ExpiryDate).HasColumnType("date");
        builder.Property(x => x.AvailableQuantity).HasColumnType("numeric(22,6)");
        builder.Property(x => x.SourceDocument).HasMaxLength(100);
        builder.HasIndex(x => new { x.ProductId, x.ExpiryDate });
        builder.HasIndex(x => x.OriginLotId);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class InventoryMovementLotConfiguration : IEntityTypeConfiguration<InventoryMovementLot>
{
    public void Configure(EntityTypeBuilder<InventoryMovementLot> builder)
    {
        builder.ToTable("InventoryMovementLots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Quantity).HasColumnType("numeric(22,6)");
        builder.HasOne(x => x.Movement).WithMany().HasForeignKey(x => x.MovementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Lot).WithMany().HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryMovementLot>().WithMany().HasForeignKey(x => x.ReversesAllocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.MovementId, x.LotId, x.ReversesAllocationId });
    }
}
