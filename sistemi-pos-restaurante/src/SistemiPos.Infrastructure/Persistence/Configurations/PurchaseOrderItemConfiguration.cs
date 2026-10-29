using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Purchases;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseOrderItemConfiguration : IEntityTypeConfiguration<PurchaseOrderItem>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderItem> builder)
    {
        builder.ToTable("PurchaseOrderItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RequestedQuantity).HasColumnType("numeric(18,2)");
        builder.Property(x => x.ReceivedQuantity).HasColumnType("numeric(18,2)");
        builder.Property(x => x.PackageQuantity).HasColumnType("numeric(18,2)");
        builder.Property(x => x.BaseQuantityPerPackage).HasColumnType("numeric(18,6)");
        builder.Property(x => x.BaseUnitCode).HasMaxLength(30);
        builder.Property(x => x.PresentationName).HasMaxLength(100).IsRequired();
        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Presentation)
            .WithMany()
            .HasForeignKey(x => x.PresentationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ProductPresentation)
            .WithMany()
            .HasForeignKey(x => x.ProductPresentationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.PurchaseOrderId, x.ProductId }).IsUnique();
    }
}
