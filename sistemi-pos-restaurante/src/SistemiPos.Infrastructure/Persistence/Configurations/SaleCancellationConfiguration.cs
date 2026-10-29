using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Sales;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class SaleCancellationConfiguration : IEntityTypeConfiguration<SaleCancellation>
{
    public void Configure(EntityTypeBuilder<SaleCancellation> builder)
    {
        builder.ToTable("SaleCancellations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Number).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.UserName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.AuthorizedByUserName).HasMaxLength(256);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);

        builder.HasIndex(x => x.Number).IsUnique();
        builder.HasIndex(x => x.SaleId).IsUnique();
    }
}
