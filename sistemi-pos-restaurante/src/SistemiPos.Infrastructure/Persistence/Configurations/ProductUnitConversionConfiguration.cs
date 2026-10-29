using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Catalog;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class ProductUnitConversionConfiguration : IEntityTypeConfiguration<ProductUnitConversion>
{
    public void Configure(EntityTypeBuilder<ProductUnitConversion> builder)
    {
        builder.ToTable("ProductUnitConversions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Factor).HasColumnType("numeric(18,6)").IsRequired();
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);

        builder.HasOne(x => x.Product)
            .WithMany(x => x.UnitConversions)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FromUnit)
            .WithMany()
            .HasForeignKey(x => x.FromUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ToUnit)
            .WithMany()
            .HasForeignKey(x => x.ToUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ProductId, x.FromUnitId, x.ToUnitId }).IsUnique();
    }
}
