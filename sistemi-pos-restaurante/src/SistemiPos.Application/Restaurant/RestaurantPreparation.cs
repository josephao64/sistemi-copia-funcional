using SistemiPos.Application.Security;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Restaurant;

namespace SistemiPos.Application.Restaurant;

public sealed partial class RestaurantService
{
    private async Task<RestaurantOrderLine> SplitPreparation(RestaurantOrder order, RestaurantOrderLine original,
        decimal quantity, CancellationToken ct)
    {
        if (quantity <= 0 || quantity >= original.ActiveQuantity || original.Status != KitchenLineStatus.Queued)
            throw new DomainRuleException("Indica una cantidad pendiente menor a la línea para dividir la preparación.");
        var products = (await repository.ProductsAsync(ct)).ToDictionary(x => x.Id);
        if (quantity != decimal.Round(quantity, Math.Min(2, products[original.ProductId].BaseUnit!.Precision)))
            throw new DomainRuleException("La cantidad no respeta la unidad del producto.");
        var holds = (await repository.ReservationsAsync(order.WarehouseId, order.Id, ct))
            .Where(x => x.LineId == original.Id && x.Status == ReservationStatus.Active).ToList();
        var fraction = quantity / original.ActiveQuantity;
        var split = new Dictionary<Guid, decimal>();
        foreach (var group in holds.GroupBy(x => x.ProductId))
        {
            var remaining = decimal.Round(group.Sum(x => x.Quantity) * fraction, products[group.Key].BaseUnit!.Precision, MidpointRounding.AwayFromZero);
            if (remaining <= 0 || remaining >= group.Sum(x => x.Quantity))
                throw new DomainRuleException("Esta división no respeta la unidad de un componente. Prepara una cantidad mayor o la línea completa.");
            foreach (var hold in group)
            {
                var allocated = Math.Min(remaining, hold.Quantity); split[hold.Id] = allocated; remaining -= allocated;
            }
        }
        var snapshot = original.Read();
        var activeGross = decimal.Round(snapshot.Gross * original.ActiveQuantity / original.Quantity, 2);
        var activeDiscount = activeGross - original.ActiveTotal;
        var firstGross = decimal.Round(activeGross * fraction, 2);
        var firstDiscount = decimal.Round(activeDiscount * fraction, 2);
        var firstConsumption = snapshot.Consumption.Select(c => c with { Quantity = holds.Where(h => h.ProductId == c.ProductId).Sum(h => split[h.Id]) }).Where(c => c.Quantity > 0).ToList();
        var secondConsumption = snapshot.Consumption.Select(c => c with { Quantity = holds.Where(h => h.ProductId == c.ProductId).Sum(h => h.Quantity - split[h.Id]) }).Where(c => c.Quantity > 0).ToList();
        var children = order.SplitLine(original,
            snapshot with { Quantity = quantity, Gross = firstGross, Discount = firstDiscount, Consumption = firstConsumption },
            snapshot with { Quantity = original.ActiveQuantity - quantity, Gross = activeGross - firstGross, Discount = activeDiscount - firstDiscount, Consumption = secondConsumption }, clock.UtcNow);
        foreach (var hold in holds)
        {
            var first = split[hold.Id]; var second = hold.Quantity - first;
            if (first > 0) repository.Add(new OrderReservation(order.Id, children.Preparing.Id, hold.ProductId, hold.WarehouseId, hold.LotId, first, hold.UnitCost));
            if (second > 0) repository.Add(new OrderReservation(order.Id, children.Pending.Id, hold.ProductId, hold.WarehouseId, hold.LotId, second, hold.UnitCost));
            // Transfer the same reserved stock to the children; no physical or aggregate stock change.
            hold.Release(1);
        }
        return children.Preparing;
    }

    private async Task ReturnIntact(RestaurantOrder order, RestaurantOrderLine line, decimal quantity, string reason, CancellationToken ct)
    {
        await Branch(PermissionCodes.InventoryAdjust, ct);
        var snapshot = line.Read();
        if (!line.StartedAtUtc.HasValue || snapshot.RecipeVersionId.HasValue || snapshot.Consumption.Count != 1
            || snapshot.Consumption[0].ProductId != line.ProductId || snapshot.Consumption[0].Quantity != snapshot.Quantity)
            throw new DomainRuleException("Solo se reintegra un SKU intacto sin preparación ni componentes adicionales. La comida preparada se registra como merma.");
        var product = (await repository.ProductsAsync(ct)).Single(x => x.Id == line.ProductId);
        if (quantity <= 0 || quantity > line.ActiveQuantity || decimal.Round(quantity, Math.Min(2, product.BaseUnit!.Precision)) != quantity)
            throw new DomainRuleException("Revisa la cantidad intacta a reintegrar.");
        var holds = (await repository.ReservationsAsync(order.WarehouseId, order.Id, ct))
            .Where(x => x.LineId == line.Id && x.Status == ReservationStatus.Consumed).ToList();
        if (product.ManagesLots && holds.Select(x => x.LotId).Distinct().Count() > 1)
            throw new DomainRuleException("La línea salió de varios lotes. Registra su cancelación y gestiona la recuperación física por lote mediante el flujo autorizado de inventario.");
        var remaining = quantity; var now = clock.UtcNow; var sequence = 0;
        foreach (var hold in holds)
        {
            var restored = Math.Min(remaining, hold.Quantity - hold.RestoredQuantity); if (restored <= 0) continue;
            if (hold.LotId.HasValue && product.ManagesExpiry)
            {
                var lot = (await repository.LotsAsync(hold.ProductId, order.WarehouseId, ct)).Single(x => x.Id == hold.LotId);
                if (lot.ExpiryDate < DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(-6)).Date))
                    throw new DomainRuleException("Un lote vencido no puede volver a disponibilidad.");
            }
            var stock = await repository.StockAsync(hold.ProductId, hold.WarehouseId, ct) ?? throw new DomainRuleException("Existencia no encontrada.");
            var before = stock.CurrentStock; stock.ReplaceForSystem(before + restored);
            var movement = new InventoryMovement(hold.ProductId, InventoryMovementType.OrderReturn, restored, before, stock.CurrentStock,
                now.AddTicks(sequence++), user.UserName, order.Number, $"Retorno intacto de línea {line.Id}: {reason}", warehouseId: hold.WarehouseId, unitCost: hold.UnitCost);
            movement.SetOrderReturnOrigin(hold.MovementId!.Value); movement.SetLotDetails(null, selectedLotId: hold.LotId);
            hold.Restore(restored); repository.Add(movement); remaining -= restored;
            if (remaining == 0) break;
        }
        if (remaining != 0) throw new DomainRuleException("No existe consumo suficiente para esta devolución.");
    }
}
