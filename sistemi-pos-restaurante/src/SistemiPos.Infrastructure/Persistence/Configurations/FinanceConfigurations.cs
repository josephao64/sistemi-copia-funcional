using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Finance;
using SistemiPos.Domain.Purchases;
using SistemiPos.Domain.Suppliers;
using SistemiPos.Domain.Sales;
using SistemiPos.Domain.Customers;
using SistemiPos.Domain.Cash;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class PayableAccountConfiguration : IEntityTypeConfiguration<PayableAccount>
{
    public void Configure(EntityTypeBuilder<PayableAccount> b)
    {
        b.ToTable("PayableAccounts"); b.HasKey(x => x.Id); b.Property(x => x.InvoiceNumber).HasMaxLength(100).IsRequired(); Money(b);
        b.Property(x => x.Version).IsRowVersion();
        b.HasIndex(x => x.PurchaseId).IsUnique(); b.HasIndex(x => new { x.Status, x.DueDateUtc });
        b.HasOne<Purchase>().WithOne().HasForeignKey<PayableAccount>(x => x.PurchaseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Payments).WithOne().HasForeignKey(x => x.PayableAccountId).OnDelete(DeleteBehavior.Restrict); b.Navigation(x => x.Payments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
    private static void Money(EntityTypeBuilder<PayableAccount> b) { b.Property(x => x.Total).HasColumnType("numeric(18,2)"); b.Property(x => x.Paid).HasColumnType("numeric(18,2)"); b.Property(x => x.Balance).HasColumnType("numeric(18,2)"); }
}
internal sealed class PayablePaymentConfiguration : IEntityTypeConfiguration<PayablePayment>
{
    public void Configure(EntityTypeBuilder<PayablePayment> b) { b.ToTable("PayablePayments"); b.HasKey(x => x.Id); b.Property(x => x.Amount).HasColumnType("numeric(18,2)"); b.Property(x => x.Reference).HasMaxLength(150); b.Property(x => x.UserName).HasMaxLength(256).IsRequired(); b.Property(x => x.AuthorizedByUserName).HasMaxLength(256); b.HasOne<CashMovement>().WithMany().HasForeignKey(x => x.CashMovementId).OnDelete(DeleteBehavior.Restrict); b.HasOne<PettyCashMovement>().WithMany().HasForeignKey(x => x.PettyCashMovementId).OnDelete(DeleteBehavior.Restrict); }
}
internal sealed class ReceivableAccountConfiguration : IEntityTypeConfiguration<ReceivableAccount>
{
    public void Configure(EntityTypeBuilder<ReceivableAccount> b)
    {
        b.ToTable("ReceivableAccounts"); b.HasKey(x => x.Id); b.Property(x => x.SaleNumber).HasMaxLength(100).IsRequired(); b.Property(x => x.Total).HasColumnType("numeric(18,2)"); b.Property(x => x.Paid).HasColumnType("numeric(18,2)"); b.Property(x => x.Balance).HasColumnType("numeric(18,2)"); b.Property(x => x.CreditDays).HasDefaultValue(0); b.Property(x => x.GraceDays).HasDefaultValue(0);
        b.HasIndex(x => x.SaleId).IsUnique(); b.HasIndex(x => new { x.Status, x.DueDateUtc }); b.HasMany(x => x.Payments).WithOne().HasForeignKey(x => x.ReceivableAccountId).OnDelete(DeleteBehavior.Restrict); b.Navigation(x => x.Payments).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasOne<Sale>().WithOne().HasForeignKey<ReceivableAccount>(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class ReceivablePaymentConfiguration : IEntityTypeConfiguration<ReceivablePayment>
{
    public void Configure(EntityTypeBuilder<ReceivablePayment> b) { b.ToTable("ReceivablePayments"); b.HasKey(x => x.Id); b.Property(x => x.Amount).HasColumnType("numeric(18,2)"); b.Property(x => x.Reference).HasMaxLength(150); b.Property(x => x.UserName).HasMaxLength(256).IsRequired(); b.Property(x => x.AuthorizedByUserName).HasMaxLength(256); b.HasOne<CashMovement>().WithMany().HasForeignKey(x => x.CashMovementId).OnDelete(DeleteBehavior.Restrict); }
}
internal sealed class PettyCashFundConfiguration : IEntityTypeConfiguration<PettyCashFund>
{
    public void Configure(EntityTypeBuilder<PettyCashFund> b) { b.ToTable("PettyCashFunds"); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(100).IsRequired(); b.Property(x => x.ResponsibleUserName).HasMaxLength(256).IsRequired(); b.HasIndex(x => x.ResponsibleUserId); b.Property(x => x.FundAmount).HasColumnType("numeric(18,2)"); b.Property(x => x.Balance).HasColumnType("numeric(18,2)"); b.HasMany(x => x.Movements).WithOne().HasForeignKey(x => x.PettyCashFundId).OnDelete(DeleteBehavior.Restrict); b.Navigation(x => x.Movements).UsePropertyAccessMode(PropertyAccessMode.Field); }
}
internal sealed class PettyCashMovementConfiguration : IEntityTypeConfiguration<PettyCashMovement>
{
    public void Configure(EntityTypeBuilder<PettyCashMovement> b) { b.ToTable("PettyCashMovements"); b.HasKey(x => x.Id); b.Property(x => x.Amount).HasColumnType("numeric(18,2)"); b.Property(x => x.Concept).HasMaxLength(150).IsRequired(); b.Property(x => x.Category).HasMaxLength(100); b.Property(x => x.Document).HasMaxLength(100); b.Property(x => x.Beneficiary).HasMaxLength(200); b.Property(x => x.Observation).HasMaxLength(500); b.Property(x => x.UserName).HasMaxLength(256).IsRequired(); b.Property(x => x.AuthorizedByUserName).HasMaxLength(256); b.Property(x => x.SourceType).HasMaxLength(100); b.Property(x => x.CancelledByUserName).HasMaxLength(256); b.Property(x => x.CancellationAuthorizedByUserName).HasMaxLength(256); b.Property(x => x.CancellationReason).HasMaxLength(500); b.HasIndex(x => new { x.SourceType, x.SourceId }); b.HasOne<PettyCashCycle>().WithMany().HasForeignKey(x => x.CycleId).OnDelete(DeleteBehavior.Restrict); b.HasOne<PettyCashCategory>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict); }
}

internal sealed class PettyCashCycleConfiguration : IEntityTypeConfiguration<PettyCashCycle>
{
    public void Configure(EntityTypeBuilder<PettyCashCycle> b) { b.ToTable("PettyCashCycles"); b.HasKey(x => x.Id); b.Property(x => x.InitialBalance).HasColumnType("numeric(18,2)"); b.Property(x => x.ExpectedBalance).HasColumnType("numeric(18,2)"); b.Property(x => x.CountedBalance).HasColumnType("numeric(18,2)"); b.Property(x => x.Difference).HasColumnType("numeric(18,2)"); b.Property(x => x.OpenedByUserName).HasMaxLength(256).IsRequired(); b.Property(x => x.ClosedByUserName).HasMaxLength(256); b.Property(x => x.Observation).HasMaxLength(500); b.HasOne<PettyCashFund>().WithMany().HasForeignKey(x => x.PettyCashFundId).OnDelete(DeleteBehavior.Restrict); b.HasIndex(x => new { x.PettyCashFundId, x.Status }); }
}

internal sealed class PettyCashCategoryConfiguration : IEntityTypeConfiguration<PettyCashCategory>
{
    public void Configure(EntityTypeBuilder<PettyCashCategory> b) { b.ToTable("PettyCashCategories"); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(100).IsRequired(); b.HasIndex(x => x.Name).IsUnique(); }
}
internal sealed class PettyCashSettlementConfiguration : IEntityTypeConfiguration<PettyCashSettlement>
{
    public void Configure(EntityTypeBuilder<PettyCashSettlement> b) { b.ToTable("PettyCashSettlements"); b.HasKey(x => x.Id); b.Property(x => x.ExpectedBalance).HasColumnType("numeric(18,2)"); b.Property(x => x.CountedCash).HasColumnType("numeric(18,2)"); b.Property(x => x.Difference).HasColumnType("numeric(18,2)"); b.Property(x => x.UserName).HasMaxLength(256).IsRequired(); b.Property(x => x.Observation).HasMaxLength(500); b.HasOne<PettyCashFund>().WithMany().HasForeignKey(x => x.PettyCashFundId).OnDelete(DeleteBehavior.Restrict); }
}
