using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Catalog;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class ProductPurchasePresentationConfiguration : IEntityTypeConfiguration<ProductPurchasePresentation>
{
    public void Configure(EntityTypeBuilder<ProductPurchasePresentation> builder)
    {
        builder.ToTable("ProductPurchasePresentations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.BaseQuantityPerPackage).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);

        builder.HasOne(x => x.Product)
            .WithMany(x => x.PurchasePresentations)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.PurchaseUnit)
            .WithMany()
            .HasForeignKey(x => x.PurchaseUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ProductId, x.Name }).IsUnique();
    }
}
