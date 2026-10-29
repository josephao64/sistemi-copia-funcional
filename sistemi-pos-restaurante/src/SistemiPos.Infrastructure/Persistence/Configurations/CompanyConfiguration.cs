using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Organizations;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.TradeName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.LegalName).HasMaxLength(128);
        builder.Property(x => x.Nit).HasMaxLength(32);
        builder.Property(x => x.Email).HasMaxLength(128);
        builder.Property(x => x.Phone).HasMaxLength(32);
        builder.Property(x => x.Address).HasMaxLength(256);
        builder.Property(x => x.TaxRegime).HasMaxLength(128);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);

        builder.HasIndex(x => new { x.BusinessGroupId, x.Code }).IsUnique();

        builder.HasOne(x => x.BusinessGroup)
            .WithMany(x => x.Companies)
            .HasForeignKey(x => x.BusinessGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Branches)
            .WithOne(x => x.Company)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
