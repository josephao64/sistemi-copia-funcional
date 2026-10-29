using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Configuration;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class CashSessionPosDeviceConfiguration : IEntityTypeConfiguration<CashSessionPosDevice>
{
    public void Configure(EntityTypeBuilder<CashSessionPosDevice> builder)
    {
        builder.ToTable("CashSessionPosDevices");
        builder.HasKey(x => new { x.CashSessionId, x.PosDeviceId });
        builder.Property(x => x.PosDeviceName).HasMaxLength(150).IsRequired();
        builder.HasOne<CashSession>().WithMany(x => x.AssignedPosDevices).HasForeignKey(x => x.CashSessionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PosDevice>().WithMany().HasForeignKey(x => x.PosDeviceId).OnDelete(DeleteBehavior.Restrict);
    }
}
