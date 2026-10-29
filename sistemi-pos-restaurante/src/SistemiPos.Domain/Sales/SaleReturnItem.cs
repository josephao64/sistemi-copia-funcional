using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Sales;

public sealed class SaleReturnItem : Entity
{
    public Guid SaleReturnId { get; set; }
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public bool ReturnedToInventory { get; private set; }

    private SaleReturnItem()
    {
    }

    public SaleReturnItem(Guid productId, decimal quantity, bool returnedToInventory)
    {
        if (productId == Guid.Empty || quantity <= 0)
        {
            throw new DomainRuleException("Los datos del detalle de devolución no son válidos.");
        }

        ProductId = productId;
        Quantity = decimal.Round(quantity, 2);
        ReturnedToInventory = returnedToInventory;
    }
}
