using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Sales;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> builder)
    {
        builder.ToTable("SaleItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RecipeJson).HasColumnType("jsonb");
        builder.Property(x => x.ModifiersJson).HasColumnType("jsonb");
        builder.HasOne<SistemiPos.Domain.Orders.RestaurantOrderLine>().WithMany()
            .HasForeignKey(x => x.OrderLineId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.ProductName).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Quantity).HasColumnType("numeric(18,2)");
        builder.Property(x => x.UnitPrice).HasColumnType("numeric(18,6)");
        builder.Property(x => x.GrossAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.CostAtSale).HasColumnType("numeric(22,6)");
        builder.Property(x => x.ConsumedUnitCostAtSale).HasColumnType("numeric(22,6)");
        builder.Property(x => x.RecipeConsumptionJson).HasColumnType("jsonb");
        builder.HasOne<SistemiPos.Domain.Recipes.RecipeVersion>().WithMany()
            .HasForeignKey(x => x.RecipeVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.DiscountPercent).HasColumnType("numeric(18,2)");
        builder.Property(x => x.DiscountAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.TaxableBaseAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.TaxPercent).HasColumnType("numeric(18,2)");
        builder.Property(x => x.TaxAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.Subtotal).HasColumnType("numeric(18,2)");
    }
}
