namespace SistemiPos.Web.Models.Sales;

public sealed class ReturnSaleItemViewModel
{
    public Guid? LotId { get; set; }
    public IReadOnlyList<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> AvailableLots { get; set; } = [];
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal SoldQuantity { get; set; }
    public decimal Quantity { get; set; }
    public bool ReturnedToInventory { get; set; } = true;
}
