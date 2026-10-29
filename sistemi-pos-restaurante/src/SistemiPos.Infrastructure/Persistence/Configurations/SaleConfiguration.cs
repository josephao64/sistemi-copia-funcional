using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Sales;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales");
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.Payment);
        builder.Property(x => x.Version).IsRowVersion();
        builder.Property(x => x.RequestFingerprint).HasMaxLength(64);
        builder.Property(x => x.TerminalCode).HasMaxLength(50);
        builder.HasOne<SistemiPos.Domain.Orders.RestaurantOrder>().WithMany()
            .HasForeignKey(x => x.RestaurantOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SistemiPos.Domain.Orders.PosTerminal>().WithMany()
            .HasForeignKey(x => x.TerminalId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.Number).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ClientOperationId).HasMaxLength(80);
        builder.Property(x => x.CustomerNitSnapshot).HasMaxLength(50);
        builder.Property(x => x.CustomerNameSnapshot).HasMaxLength(200);
        builder.Property(x => x.CustomerAddressSnapshot).HasMaxLength(500);
        builder.Property(x => x.CustomerPhoneSnapshot).HasMaxLength(50);
        builder.Property(x => x.CustomerVisitOriginSnapshot).HasMaxLength(150);
        builder.Property(x => x.CashierUserName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Subtotal).HasColumnType("numeric(18,2)");
        builder.Property(x => x.DiscountTotal).HasColumnType("numeric(18,2)");
        builder.Property(x => x.TaxableBaseTotal).HasColumnType("numeric(18,2)");
        builder.Property(x => x.TaxTotal).HasColumnType("numeric(18,2)");
        builder.Property(x => x.Total).HasColumnType("numeric(18,2)");
        builder.Property(x => x.DiscountAppliedByUserName).HasMaxLength(256);
        builder.Property(x => x.DiscountReason).HasMaxLength(500);
        builder.Property(x => x.DiscountAuthorizedByUserName).HasMaxLength(256);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10).IsRequired();
        builder.Property(x => x.CurrencyName).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CurrencySymbol).HasMaxLength(10).IsRequired();
        builder.Property(x => x.DecimalSeparator).HasMaxLength(5).IsRequired();
        builder.Property(x => x.ThousandsSeparator).HasMaxLength(5).IsRequired();
        builder.Property(x => x.CurrencySymbolPosition).HasMaxLength(10).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);

        builder.HasIndex(x => x.Number).IsUnique();
        builder.HasIndex(x => x.ClientOperationId)
            .IsUnique()
            .HasFilter("\"ClientOperationId\" IS NOT NULL");
        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SistemiPos.Domain.Cash.CashSession>()
            .WithMany()
            .HasForeignKey(x => x.CashSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.WarehouseId);

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Payments)
            .WithOne()
            .HasForeignKey(x => x.SaleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Payments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
