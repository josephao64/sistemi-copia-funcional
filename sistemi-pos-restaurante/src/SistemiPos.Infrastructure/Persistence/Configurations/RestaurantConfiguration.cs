using Microsoft.EntityFrameworkCore;
using SistemiPos.Domain.Restaurant;
using SistemiPos.Domain.Organizations;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Catalog;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal static class RestaurantConfiguration
{
    internal static void Configure(ModelBuilder model)
    {
        model.HasSequence<long>("RestaurantOrderNumberSequence");
        var setup = model.Entity<RestaurantSetup>(); setup.ToTable("RestaurantSetups"); setup.HasKey(x => x.Id);
        setup.HasIndex(x => x.BranchId).IsUnique(); setup.Property(x => x.Version).IsRowVersion();
        setup.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        var order = model.Entity<RestaurantOrder>(); order.ToTable("RestaurantOrders"); order.HasKey(x => x.Id);
        order.HasIndex(x => x.Number).IsUnique(); order.HasIndex(x => new { x.BranchId, x.Status });
        order.HasIndex(x => new { x.BranchId, x.ChannelId, x.ExternalReference }).IsUnique().HasFilter("\"ExternalReference\" IS NOT NULL");
        order.Property(x => x.Number).HasMaxLength(30); order.Property(x => x.ExternalReference).HasMaxLength(120);
        order.Property(x => x.Version).IsRowVersion(); order.Property(x => x.DeliveryFee).HasPrecision(18, 2); order.Ignore(x => x.Total);
        order.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        order.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        order.HasOne<TableTurn>().WithMany().HasForeignKey(x => x.TableTurnId).OnDelete(DeleteBehavior.Restrict);
        order.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        order.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        order.HasMany(x => x.Events).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        order.Navigation(x => x.Events).UsePropertyAccessMode(PropertyAccessMode.Field);
        var line = model.Entity<RestaurantOrderLine>(); line.ToTable("RestaurantOrderLines", t => t.HasCheckConstraint("CK_RestaurantLine_Quantity", "\"Quantity\" > 0 AND \"CancelledQuantity\" >= 0 AND \"CancelledQuantity\" <= \"Quantity\" AND \"WasteQuantity\" >= 0 AND \"WasteQuantity\" <= \"CancelledQuantity\""));
        line.HasKey(x => x.Id); line.Ignore(x => x.ActiveQuantity); line.Ignore(x => x.ActiveTotal);
        line.Property(x => x.Quantity).HasPrecision(18, 6); line.Property(x => x.CancelledQuantity).HasPrecision(18, 6); line.Property(x => x.WasteQuantity).HasPrecision(18, 6);
        line.Property(x => x.PreparedQuantity).HasPrecision(18, 6);
        line.Property(x => x.ReturnedQuantity).HasPrecision(18, 6);
        line.HasOne<RestaurantOrderLine>().WithMany().HasForeignKey(x => x.ParentLineId).OnDelete(DeleteBehavior.Restrict);
        line.ToTable("RestaurantOrderLines", t => t.HasCheckConstraint("CK_RestaurantLine_Preparation", "\"PreparedQuantity\" >= 0 AND \"PreparedQuantity\" <= \"Quantity\" AND \"ReturnedQuantity\" >= 0 AND \"ReturnedQuantity\" + \"WasteQuantity\" <= \"PreparedQuantity\" AND \"ReturnedQuantity\" + \"WasteQuantity\" <= \"CancelledQuantity\""));
        line.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        line.HasIndex(x => new { x.StationId, x.Status });
        var ev = model.Entity<RestaurantEvent>(); ev.ToTable("RestaurantEvents"); ev.HasKey(x => x.Id); ev.HasIndex(x => new { x.OrderId, x.Revision }).IsUnique();
        var hold = model.Entity<OrderReservation>(); hold.ToTable("OrderReservations", t => t.HasCheckConstraint("CK_OrderReservation_Quantity", "\"Quantity\" >= 0")); hold.HasKey(x => x.Id);
        hold.Property(x => x.Quantity).HasPrecision(18, 6); hold.Property(x => x.UnitCost).HasPrecision(18, 6);
        hold.Property(x => x.RestoredQuantity).HasPrecision(18, 6);
        hold.ToTable("OrderReservations", t => t.HasCheckConstraint("CK_OrderReservation_Restored", "\"RestoredQuantity\" >= 0 AND \"RestoredQuantity\" <= \"Quantity\""));
        hold.HasIndex(x => new { x.ProductId, x.WarehouseId, x.Status }); hold.HasIndex(x => new { x.LotId, x.Status });
        hold.HasOne<RestaurantOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        hold.HasOne<RestaurantOrderLine>().WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Restrict);
        hold.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        hold.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        hold.HasOne<InventoryExpiryLot>().WithMany().HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Restrict);
        hold.HasOne<InventoryMovement>().WithMany().HasForeignKey(x => x.MovementId).OnDelete(DeleteBehavior.Restrict);
        var turn = model.Entity<TableTurn>(); turn.ToTable("RestaurantTableTurns"); turn.HasKey(x => x.Id); turn.Property(x => x.Version).IsRowVersion();
        turn.HasIndex(x => new { x.BranchId, x.TableId }).IsUnique().HasFilter("\"ClosedAtUtc\" IS NULL");
        turn.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        var command = model.Entity<RestaurantCommand>(); command.ToTable("RestaurantCommands"); command.HasKey(x => x.Id); command.HasIndex(x => x.OperationId).IsUnique();
        command.Property(x => x.Fingerprint).HasMaxLength(64); command.Property(x => x.UserName).HasMaxLength(256);
        var waste = model.Entity<OrderWaste>(); waste.ToTable("OrderWaste"); waste.HasKey(x => x.Id);
        waste.Property(x => x.Quantity).HasPrecision(18, 6); waste.Property(x => x.Cost).HasPrecision(18, 6);
        waste.HasOne<RestaurantOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        waste.HasOne<RestaurantOrderLine>().WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<InventoryStock>().Property(x => x.ReservedQuantity).HasPrecision(18, 6).HasDefaultValue(0m);
        model.Entity<InventoryStock>().ToTable("InventoryStocks", t => t.HasCheckConstraint("CK_InventoryStock_Reservations", "\"ReservedQuantity\" >= 0 AND \"CurrentStock\" >= \"ReservedQuantity\""));
        model.Entity<InventoryMovement>().HasOne<InventoryMovement>().WithMany().HasForeignKey(x => x.OriginalMovementId).OnDelete(DeleteBehavior.Restrict);
    }
}
