namespace SistemiPos.Web.Models.Sales;

public sealed class CancelSaleViewModel
{
    public Guid SaleId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? AuthorizerUserName { get; set; }
    public string? AuthorizerPassword { get; set; }
}
