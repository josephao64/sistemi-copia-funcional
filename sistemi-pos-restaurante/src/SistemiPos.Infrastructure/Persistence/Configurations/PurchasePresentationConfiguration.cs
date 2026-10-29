using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Purchases;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class PurchasePresentationConfiguration : IEntityTypeConfiguration<PurchasePresentation>
{
    public void Configure(EntityTypeBuilder<PurchasePresentation> builder)
    {
        builder.ToTable("PurchasePresentations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);
        builder.HasIndex(x => x.Name).IsUnique();
        var createdAt = new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.Zero);
        builder.HasData(
            new { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), Name = "Unidad", Units = 1, IsActive = true, CreatedAtUtc = createdAt },
            new { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Name = "Docena", Units = 12, IsActive = true, CreatedAtUtc = createdAt },
            new { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), Name = "Caja 24", Units = 24, IsActive = true, CreatedAtUtc = createdAt },
            new { Id = Guid.Parse("10000000-0000-0000-0000-000000000004"), Name = "Caja 48", Units = 48, IsActive = true, CreatedAtUtc = createdAt });
    }
}
