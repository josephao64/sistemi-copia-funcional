using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Catalog;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class ProductBranchSettingConfiguration : IEntityTypeConfiguration<ProductBranchSetting>
{
    public void Configure(EntityTypeBuilder<ProductBranchSetting> builder)
    {
        builder.ToTable("ProductBranchSettings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.IsEnabled).HasDefaultValue(true);
        builder.Property(x => x.IsVisible).HasDefaultValue(true);
        builder.Property(x => x.SalePrice).HasColumnType("numeric(18,2)");
        builder.Property(x => x.MinimumStock).HasColumnType("numeric(18,2)");
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);

        builder.HasOne(x => x.Product)
            .WithMany(x => x.BranchSettings)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ProductId, x.BranchId }).IsUnique();
    }
}
