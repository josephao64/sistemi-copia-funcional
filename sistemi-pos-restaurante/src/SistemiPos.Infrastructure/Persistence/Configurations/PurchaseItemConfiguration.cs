using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Purchases;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseItemConfiguration : IEntityTypeConfiguration<PurchaseItem>
{
    public void Configure(EntityTypeBuilder<PurchaseItem> builder)
    {
        builder.ToTable("PurchaseItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BaseQuantityPerPackage).HasColumnType("numeric(18,4)");
        builder.Property(x => x.BaseUnitCode).HasMaxLength(100);
        builder.Property(x => x.ProductPresentationId);

        builder.Property(x => x.Quantity).HasColumnType("numeric(18,2)");
        builder.Property(x => x.UnitCost).HasColumnType("numeric(18,6)");
        builder.Property(x => x.Subtotal).HasColumnType("numeric(18,2)");
        builder.Property(x => x.PackageType).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.PackageQuantity).HasColumnType("numeric(18,2)");
        builder.Property(x => x.PackageCost).HasColumnType("numeric(18,2)");
        builder.Property(x => x.PackageName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.GrossSubtotal).HasColumnType("numeric(18,2)");
        builder.Property(x => x.LineDiscountType).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.LineDiscountValue).HasColumnType("numeric(18,2)");
        builder.Property(x => x.LineDiscountAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.GlobalDiscountAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.SupplierProductName).HasMaxLength(300);
        builder.Property(x => x.SupplierProductCode).HasMaxLength(100);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
