using Microsoft.EntityFrameworkCore;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Inventory;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Inventory;

internal static class InventoryLotLedger
{
    public static async Task PrepareAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var movements = db.ChangeTracker.Entries<InventoryMovement>()
            .Where(x => x.State == EntityState.Added)
            .Select(x => x.Entity)
            .OrderBy(x => x.OccurredAtUtc)
            .ThenBy(x => x.Id)
            .ToList();
        foreach (var movement in movements)
        {
            if (db.InventoryMovementLots.Local.Any(x => x.MovementId == movement.Id)) continue;
            var product = db.Products.Local.FirstOrDefault(x => x.Id == movement.ProductId)
                ?? await db.Products.SingleAsync(x => x.Id == movement.ProductId, cancellationToken);
            if (!movement.WarehouseId.HasValue)
            {
                var changedStocks = db.ChangeTracker.Entries<InventoryStock>()
                    .Where(x => x.Entity.ProductId == movement.ProductId && x.State is EntityState.Added or EntityState.Modified)
                    .Select(x => x.Entity).ToList();
                if (changedStocks.Count != 1)
                    throw new DomainRuleException("El movimiento debe identificar una única bodega de inventario.");
                if (changedStocks[0].WarehouseId is Guid warehouseId) movement.AssignWarehouse(warehouseId);
            }
            if (!product.ManagesLots)
            {
                if (movement.SelectedLotId.HasValue || product.ManagesExpiry)
                    throw new DomainRuleException("El producto no tiene habilitado el control por lote.");
                continue;
            }
            var persisted = await db.InventoryExpiryLots.Where(x => x.ProductId == movement.ProductId
                && x.WarehouseId == movement.WarehouseId).ToListAsync(cancellationToken);
            var lots = persisted.Concat(db.InventoryExpiryLots.Local.Where(x => x.ProductId == movement.ProductId
                && x.WarehouseId == movement.WarehouseId)).DistinctBy(x => x.Id).ToList();
            var missing = movement.PreviousStock - lots.Sum(x => x.AvailableQuantity);
            if (missing > 0)
            {
                if (product.ManagesExpiry)
                    throw new DomainRuleException($"{product.DisplayName}: registra los lotes de saldo inicial con su vencimiento antes de operar.");
                var opening = new InventoryExpiryLot(product.Id, null, missing, "SALDO INICIAL",
                    DateTimeOffset.UtcNow, movement.WarehouseId, isOpeningBalance: true);
                db.InventoryExpiryLots.Add(opening);
                lots.Add(opening);
            }
            else if (missing < 0)
                throw new DomainRuleException($"El saldo de lotes de {product.DisplayName} no coincide con la existencia. Requiere conciliación.");

            if (movement.Type == InventoryMovementType.PurchaseReversal)
            {
                await ReversePurchaseAsync(db, movement, cancellationToken);
            }
            else if (movement.Type is InventoryMovementType.ReturnEntry or InventoryMovementType.CancellationReversal or InventoryMovementType.OrderReturn)
            {
                await RestoreAsync(db, movement, cancellationToken);
            }
            else if (movement.NewStock > movement.PreviousStock)
            {
                if (product.ManagesExpiry && !movement.EntryExpiryDate.HasValue)
                    throw new DomainRuleException($"Indica el vencimiento del lote de {product.DisplayName}.");
                var lot = new InventoryExpiryLot(product.Id,
                    product.ManagesExpiry ? movement.EntryExpiryDate : null, movement.Quantity,
                    movement.SourceDocument, movement.OccurredAtUtc, movement.WarehouseId,
                    lotNumber: movement.EntryLotNumber,
                    originLotId: movement.SelectedLotId,
                    unitCost: movement.UnitCost);
                db.InventoryExpiryLots.Add(lot);
                db.InventoryMovementLots.Add(new InventoryMovementLot(movement, lot, movement.Quantity));
            }
            else
            {
                var holds = await db.OrderReservations.Where(x => x.ProductId == movement.ProductId && x.WarehouseId == movement.WarehouseId
                    && x.Status == SistemiPos.Domain.Restaurant.ReservationStatus.Active).ToListAsync(cancellationToken);
                var reserved = holds.Concat(db.OrderReservations.Local.Where(x => x.ProductId == movement.ProductId && x.WarehouseId == movement.WarehouseId))
                    .DistinctBy(x => x.Id).Where(x => x.Status == SistemiPos.Domain.Restaurant.ReservationStatus.Active && x.LotId.HasValue)
                    .GroupBy(x => x.LotId!.Value).ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity));
                var today = DateOnly.FromDateTime(movement.OccurredAtUtc.ToOffset(TimeSpan.FromHours(-6)).Date);
                var allocations = InventoryLotAllocation.Plan(lots, movement.Quantity, product.ManagesExpiry,
                    today, movement.SelectedLotId,
                    allowExpired: movement.Type == InventoryMovementType.NegativeAdjustment && movement.SelectedLotId.HasValue, reserved: reserved);
                foreach (var allocation in allocations)
                {
                    allocation.Lot.Consume(allocation.Quantity);
                    db.InventoryMovementLots.Add(new InventoryMovementLot(movement, allocation.Lot, -allocation.Quantity));
                }
            }
        }
        if (movements.Count > 0)
        {
            var productIds = movements.Select(x => x.ProductId).Distinct().ToList();
            var reservations = await db.OrderReservations.Where(x => productIds.Contains(x.ProductId)
                && x.Status == SistemiPos.Domain.Restaurant.ReservationStatus.Active).ToListAsync(cancellationToken);
            var reservedByLot = reservations.Concat(db.OrderReservations.Local.Where(x => productIds.Contains(x.ProductId)))
                .DistinctBy(x => x.Id).Where(x => x.Status == SistemiPos.Domain.Restaurant.ReservationStatus.Active && x.LotId.HasValue)
                .GroupBy(x => x.LotId!.Value).ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity));
            foreach (var lot in db.InventoryExpiryLots.Local)
                if (lot.AvailableQuantity < reservedByLot.GetValueOrDefault(lot.Id))
                    throw new DomainRuleException("El movimiento afecta un lote reservado para un pedido. Libera o reasigna el pedido antes de operar.");
        }
    }

    private static async Task ReversePurchaseAsync(AppDbContext db, InventoryMovement movement, CancellationToken cancellationToken)
    {
        var original = await db.InventoryMovementLots.Include(x => x.Lot).Include(x => x.Movement)
            .Where(x => x.Movement.PurchaseId == movement.PurchaseId && x.Movement.ProductId == movement.ProductId
                && x.Movement.WarehouseId == movement.WarehouseId && x.Movement.Type == InventoryMovementType.PurchaseEntry)
            .ToListAsync(cancellationToken);
        var ids = original.Select(x => x.Id).ToList();
        var reversed = await db.InventoryMovementLots.Where(x => x.ReversesAllocationId.HasValue
            && ids.Contains(x.ReversesAllocationId.Value)).Select(x => x.ReversesAllocationId!.Value).ToListAsync(cancellationToken);
        reversed.AddRange(db.InventoryMovementLots.Local.Where(x => x.ReversesAllocationId.HasValue)
            .Select(x => x.ReversesAllocationId!.Value));
        foreach (var allocation in PurchaseLotReversal.Plan(movement, original, reversed))
        {
            allocation.Lot.Consume(allocation.Quantity);
            db.InventoryMovementLots.Add(new InventoryMovementLot(movement, allocation.Lot, -allocation.Quantity, allocation.Id));
        }
    }

    private static async Task RestoreAsync(AppDbContext db, InventoryMovement movement, CancellationToken cancellationToken)
    {
        var source = movement.OrderReturnId.HasValue ? $"PED-{movement.OrderReturnId.Value:N}" : movement.SourceDocument;
        var sourceType = movement.OrderReturnId.HasValue ? InventoryMovementType.PosOrderConsumption : InventoryMovementType.SaleExit;
        var original = await db.InventoryMovementLots.Include(x => x.Lot).Include(x => x.Movement)
            .Where(x => x.Movement.ProductId == movement.ProductId
                && x.Movement.WarehouseId == movement.WarehouseId
                && x.Movement.SourceDocument == source
                && x.Movement.Type == (movement.Type == InventoryMovementType.OrderReturn ? InventoryMovementType.OrderConsumption : sourceType)
                && (movement.Type != InventoryMovementType.OrderReturn || x.MovementId == movement.OriginalMovementId) && x.Quantity < 0)
            .OrderBy(x => x.Lot.ExpiryDate).ThenBy(x => x.Lot.ReceivedAtUtc).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        original = original.Concat(db.InventoryMovementLots.Local.Where(x => x.Movement.ProductId == movement.ProductId
            && x.Movement.WarehouseId == movement.WarehouseId && x.Movement.SourceDocument == source
            && x.Movement.Type == (movement.Type == InventoryMovementType.OrderReturn ? InventoryMovementType.OrderConsumption : sourceType)
                && (movement.Type != InventoryMovementType.OrderReturn || x.MovementId == movement.OriginalMovementId) && x.Quantity < 0)).DistinctBy(x => x.Id).ToList();
        var ids = original.Select(x => x.Id).ToList();
        var restored = await db.InventoryMovementLots.Where(x => x.ReversesAllocationId.HasValue
            && ids.Contains(x.ReversesAllocationId.Value)).ToListAsync(cancellationToken);
        restored = restored.Concat(db.InventoryMovementLots.Local.Where(x => x.ReversesAllocationId.HasValue
            && ids.Contains(x.ReversesAllocationId.Value))).DistinctBy(x => x.Id).ToList();
        var available = original.Select(x => new
        {
            Allocation = x,
            Quantity = -x.Quantity - restored.Where(y => y.ReversesAllocationId == x.Id).Sum(y => y.Quantity)
        }).Where(x => x.Quantity > 0 && (!movement.SelectedLotId.HasValue || x.Allocation.LotId == movement.SelectedLotId)).ToList();
        if (available.Sum(x => x.Quantity) < movement.Quantity)
            throw new DomainRuleException("No se puede devolver el inventario: faltan lotes originales disponibles de la venta. Requiere conciliación.");
        if (movement.Type == InventoryMovementType.ReturnEntry && !movement.SelectedLotId.HasValue
            && available.Select(x => x.Allocation.LotId).Distinct().Count() > 1
            && movement.Quantity < available.Sum(x => x.Quantity))
            throw new DomainRuleException("Selecciona el lote original para esta devolución parcial.");
        var remaining = movement.Quantity;
        foreach (var item in available)
        {
            var quantity = Math.Min(remaining, item.Quantity);
            item.Allocation.Lot.Restore(quantity);
            db.InventoryMovementLots.Add(new InventoryMovementLot(movement, item.Allocation.Lot, quantity, item.Allocation.Id));
            remaining -= quantity;
            if (remaining == 0) break;
        }
    }
}
