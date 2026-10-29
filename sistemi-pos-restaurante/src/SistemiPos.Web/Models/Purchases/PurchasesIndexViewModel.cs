using SistemiPos.Application.Purchases;

namespace SistemiPos.Web.Models.Purchases;

public sealed class PurchasesIndexViewModel
{
    public IReadOnlyList<PurchaseDto> Purchases { get; init; } = [];
    public IReadOnlyList<PurchaseOrderDto> Orders { get; init; } = [];
    public SistemiPos.Domain.Purchases.PurchaseOrderStatus? Status { get; init; }
    public Guid? SupplierId { get; init; }
    public string? SupplierName { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public IReadOnlyList<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Suppliers { get; init; } = [];
    public string Section { get; init; } = "orders";
    public string Title { get; init; } = "Órdenes de compra";
}

public sealed class PurchaseHistoryViewModel
{
    public IReadOnlyList<PurchaseDto> Purchases { get; init; } = [];
    public Guid? SupplierId { get; init; }
    public string? SupplierName { get; init; }
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public SistemiPos.Domain.Purchases.PurchaseTermsType? Terms { get; init; }
    public SistemiPos.Domain.Common.DocumentStatus? Status { get; init; }
    public IReadOnlyList<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Suppliers { get; init; } = [];
    public string CurrencySymbol { get; init; } = "Q";
    public int CurrencyDecimals { get; init; } = 2;
}
