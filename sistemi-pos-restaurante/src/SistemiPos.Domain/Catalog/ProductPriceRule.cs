using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Catalog;

public sealed class ProductPriceRule : Entity
{
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public decimal Price { get; private set; }
    public bool IsActive { get; private set; } = true;

    private ProductPriceRule()
    {
    }

    public ProductPriceRule(Guid productId, decimal quantity, decimal price, bool isActive = true)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainRuleException("El producto de la regla de precio es requerido.");
        }

        ProductId = productId;
        Update(quantity, price, isActive);
    }

    public void Update(decimal quantity, decimal price, bool isActive)
    {
        var normalizedQuantity = decimal.Round(quantity, 2);
        if (normalizedQuantity <= 0)
        {
            throw new DomainRuleException("La cantidad de la regla de precio debe ser mayor que cero.");
        }

        if (price < 0)
        {
            throw new DomainRuleException("El precio de la regla no puede ser negativo.");
        }

        Quantity = normalizedQuantity;
        Price = decimal.Round(price, 2);
        IsActive = isActive;
    }
}
