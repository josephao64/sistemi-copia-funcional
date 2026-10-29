using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Purchases;
using SistemiPos.Domain.Suppliers;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseReceiptConfiguration : IEntityTypeConfiguration<PurchaseReceipt>
{
    public void Configure(EntityTypeBuilder<PurchaseReceipt> builder)
    {
        builder.ToTable("PurchaseReceipts"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Number).HasMaxLength(50).IsRequired();
        builder.Property(x => x.SupplierDocumentNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Observations).HasMaxLength(1000);
        builder.Property(x => x.Total).HasColumnType("numeric(18,2)");
        builder.Property(x => x.CreatedBy).HasMaxLength(256); builder.Property(x => x.ModifiedBy).HasMaxLength(256);
        builder.HasIndex(x => x.Number).IsUnique(); builder.HasIndex(x => x.PurchaseOrderId); builder.HasIndex(x => x.PurchaseId).IsUnique();
        builder.HasIndex(x => x.BranchId);
        builder.HasIndex(x => x.WarehouseId);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Purchase).WithOne().HasForeignKey<PurchaseReceipt>(x => x.PurchaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.PurchaseReceiptId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PurchaseReceiptItemConfiguration : IEntityTypeConfiguration<PurchaseReceiptItem>
{
    public void Configure(EntityTypeBuilder<PurchaseReceiptItem> builder)
    {
        builder.ToTable("PurchaseReceiptItems"); builder.HasKey(x => x.Id);
        builder.Property(x => x.RequestedQuantity).HasColumnType("numeric(18,2)");
        builder.Property(x => x.ReceivedQuantity).HasColumnType("numeric(18,2)");
        builder.Property(x => x.ActualUnitCost).HasColumnType("numeric(18,6)");
        builder.Property(x => x.Subtotal).HasColumnType("numeric(18,2)");
        builder.HasIndex(x => new { x.PurchaseReceiptId, x.ProductId }).IsUnique();
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseOrderItem>().WithMany().HasForeignKey(x => x.PurchaseOrderItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
