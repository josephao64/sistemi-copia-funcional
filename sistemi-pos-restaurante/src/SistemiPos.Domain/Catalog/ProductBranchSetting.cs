using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Organizations;

namespace SistemiPos.Domain.Catalog;

public sealed class ProductBranchSetting : AuditableEntity
{
    public Guid ProductId { get; private set; }
    public Product Product { get; set; } = null!;
    public Guid BranchId { get; private set; }
    public Branch Branch { get; set; } = null!;
    public bool IsEnabled { get; private set; } = true;
    public bool IsVisible { get; private set; } = true;
    public decimal? SalePrice { get; private set; }
    public decimal? MinimumStock { get; private set; }

    private ProductBranchSetting()
    {
    }

    public ProductBranchSetting(
        Guid productId,
        Guid branchId,
        bool isEnabled = true,
        bool isVisible = true,
        decimal? salePrice = null,
        decimal? minimumStock = null)
    {
        if (productId == Guid.Empty)
            throw new DomainRuleException("El producto es requerido.");
        if (branchId == Guid.Empty)
            throw new DomainRuleException("La sucursal es requerida.");
        if (salePrice.HasValue && salePrice.Value < 0)
            throw new DomainRuleException("El precio de venta no puede ser negativo.");
        if (minimumStock.HasValue && minimumStock.Value < 0)
            throw new DomainRuleException("El stock mínimo no puede ser negativo.");

        ProductId = productId;
        BranchId = branchId;
        IsEnabled = isEnabled;
        IsVisible = isVisible;
        SalePrice = salePrice.HasValue ? decimal.Round(salePrice.Value, 2) : null;
        MinimumStock = minimumStock.HasValue ? decimal.Round(minimumStock.Value, 2) : null;
    }

    public void Update(bool isEnabled, bool isVisible, decimal? salePrice, decimal? minimumStock)
    {
        if (salePrice.HasValue && salePrice.Value < 0)
            throw new DomainRuleException("El precio de venta no puede ser negativo.");
        if (minimumStock.HasValue && minimumStock.Value < 0)
            throw new DomainRuleException("El stock mínimo no puede ser negativo.");

        IsEnabled = isEnabled;
        IsVisible = isVisible;
        SalePrice = salePrice.HasValue ? decimal.Round(salePrice.Value, 2) : null;
        MinimumStock = minimumStock.HasValue ? decimal.Round(minimumStock.Value, 2) : null;
    }
}
