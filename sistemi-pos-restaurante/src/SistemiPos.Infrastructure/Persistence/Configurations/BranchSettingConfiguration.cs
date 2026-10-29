using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Organizations;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class BranchSettingConfiguration : IEntityTypeConfiguration<BranchSetting>
{
    public void Configure(EntityTypeBuilder<BranchSetting> builder)
    {
        builder.ToTable("BranchSettings");
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Branch)
            .WithOne(x => x.Setting)
            .HasForeignKey<BranchSetting>(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.BranchId).IsUnique();

        builder.Property(x => x.EstablishmentCode).HasMaxLength(50);
        builder.Property(x => x.EstablishmentName).HasMaxLength(200);
        builder.Property(x => x.ReceiptFooter).HasMaxLength(500);

        builder.Property(x => x.OverridePaymentMethods).HasDefaultValue(false);
        builder.Property(x => x.CashPaymentsEnabled).HasDefaultValue(true);
        builder.Property(x => x.TransferPaymentsEnabled).HasDefaultValue(true);
        builder.Property(x => x.PosPaymentsEnabled).HasDefaultValue(true);
        builder.Property(x => x.CreditPaymentsEnabled).HasDefaultValue(true);

        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);
    }
}
