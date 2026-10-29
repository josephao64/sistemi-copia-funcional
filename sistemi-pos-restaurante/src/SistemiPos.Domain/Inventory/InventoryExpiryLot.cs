using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Inventory;

public sealed class InventoryExpiryLot : Entity
{
    public Guid ProductId { get; private set; }
    public Product Product { get; set; } = null!;
    public DateOnly? ExpiryDate { get; private set; }
    public decimal AvailableQuantity { get; private set; }
    public string? SourceDocument { get; private set; }

    public string LotNumber { get; private set; } = string.Empty;
    public DateTimeOffset? ReceivedAtUtc { get; private set; }
    public decimal? ReceivedQuantity { get; private set; }
    public bool IsOpeningBalance { get; private set; }
    public Guid? WarehouseId { get; private set; }
    public Guid? OriginLotId { get; private set; }
    public decimal? UnitCost { get; private set; }
    public uint Version { get; private set; }

    private InventoryExpiryLot() { }

    public InventoryExpiryLot(Guid productId, DateOnly? expiryDate, decimal quantity, string? sourceDocument,
        DateTimeOffset? receivedAtUtc = null, Guid? warehouseId = null, bool isOpeningBalance = false, string? lotNumber = null,
        Guid? originLotId = null, decimal? unitCost = null)
    {
        if (productId == Guid.Empty || quantity <= 0) throw new DomainRuleException("El lote de caducidad requiere producto y cantidad válida.");
        if (quantity != decimal.Round(quantity, 6) || lotNumber?.Trim().Length > 100)
            throw new DomainRuleException("La cantidad admite seis decimales y el lote hasta 100 caracteres.");
        ProductId = productId;
        ExpiryDate = expiryDate;
        ReceivedAtUtc = receivedAtUtc ?? DateTimeOffset.UtcNow;
        ReceivedQuantity = decimal.Round(quantity, 6);
        WarehouseId = warehouseId;
        IsOpeningBalance = isOpeningBalance;
        LotNumber = string.IsNullOrWhiteSpace(lotNumber) ? $"{(isOpeningBalance ? "APERTURA" : "LOTE")}-{Id:N}" : lotNumber.Trim();
        AvailableQuantity = decimal.Round(quantity, 6);
        SourceDocument = string.IsNullOrWhiteSpace(sourceDocument) ? null : sourceDocument.Trim();
        OriginLotId = originLotId;
        UnitCost = unitCost.HasValue ? decimal.Round(unitCost.Value, 6) : null;
    }

    public void SetOriginLotAndCost(Guid? originLotId, decimal? unitCost)
    {
        OriginLotId = originLotId;
        if (unitCost.HasValue)
        {
            UnitCost = decimal.Round(unitCost.Value, 6);
        }
    }

    public void AssignWarehouse(Guid warehouseId)
    {
        if (WarehouseId.HasValue || warehouseId == Guid.Empty)
            throw new DomainRuleException("El lote ya tiene bodega o la bodega no es válida.");
        WarehouseId = warehouseId;
    }

    public void Restore(decimal quantity)
    {
        if (quantity <= 0 || !ReceivedQuantity.HasValue || AvailableQuantity + quantity > ReceivedQuantity)
            throw new DomainRuleException("La devolución excede la cantidad original del lote.");
        AvailableQuantity += quantity;
    }

    public void Consume(decimal quantity)
    {
        if (quantity <= 0 || quantity > AvailableQuantity) throw new DomainRuleException("La cantidad del lote de caducidad no es válida.");
        AvailableQuantity = decimal.Round(AvailableQuantity - quantity, 6);
    }
}
