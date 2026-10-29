using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Organizations;

namespace SistemiPos.Domain.Inventory;

public sealed class InventoryStock : AuditableEntity
{
    public Guid ProductId { get; private set; }
    public Product Product { get; set; } = null!;
    public Guid? WarehouseId { get; private set; }
    public Warehouse? Warehouse { get; set; }
    public decimal CurrentStock { get; private set; }
    public decimal ReservedQuantity { get; private set; }
    public uint Version { get; private set; }

    private InventoryStock()
    {
    }

    public InventoryStock(Guid productId, Guid? warehouseId = null)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainRuleException("El producto es requerido.");
        }

        ProductId = productId;
        WarehouseId = warehouseId;
        CurrentStock = 0m;
    }

    public void AssignWarehouse(Guid warehouseId)
    {
        if (warehouseId == Guid.Empty)
        {
            throw new DomainRuleException("La bodega es requerida.");
        }

        WarehouseId = warehouseId;
    }

    public void ReplaceForSystem(decimal stock)
    {
        if (stock < ReservedQuantity)
        {
            throw new DomainRuleException("La existencia no puede ser negativa ni utilizar cantidades reservadas para pedidos.");
        }

        CurrentStock = decimal.Round(stock, 6);
    }

    public void ReserveForOrder(decimal quantity)
    {
        if (quantity <= 0 || CurrentStock - ReservedQuantity < quantity) throw new DomainRuleException("Existencia insuficiente después de las reservas de pedidos.");
        ReservedQuantity += quantity;
    }
    public void ReleaseOrderReservation(decimal quantity)
    {
        if (quantity < 0 || quantity > ReservedQuantity) throw new DomainRuleException("La reserva de inventario no coincide.");
        ReservedQuantity -= quantity;
    }
}
