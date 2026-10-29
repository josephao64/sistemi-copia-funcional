using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Orders;
using SistemiPos.Domain.Organizations;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class PosTerminalConfiguration : IEntityTypeConfiguration<PosTerminal>
{
    public void Configure(EntityTypeBuilder<PosTerminal> b)
    {
        b.ToTable("PosTerminals");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.RegisterCode).HasMaxLength(50).IsRequired();
        b.HasIndex(x => new { x.BranchId, x.Code }).IsUnique();
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RestaurantOrderConfiguration : IEntityTypeConfiguration<RestaurantOrder>
{
    public void Configure(EntityTypeBuilder<RestaurantOrder> b)
    {
        b.ToTable("PosOrders");
        b.HasKey(x => x.Id);
        b.Property(x => x.Version).IsRowVersion();
        b.Property(x => x.CurrencyCode).HasMaxLength(10);
        b.HasIndex(x => new { x.BranchId, x.CreatedAtUtc });
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PosTerminal>().WithMany().HasForeignKey(x => x.TerminalId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CashSession>().WithMany().HasForeignKey(x => x.CashSessionId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Events).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Events).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class RestaurantOrderLineConfiguration : IEntityTypeConfiguration<RestaurantOrderLine>
{
    public void Configure(EntityTypeBuilder<RestaurantOrderLine> b)
    {
        b.ToTable("PosOrderLines", t => t.HasCheckConstraint("CK_OrderLine_Quantities",
            "\"Quantity\" > 0 AND \"PreparingQuantity\" >= 0 AND \"PreparedQuantity\" >= 0 AND \"DispatchedQuantity\" >= 0 AND \"CancelledQuantity\" >= 0 AND \"StartedQuantity\" >= 0 AND \"CancelledAfterPreparationQuantity\" >= 0 AND \"Quantity\" >= \"StartedQuantity\" + \"CancelledQuantity\" - \"CancelledAfterPreparationQuantity\" AND \"SoldQuantity\" >= 0 AND \"SoldQuantity\" <= \"Quantity\" - \"CancelledQuantity\" AND \"AllocatedGrossAmount\" >= 0 AND \"AllocatedGrossAmount\" <= \"GrossAmount\""));
        b.HasKey(x => x.Id);
        b.Property(x => x.ProductName).HasMaxLength(512).IsRequired();
        foreach (var name in new[] { "Quantity", "PreparingQuantity", "PreparedQuantity", "DispatchedQuantity", "CancelledQuantity", "StartedQuantity", "CancelledAfterPreparationQuantity" })
            b.Property<decimal>(name).HasPrecision(18, 2);
        b.Property(x => x.UnitPrice).HasPrecision(18, 6);
        b.Property(x => x.GrossAmount).HasPrecision(18, 2);
        b.Property(x => x.SoldQuantity).HasPrecision(18, 2);
        b.Property(x => x.AllocatedGrossAmount).HasPrecision(18, 2);
        b.Property(x => x.CapturedConsumptionCost).HasPrecision(22, 6);
        b.Ignore(x => x.SellableQuantity);
        b.Property(x => x.TaxPercent).HasPrecision(18, 2);
        b.Property(x => x.RecipeJson).HasColumnType("jsonb");
        b.Property(x => x.ModifiersJson).HasColumnType("jsonb");
        b.Property(x => x.RevisionOperationId).HasMaxLength(80);
        b.HasIndex(x => new { x.OrderId, x.RevisionOperationId }).IsUnique().HasFilter("\"RevisionOperationId\" IS NOT NULL");
        b.HasOne<RestaurantOrderLine>().WithMany().HasForeignKey(x => x.ReplacesLineId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(x => x.RequiresPreparation);
        b.Ignore(x => x.PendingQuantity);
        b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RestaurantOrderEventConfiguration : IEntityTypeConfiguration<RestaurantOrderEvent>
{
    public void Configure(EntityTypeBuilder<RestaurantOrderEvent> b)
    {
        b.ToTable("RestaurantOrderEvents");
        b.HasKey(x => x.Id);
        b.Property(x => x.OperationId).HasMaxLength(80).IsRequired();
        b.Property(x => x.UserName).HasMaxLength(256).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.ExtraPackagingJson).HasColumnType("jsonb");
        b.Property(x => x.Quantity).HasPrecision(18, 2);
        b.HasIndex(x => new { x.OrderId, x.OperationId }).IsUnique();
        b.HasOne<RestaurantOrderLine>().WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Restrict);
    }
}
