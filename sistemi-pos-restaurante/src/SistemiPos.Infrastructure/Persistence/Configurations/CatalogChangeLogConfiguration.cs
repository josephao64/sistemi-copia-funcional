using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Catalog;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class CatalogChangeLogConfiguration : IEntityTypeConfiguration<CatalogChangeLog>
{
    public void Configure(EntityTypeBuilder<CatalogChangeLog> builder)
    {
        builder.ToTable("CatalogChangeLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProductInternalCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Field).HasMaxLength(100).IsRequired();
        builder.Property(x => x.OldValue).HasMaxLength(500);
        builder.Property(x => x.NewValue).HasMaxLength(500);
        builder.Property(x => x.Actor).HasMaxLength(256).IsRequired();
        builder.Property(x => x.TimestampUtc).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500);

        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => x.BranchId);
        builder.HasIndex(x => x.TimestampUtc);
    }
}
