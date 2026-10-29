using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Purchases;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class ProductSupplierConfiguration : IEntityTypeConfiguration<ProductSupplier>
{
    public void Configure(EntityTypeBuilder<ProductSupplier> builder)
    {
        builder.ToTable("ProductSuppliers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SupplierProductName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.SupplierProductCode).HasMaxLength(100);
        builder.Property(x => x.PackageType).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.PackageName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastPackageCost).HasColumnType("numeric(18,2)");
        builder.Property(x => x.LastUnitCost).HasColumnType("numeric(18,6)");
        builder.Property(x => x.Terms).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);
        builder.HasIndex(x => new { x.ProductId, x.SupplierId }).IsUnique();
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SupplierProductPriceHistoryConfiguration : IEntityTypeConfiguration<SupplierProductPriceHistory>
{
    public void Configure(EntityTypeBuilder<SupplierProductPriceHistory> builder)
    {
        builder.ToTable("SupplierProductPriceHistory");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PackageType).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.PackageQuantity).HasColumnType("numeric(18,2)");
        builder.Property(x => x.PackageCost).HasColumnType("numeric(18,2)");
        builder.Property(x => x.TotalCost).HasColumnType("numeric(18,2)");
        builder.Property(x => x.PackageName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LineDiscountAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.GlobalDiscountAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.UnitCost).HasColumnType("numeric(18,6)");
        builder.HasIndex(x => new { x.ProductId, x.SupplierId, x.PurchaseDateUtc });
        builder.HasOne(x => x.ProductSupplier).WithMany().HasForeignKey(x => x.ProductSupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Purchase).WithMany().HasForeignKey(x => x.PurchaseId).OnDelete(DeleteBehavior.Restrict);
    }
}
