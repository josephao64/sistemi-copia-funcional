using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Catalog;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class ProductPriceRuleConfiguration : IEntityTypeConfiguration<ProductPriceRule>
{
    public void Configure(EntityTypeBuilder<ProductPriceRule> builder)
    {
        builder.ToTable("ProductPriceRules");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity).HasColumnType("numeric(18,2)");
        builder.Property(x => x.Price).HasColumnType("numeric(18,2)");
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasOne(x => x.Product)
            .WithMany(x => x.PriceRules)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ProductId, x.Quantity }).IsUnique();
    }
}
