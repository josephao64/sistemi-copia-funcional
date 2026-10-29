using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Organizations;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class BusinessGroupConfiguration : IEntityTypeConfiguration<BusinessGroup>
{
    public void Configure(EntityTypeBuilder<BusinessGroup> builder)
    {
        builder.ToTable("BusinessGroups");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.Property(x => x.LegalName).HasMaxLength(128);
        builder.Property(x => x.TaxNumber).HasMaxLength(32);
        builder.Property(x => x.Description).HasMaxLength(256);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);

        builder.HasIndex(x => x.Code).IsUnique();

        builder.HasMany(x => x.Companies)
            .WithOne(x => x.BusinessGroup)
            .HasForeignKey(x => x.BusinessGroupId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
