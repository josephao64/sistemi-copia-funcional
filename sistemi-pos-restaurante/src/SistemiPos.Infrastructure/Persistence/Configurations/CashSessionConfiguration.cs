using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Cash;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class CashSessionConfiguration : IEntityTypeConfiguration<CashSession>
{
    public void Configure(EntityTypeBuilder<CashSession> builder)
    {
        builder.ToTable("CashSessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Version).IsRowVersion();
        builder.Property(x => x.TimeZoneSnapshot).HasMaxLength(64);
        builder.Property(x => x.ClosedByUserName).HasMaxLength(256);
        builder.HasOne<SistemiPos.Domain.Orders.PosTerminal>().WithMany().HasForeignKey(x => x.TerminalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.BranchId, x.TerminalId }).IsUnique().HasFilter("\"Status\" = 1 AND \"TerminalId\" IS NOT NULL");
        builder.Property(x => x.CurrencyCode).HasMaxLength(10);
        builder.Property(x => x.CurrencySnapshotJson).HasColumnType("jsonb");
        builder.HasIndex(x => new { x.BranchId, x.RegisterCode }).IsUnique().HasFilter("\"Status\" = 1");
        builder.HasIndex(x => x.CashierUserName).IsUnique().HasFilter("\"Status\" = 1");

        builder.Property(x => x.RegisterCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CashierUserName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.OpeningObservation).HasMaxLength(500);
        builder.Property(x => x.ClosingObservation).HasMaxLength(500);
        builder.Property(x => x.DifferenceAuthorizedByUserName).HasMaxLength(256);
        builder.Property(x => x.ReconciledByUserName).HasMaxLength(256);
        builder.Property(x => x.ReconciliationObservation).HasMaxLength(500);
        builder.Property(x => x.OpeningAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.CountedCash).HasColumnType("numeric(18,2)");
        builder.Property(x => x.TotalSold).HasColumnType("numeric(18,2)");
        builder.Property(x => x.TotalOtherSales).HasColumnType("numeric(18,2)");
        builder.Property(x => x.TotalOtherRefunds).HasColumnType("numeric(18,2)");
        builder.Property(x => x.TotalCashSales).HasColumnType("numeric(18,2)");
        builder.Property(x => x.TotalTransferSales).HasColumnType("numeric(18,2)");
        builder.Property(x => x.TotalPosSales).HasColumnType("numeric(18,2)");
        builder.Property(x => x.OtherIncomeTotal).HasColumnType("numeric(18,2)");
        builder.Property(x => x.ExpenseTotal).HasColumnType("numeric(18,2)");
        builder.Property(x => x.TotalCashRefunds).HasColumnType("numeric(18,2)");
        builder.Property(x => x.TotalTransferRefunds).HasColumnType("numeric(18,2)");
        builder.Property(x => x.TotalPosRefunds).HasColumnType("numeric(18,2)");
        builder.Property(x => x.ExpectedCash).HasColumnType("numeric(18,2)");
        builder.Property(x => x.Difference).HasColumnType("numeric(18,2)");
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);

        builder.HasIndex(x => new { x.RegisterCode, x.Status });
        builder.HasIndex(x => x.CashierUserId)
            .IsUnique()
            .HasFilter("\"Status\" = 1 AND \"CashierUserId\" IS NOT NULL");

        builder.HasMany(x => x.Movements)
            .WithOne()
            .HasForeignKey(x => x.CashSessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Movements).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(x => x.Counts)
            .WithOne()
            .HasForeignKey(x => x.CashSessionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Counts).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(x => x.PosClosings)
            .WithOne()
            .HasForeignKey(x => x.CashSessionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.PosClosings).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.AssignedPosDevices).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.BranchId);
    }
}
