namespace SistemiPos.Web.Models.Sales;

public sealed class ReturnSaleViewModel
{
    public Guid SaleId { get; set; }
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(80)]
    public string OperationId { get; set; } = "RETURN-" + Guid.NewGuid().ToString("N");
    public string Reason { get; set; } = string.Empty;
    public List<ReturnSaleItemViewModel> Items { get; set; } = [];
    public string? AuthorizerUserName { get; set; }
    public string? AuthorizerPassword { get; set; }
}
