using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Configuration;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class RoleDiscountSettingConfiguration : IEntityTypeConfiguration<RoleDiscountSetting>
{
    public void Configure(EntityTypeBuilder<RoleDiscountSetting> builder)
    {
        builder.ToTable("RoleDiscountSettings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MaxDiscountPercent).HasColumnType("numeric(18,2)");
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);

        builder.HasIndex(x => x.RoleId).IsUnique();
    }
}
