using Microsoft.AspNetCore.Mvc.Rendering;
using SistemiPos.Application.Sales;

namespace SistemiPos.Web.Models.Sales;

public sealed class SaleDetailsViewModel
{
    public SaleDto Sale { get; set; } = null!;
    public CancelSaleViewModel Cancellation { get; set; } = new();
    public ReturnSaleViewModel Return { get; set; } = new();
    public RefundSaleViewModel Refund { get; set; } = new();
    public IReadOnlyList<SelectListItem> CashSessions { get; set; } = [];
}
