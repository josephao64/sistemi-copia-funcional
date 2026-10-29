using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Sales;
using SistemiPos.Domain.Configuration;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class SalePaymentConfiguration : IEntityTypeConfiguration<SalePayment>
{
    public void Configure(EntityTypeBuilder<SalePayment> builder)
    {
        builder.ToTable("SalePayments");
        builder.HasKey(x => x.Id);
        builder.HasOne<SistemiPos.Domain.Configuration.PaymentMethodDefinition>().WithMany().HasForeignKey(x => x.DefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.DefinitionName).HasMaxLength(200);
        builder.Property(x => x.OtherReference).HasMaxLength(100);
        builder.HasOne<SaleAccountPart>().WithMany().HasForeignKey(x => x.AccountPartId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.TotalAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.CreatedByUserName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ReceivedAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.ChangeAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.TransferSourceBank).HasMaxLength(150);
        builder.Property(x => x.TransferSenderName).HasMaxLength(200);
        builder.Property(x => x.TransferDestinationBank).HasMaxLength(150);
        builder.Property(x => x.TransferDestinationAccountName).HasMaxLength(150);
        builder.Property(x => x.TransferDestinationAccountNumber).HasMaxLength(100);
        builder.Property(x => x.TransferReference).HasMaxLength(100);
        builder.Property(x => x.TransferAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.TransferReviewedByUserName).HasMaxLength(256);
        builder.Property(x => x.TransferReviewObservation).HasMaxLength(500);
        builder.Property(x => x.CardLast4).HasMaxLength(4);
        builder.Property(x => x.CardAuthorizationNumber).HasMaxLength(100);
        builder.Property(x => x.CardVoucherReference).HasMaxLength(100);
        builder.Property(x => x.CardAmount).HasColumnType("numeric(18,2)");
        builder.HasIndex(x => x.PosDeviceId);
        builder.HasOne<PosDevice>().WithMany().HasForeignKey(x => x.PosDeviceId).OnDelete(DeleteBehavior.Restrict);
    }
}
