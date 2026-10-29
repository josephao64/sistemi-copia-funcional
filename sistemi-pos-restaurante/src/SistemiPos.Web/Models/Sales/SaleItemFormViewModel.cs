namespace SistemiPos.Web.Models.Sales;

using SistemiPos.Domain.Sales;

public sealed class SaleItemFormViewModel
{
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; } = 1m;
    public decimal DiscountPercent { get; set; }
    public SaleDiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public string? DiscountComment { get; set; }
}
