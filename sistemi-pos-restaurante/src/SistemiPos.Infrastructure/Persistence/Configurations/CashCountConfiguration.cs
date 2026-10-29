using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Configuration;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class CashCountConfiguration : IEntityTypeConfiguration<CashCount>
{
    public void Configure(EntityTypeBuilder<CashCount> b)
    {
        b.ToTable("CashCounts"); b.HasKey(x => x.Id);
        b.Property(x => x.CountedByUserName).HasMaxLength(256).IsRequired();
        b.Property(x => x.ExpectedCash).HasColumnType("numeric(18,2)"); b.Property(x => x.CountedCash).HasColumnType("numeric(18,2)"); b.Property(x => x.Difference).HasColumnType("numeric(18,2)");
        b.HasIndex(x => new { x.CashSessionId, x.Sequence }).IsUnique();
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.CashCountId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
internal sealed class CashCountLineConfiguration : IEntityTypeConfiguration<CashCountLine>
{
    public void Configure(EntityTypeBuilder<CashCountLine> b)
    {
        b.ToTable("CashCountLines"); b.HasKey(x => x.Id); b.Property(x => x.DenominationValue).HasColumnType("numeric(18,2)"); b.Property(x => x.Subtotal).HasColumnType("numeric(18,2)"); b.HasOne<CashDenomination>().WithMany().HasForeignKey(x => x.DenominationId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class CashPosClosingConfiguration : IEntityTypeConfiguration<CashPosClosing>
{
    public void Configure(EntityTypeBuilder<CashPosClosing> b)
    {
        b.ToTable("CashPosClosings"); b.HasKey(x => x.Id); b.Property(x => x.PosDeviceName).HasMaxLength(100).IsRequired();
        b.Property(x => x.SystemTotal).HasColumnType("numeric(18,2)"); b.Property(x => x.PhysicalTotal).HasColumnType("numeric(18,2)"); b.Property(x => x.Difference).HasColumnType("numeric(18,2)");
        b.Property(x => x.BatchReference).HasMaxLength(100); b.Property(x => x.ReceiptPath).HasMaxLength(500); b.HasIndex(x => new { x.CashSessionId, x.PosDeviceId }).IsUnique();
        b.HasOne<PosDevice>().WithMany().HasForeignKey(x => x.PosDeviceId).OnDelete(DeleteBehavior.Restrict);
    }
}
