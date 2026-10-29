using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Inventory;

public sealed class InventoryTransferItem : Entity
{
    public Guid TransferId { get; internal set; }
    public Guid ProductId { get; private set; }
    public Product Product { get; set; } = null!;
    public decimal QuantitySent { get; private set; }
    public decimal QuantityReceived { get; private set; }
    public decimal UnitCostOrigin { get; private set; }
    public decimal AssignedUnitCost { get; private set; }
    public Guid? OriginLotId { get; private set; }
    public string? LotNumber { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }

    private InventoryTransferItem()
    {
    }

    public InventoryTransferItem(
        Guid productId,
        decimal quantitySent,
        decimal unitCostOrigin,
        decimal assignedUnitCost,
        Guid? originLotId = null,
        string? lotNumber = null,
        DateOnly? expiryDate = null)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainRuleException("El producto es requerido en la línea de traslado.");
        }

        if (quantitySent <= 0)
        {
            throw new DomainRuleException("La cantidad enviada debe ser mayor a cero.");
        }

        if (unitCostOrigin < 0 || assignedUnitCost < 0)
        {
            throw new DomainRuleException("El costo no puede ser negativo.");
        }

        ProductId = productId;
        QuantitySent = decimal.Round(quantitySent, 6);
        QuantityReceived = 0m;
        UnitCostOrigin = decimal.Round(unitCostOrigin, 4);
        AssignedUnitCost = decimal.Round(assignedUnitCost, 4);
        OriginLotId = originLotId;
        LotNumber = string.IsNullOrWhiteSpace(lotNumber) ? null : lotNumber.Trim();
        ExpiryDate = expiryDate;
    }

    public void RegisterReceived(decimal quantityReceived)
    {
        if (quantityReceived < 0)
        {
            throw new DomainRuleException("La cantidad recibida no puede ser negativa.");
        }

        if (quantityReceived > QuantitySent)
        {
            throw new DomainRuleException("La cantidad recibida no puede ser mayor a la cantidad enviada.");
        }

        QuantityReceived = decimal.Round(quantityReceived, 6);
    }
}
