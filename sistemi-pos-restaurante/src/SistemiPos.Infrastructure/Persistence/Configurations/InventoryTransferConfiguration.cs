using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Inventory;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class InventoryTransferConfiguration : IEntityTypeConfiguration<InventoryTransfer>
{
    public void Configure(EntityTypeBuilder<InventoryTransfer> builder)
    {
        builder.ToTable("InventoryTransfers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Number).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.RequestedBy).HasMaxLength(256).IsRequired();
        builder.Property(x => x.DispatchedBy).HasMaxLength(256);
        builder.Property(x => x.ReceivedBy).HasMaxLength(256);
        builder.Property(x => x.CancelledBy).HasMaxLength(256);
        builder.Property(x => x.CancellationReason).HasMaxLength(500);
        builder.Property(x => x.Observations).HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);
        builder.Property(x => x.Version).IsRowVersion();

        builder.HasIndex(x => x.Number).IsUnique();
        builder.HasIndex(x => x.OriginWarehouseId);
        builder.HasIndex(x => x.DestinationWarehouseId);
        builder.HasIndex(x => x.Status);

        builder.HasOne(x => x.OriginWarehouse)
            .WithMany()
            .HasForeignKey(x => x.OriginWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DestinationWarehouse)
            .WithMany()
            .HasForeignKey(x => x.DestinationWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.TransferId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class InventoryTransferItemConfiguration : IEntityTypeConfiguration<InventoryTransferItem>
{
    public void Configure(EntityTypeBuilder<InventoryTransferItem> builder)
    {
        builder.ToTable("InventoryTransferItems");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.QuantitySent).HasColumnType("numeric(22,6)").IsRequired();
        builder.Property(x => x.QuantityReceived).HasColumnType("numeric(22,6)").IsRequired();
        builder.Property(x => x.UnitCostOrigin).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.AssignedUnitCost).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.LotNumber).HasMaxLength(100);
        builder.Property(x => x.ExpiryDate).HasColumnType("date");

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<InventoryExpiryLot>()
            .WithMany()
            .HasForeignKey(x => x.OriginLotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TransferId, x.ProductId }).IsUnique();
        builder.HasIndex(x => x.OriginLotId);
    }
}
