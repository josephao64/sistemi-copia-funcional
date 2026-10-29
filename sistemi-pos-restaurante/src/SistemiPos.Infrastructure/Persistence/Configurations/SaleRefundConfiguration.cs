using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Sales;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class SaleRefundConfiguration : IEntityTypeConfiguration<SaleRefund>
{
    public void Configure(EntityTypeBuilder<SaleRefund> builder)
    {
        builder.ToTable("SaleRefunds");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OperationId).HasMaxLength(80);
        builder.HasIndex(x => new { x.SaleId, x.OperationId }).IsUnique().HasFilter("\"OperationId\" IS NOT NULL");
        builder.HasOne<SalePayment>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.Amount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ProcessedByUserName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.AuthorizedByUserName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Reference).HasMaxLength(100);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);
    }
}
