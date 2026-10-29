using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Organizations;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class CompanySettingConfiguration : IEntityTypeConfiguration<CompanySetting>
{
    public void Configure(EntityTypeBuilder<CompanySetting> builder)
    {
        builder.ToTable("CompanySettings");
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Company)
            .WithOne(x => x.Setting)
            .HasForeignKey<CompanySetting>(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CompanyId).IsUnique();

        builder.Property(x => x.BusinessName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.BusinessLegalName).HasMaxLength(200);
        builder.Property(x => x.BusinessNit).HasMaxLength(50);
        builder.Property(x => x.BusinessAddress).HasMaxLength(500);
        builder.Property(x => x.BusinessPhone).HasMaxLength(100);
        builder.Property(x => x.BusinessEmail).HasMaxLength(200);
        builder.Property(x => x.BusinessLogoUrl).HasMaxLength(500);

        builder.Property(x => x.CurrencyCode).HasMaxLength(10).IsRequired();
        builder.HasOne<CurrencyDefinition>().WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.CurrencyName).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CurrencySymbol).HasMaxLength(10).IsRequired();
        builder.Property(x => x.DecimalSeparator).HasMaxLength(5).IsRequired();
        builder.Property(x => x.ThousandsSeparator).HasMaxLength(5).IsRequired();

        builder.Property(x => x.TaxPercent).HasColumnType("numeric(18,2)");
        builder.Property(x => x.DefaultMaxDiscountPercent).HasColumnType("numeric(18,2)");
        builder.Property(x => x.CashPaymentsEnabled).HasDefaultValue(true);
        builder.Property(x => x.TransferPaymentsEnabled).HasDefaultValue(true);
        builder.Property(x => x.PosPaymentsEnabled).HasDefaultValue(true);
        builder.Property(x => x.CreditPaymentsEnabled).HasDefaultValue(true);
        builder.Property(x => x.DiscountsEnabled).HasDefaultValue(false);
        builder.Property(x => x.ReceiptFooter).HasMaxLength(500);

        builder.Property(x => x.FelProvider).HasMaxLength(30).HasDefaultValue("INFILE").IsRequired();
        builder.Property(x => x.FelEnvironment).HasDefaultValue(FelEnvironmentType.Testing);
        builder.Property(x => x.DefaultFelDocumentType).HasMaxLength(20).HasDefaultValue("FACT").IsRequired();
        builder.Property(x => x.FelTaxPhrases).HasMaxLength(2000);
        builder.Property(x => x.InfileApiUserProtected).HasMaxLength(2000);
        builder.Property(x => x.InfileApiKeyProtected).HasMaxLength(2000);
        builder.Property(x => x.InfileSignatureUserProtected).HasMaxLength(2000);
        builder.Property(x => x.InfileSignatureKeyProtected).HasMaxLength(2000);

        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);
    }
}
