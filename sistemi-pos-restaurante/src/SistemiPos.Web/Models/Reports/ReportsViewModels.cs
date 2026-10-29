using Microsoft.AspNetCore.Mvc.Rendering;
using SistemiPos.Application.Reports;
using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Sales;

namespace SistemiPos.Web.Models.Reports;

public sealed class SalesReportViewModel
{
    public string Period { get; set; } = "month";
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? CustomRangeError { get; set; }
    public string? CashierUserName { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? CategoryId { get; set; }
    public PaymentMethodType? PaymentMethod { get; set; }
    public IReadOnlyList<SelectListItem> Products { get; set; } = [];
    public IReadOnlyList<SelectListItem> Categories { get; set; } = [];
    public IReadOnlyList<SalesReportRow> Rows { get; set; } = [];
    public IReadOnlyList<DailySalesTotal> DailySalesHistory { get; set; } = [];
    public int CurrentYear { get; set; }
    public int CurrentMonth { get; set; }
    public int CurrentDay { get; set; }
    public int LatestTransactionsLimit { get; set; } = 10;
    public string CurrencySymbol { get; set; } = "Q";
    public int CurrencyDecimals { get; set; } = 2;
    public CurrencySymbolPosition CurrencySymbolPosition { get; set; } = CurrencySymbolPosition.Before;
    public SalesIndicatorSummary IndicatorSummary { get; set; } = SalesIndicatorSummary.From([]);
}

public sealed class CashierSalesViewModel
{
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public string? CashierUserName { get; init; }
    public string? ReturnUrl { get; init; }
    public IReadOnlyList<SelectListItem> Cashiers { get; init; } = [];
    public IReadOnlyList<CashierSalesReportRow> Rows { get; init; } = [];
    public string CurrencySymbol { get; init; } = "Q";
    public int CurrencyDecimals { get; init; } = 2;
    public CurrencySymbolPosition CurrencySymbolPosition { get; init; } = CurrencySymbolPosition.Before;
}

public sealed class ProductSalesRankingViewModel
{
    public bool IsMostSold { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public Guid? ProductId { get; init; }
    public Guid? CategoryId { get; init; }
    public string? ReturnUrl { get; init; }
    public IReadOnlyList<SelectListItem> Products { get; init; } = [];
    public IReadOnlyList<SelectListItem> Categories { get; init; } = [];
    public IReadOnlyList<ProductSalesRankingRow> Rows { get; init; } = [];
    public string CurrencySymbol { get; init; } = "Q";
    public int CurrencyDecimals { get; init; } = 2;
    public CurrencySymbolPosition CurrencySymbolPosition { get; init; } = CurrencySymbolPosition.Before;
}

public sealed class PurchaseReportViewModel
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? ProductId { get; set; }
    public IReadOnlyList<SelectListItem> Suppliers { get; set; } = [];
    public IReadOnlyList<SelectListItem> Products { get; set; } = [];
    public IReadOnlyList<PurchaseReportRow> Rows { get; set; } = [];
}

public sealed class InventoryReportViewModel
{
    public InventoryReport Report { get; set; } = null!;
}

public sealed class ProductReportViewModel
{
    public ProductReport Report { get; set; } = null!;
}

public sealed class CashReportViewModel
{
    public IReadOnlyList<CashReportRow> Rows { get; set; } = [];
}
