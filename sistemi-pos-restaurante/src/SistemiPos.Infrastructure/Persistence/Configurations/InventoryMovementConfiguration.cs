using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Purchases;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ToTable("InventoryMovements");
        builder.HasKey(x => x.Id);
        builder.HasOne<SistemiPos.Domain.Orders.RestaurantOrderEvent>().WithMany()
            .HasForeignKey(x => x.RestaurantOrderEventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.RestaurantOrderEventId, x.ProductId }).IsUnique()
            .HasFilter("\"RestaurantOrderEventId\" IS NOT NULL");
        builder.Ignore(x => x.EntryExpiryDate);
        builder.Ignore(x => x.EntryLotNumber);
        builder.Ignore(x => x.SelectedLotId);
        builder.Ignore(x => x.OrderReturnId);

        builder.Property(x => x.Quantity).HasColumnType("numeric(22,6)");
        builder.Property(x => x.PreviousStock).HasColumnType("numeric(22,6)");
        builder.Property(x => x.NewStock).HasColumnType("numeric(22,6)");
        builder.Property(x => x.UnitCost).HasColumnType("numeric(22,6)");
        builder.Property(x => x.UserName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.SourceDocument).HasMaxLength(50);
        builder.Property(x => x.Note).HasMaxLength(500);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PhysicalInventoryCount)
            .WithMany()
            .HasForeignKey(x => x.PhysicalInventoryCountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PhysicalInventoryCountItem)
            .WithMany()
            .HasForeignKey(x => x.PhysicalInventoryCountItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PurchaseOrder>()
            .WithMany()
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Purchase>()
            .WithMany()
            .HasForeignKey(x => x.PurchaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Transfer)
            .WithMany()
            .HasForeignKey(x => x.TransferId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TransferItem)
            .WithMany()
            .HasForeignKey(x => x.TransferItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PurchaseOrderId);
        builder.HasIndex(x => x.PurchaseId);
        builder.HasIndex(x => x.WarehouseId);
        builder.HasIndex(x => x.TransferId);
        builder.HasIndex(x => x.TransferItemId);
    }
}
