using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Catalog;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.InternalCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Barcode).HasMaxLength(100);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SupplierInvoiceName).HasMaxLength(300);
        builder.Property(x => x.SupplierDescription).HasMaxLength(800);
        builder.Property(x => x.VariantValue).HasMaxLength(300);
        builder.Property(x => x.ManagesVariants).HasDefaultValue(false);
        builder.Property(x => x.ManagesExpiry).HasDefaultValue(false);
        builder.Property(x => x.ManagesLots).HasDefaultValue(true);
        builder.Property(x => x.ExpiryAlertDays);
        builder.Property(x => x.Description).HasMaxLength(800);
        builder.Property(x => x.ImageUrl).HasMaxLength(500);
        builder.Property(x => x.Brand).HasMaxLength(150).IsRequired();
        builder.Property(x => x.UnitOfMeasure).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastCost).HasColumnType("numeric(18,6)");
        builder.Property(x => x.PreviousCost).HasColumnType("numeric(18,6)");
        builder.Property(x => x.SalePrice).HasColumnType("numeric(18,2)");
        builder.Property(x => x.MinimumStock).HasColumnType("numeric(18,2)");
        builder.Property(x => x.AppliesTax).HasDefaultValue(true);
        builder.Property(x => x.NoMovementDaysOverride);
        builder.Property(x => x.MultiPriceEnabled).HasDefaultValue(false);
        builder.Property(x => x.MultiPriceMode).HasConversion<string>().HasMaxLength(30).HasDefaultValue(MultiPriceMode.MinimumQuantity);
        builder.Property(x => x.ArticleType).HasConversion<string>().HasMaxLength(30).HasDefaultValue(ArticleType.Reventa);
        builder.Property(x => x.BaseUnitId);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);

        builder.HasOne(x => x.BaseUnit)
            .WithMany()
            .HasForeignKey(x => x.BaseUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Category)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ParentProduct)
            .WithMany()
            .HasForeignKey(x => x.ParentProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ParentProductId);

        builder.HasMany(x => x.VariantOptions)
            .WithOne(x => x.Product)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.VariantOptions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(x => x.BranchSettings).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.PurchasePresentations).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.UnitConversions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => x.InternalCode).IsUnique();
        builder.HasIndex(x => x.Barcode)
            .IsUnique()
            .HasFilter("\"Barcode\" IS NOT NULL");
    }
}

internal sealed class VariantTypeConfiguration : IEntityTypeConfiguration<VariantType>
{
    public void Configure(EntityTypeBuilder<VariantType> builder)
    {
        builder.ToTable("VariantTypes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);
        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasMany(x => x.Options).WithOne(x => x.VariantType).HasForeignKey(x => x.VariantTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Options).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class VariantOptionConfiguration : IEntityTypeConfiguration<VariantOption>
{
    public void Configure(EntityTypeBuilder<VariantOption> builder)
    {
        builder.ToTable("VariantOptions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Value).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);
        builder.HasIndex(x => new { x.VariantTypeId, x.Value }).IsUnique();
    }
}

internal sealed class ProductVariantOptionConfiguration : IEntityTypeConfiguration<ProductVariantOption>
{
    public void Configure(EntityTypeBuilder<ProductVariantOption> builder)
    {
        builder.ToTable("ProductVariantOptions");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ProductId, x.VariantOptionId }).IsUnique();
        builder.HasOne(x => x.VariantOption).WithMany().HasForeignKey(x => x.VariantOptionId).OnDelete(DeleteBehavior.Restrict);
    }
}
