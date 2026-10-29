namespace SistemiPos.Web.Models.Sales;

public sealed record SalePdfRequestViewModel(Guid SaleId, string SaleNumber, Guid RequestKey);
