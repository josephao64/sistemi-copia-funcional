using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Inventory;

public static class InventoryLotAllocation
{
    public static IReadOnlyList<(InventoryExpiryLot Lot, decimal Quantity)> Plan(
        IEnumerable<InventoryExpiryLot> lots, decimal quantity, bool managesExpiry, DateOnly today,
        Guid? selectedLotId = null, bool allowExpired = false, IReadOnlyDictionary<Guid, decimal>? reserved = null)
    {
        if (quantity <= 0) throw new DomainRuleException("La cantidad debe ser mayor a cero.");
        decimal Free(InventoryExpiryLot lot) => Math.Max(0, lot.AvailableQuantity - (reserved?.GetValueOrDefault(lot.Id) ?? 0));
        var eligible = lots.Where(x => Free(x) > 0 && x.ReceivedAtUtc.HasValue
            && (!selectedLotId.HasValue || x.Id == selectedLotId)
            && (!managesExpiry || (x.ExpiryDate.HasValue && (allowExpired || x.ExpiryDate >= today))));
        var ordered = managesExpiry
            ? eligible.OrderBy(x => x.ExpiryDate).ThenBy(x => x.ReceivedAtUtc).ThenBy(x => x.Id)
            : eligible.OrderBy(x => x.ReceivedAtUtc).ThenBy(x => x.Id);
        var available = ordered.ToList();
        if (available.Sum(Free) < quantity)
            throw new DomainRuleException("No hay saldo suficiente en lotes disponibles. Revisa los vencidos y los saldos iniciales pendientes.");
        var result = new List<(InventoryExpiryLot, decimal)>();
        foreach (var lot in available)
        {
            var used = Math.Min(quantity, Free(lot));
            result.Add((lot, used));
            quantity -= used;
            if (quantity == 0) break;
        }
        return result;
    }
}
