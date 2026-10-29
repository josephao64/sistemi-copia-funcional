using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Sales;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class FelCertificationConfiguration : IEntityTypeConfiguration<FelCertification>
{
    public void Configure(EntityTypeBuilder<FelCertification> builder)
    {
        builder.ToTable("FelCertifications");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.SaleId).IsUnique();
        builder.HasOne<Sale>().WithOne().HasForeignKey<FelCertification>(x => x.SaleId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.Provider).HasMaxLength(30).IsRequired();
        builder.Property(x => x.AuthorizationNumber).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Series).HasMaxLength(100).IsRequired();
        builder.Property(x => x.DocumentNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CertifierName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CertifierNit).HasMaxLength(50);
        builder.Property(x => x.QrCode).HasMaxLength(2000);
        builder.Property(x => x.AccessNumber).HasMaxLength(100);
        builder.Property(x => x.CertifiedDocumentXml).HasColumnType("text");
        builder.Property(x => x.CertificationResponseJson).HasColumnType("text");
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);
    }
}
