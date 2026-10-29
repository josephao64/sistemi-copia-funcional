using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Sales;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class SaleAccountPartConfiguration : IEntityTypeConfiguration<SaleAccountPart>
{
    public void Configure(EntityTypeBuilder<SaleAccountPart> b)
    {
        b.ToTable("SaleAccountParts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.HasIndex(x => new { x.SaleId, x.Number }).IsUnique();
        b.HasOne<Sale>().WithMany(x => x.AccountParts).HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
    }
}
