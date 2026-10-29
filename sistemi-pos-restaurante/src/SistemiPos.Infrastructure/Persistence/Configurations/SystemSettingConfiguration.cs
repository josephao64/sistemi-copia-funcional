using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Configuration;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("SystemSettings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BusinessName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.BusinessLegalName).HasMaxLength(200);
        builder.Property(x => x.BusinessNit).HasMaxLength(50);
        builder.Property(x => x.BusinessAddress).HasMaxLength(500);
        builder.Property(x => x.BusinessPhone).HasMaxLength(100);
        builder.Property(x => x.BusinessEmail).HasMaxLength(200);
        builder.Property(x => x.BusinessLogoUrl).HasMaxLength(500);
        builder.Property(x => x.EstablishmentName).HasMaxLength(200);
        builder.Property(x => x.EstablishmentCode).HasMaxLength(50);
        builder.Property(x => x.FelProvider).HasMaxLength(30).HasDefaultValue("INFILE").IsRequired();
        builder.Property(x => x.FelEnvironment).HasDefaultValue(FelEnvironmentType.Testing);
        builder.Property(x => x.DefaultFelDocumentType).HasMaxLength(20).HasDefaultValue("FACT").IsRequired();
        builder.Property(x => x.FelTaxPhrases).HasMaxLength(2000);
        builder.Property(x => x.InfileApiUserProtected).HasMaxLength(2000);
        builder.Property(x => x.InfileApiKeyProtected).HasMaxLength(2000);
        builder.Property(x => x.InfileSignatureUserProtected).HasMaxLength(2000);
        builder.Property(x => x.InfileSignatureKeyProtected).HasMaxLength(2000);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10).IsRequired();
        builder.HasOne<CurrencyDefinition>().WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.CurrencyName).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CurrencySymbol).HasMaxLength(10).IsRequired();
        builder.Property(x => x.DecimalSeparator).HasMaxLength(5).IsRequired();
        builder.Property(x => x.ThousandsSeparator).HasMaxLength(5).IsRequired();
        builder.Property(x => x.TaxPercent).HasColumnType("numeric(18,2)");
        builder.Property(x => x.DefaultMaxDiscountPercent).HasColumnType("numeric(18,2)");
        builder.Property(x => x.CreditPaymentsEnabled).HasDefaultValue(true);
        builder.Property(x => x.ProductsWithoutMovementDays).HasDefaultValue(30);
        builder.Property(x => x.ReceiptFooter).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);
    }
}

internal sealed class CurrencyDefinitionConfiguration : IEntityTypeConfiguration<CurrencyDefinition>
{
    public void Configure(EntityTypeBuilder<CurrencyDefinition> builder)
    {
        builder.ToTable("Currencies"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(10).IsRequired(); builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(80).IsRequired(); builder.Property(x => x.Symbol).HasMaxLength(10).IsRequired();
        builder.Property(x => x.DecimalSeparator).HasMaxLength(5).IsRequired(); builder.Property(x => x.ThousandsSeparator).HasMaxLength(5).IsRequired();
    }
}
