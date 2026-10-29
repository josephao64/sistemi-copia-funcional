using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemiPos.Application.Catalog;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Reports;
using SistemiPos.Application.Security;
using SistemiPos.Application.Settings;
using SistemiPos.Application.Suppliers;
using SistemiPos.Domain.Sales;
using SistemiPos.Web.Models.Reports;

namespace SistemiPos.Web.Controllers;

[Authorize(Policy = PermissionCodes.ReportsView)]
public sealed class ReportsController(
    IReportService reportService,
    ICatalogService catalogService,
    ISupplierService supplierService,
    IDateTimeProvider dateTimeProvider,
    ISettingsService settingsService) : Controller
{
    public async Task<IActionResult> Sales(
        string? period,
        DateTime? fromDate,
        DateTime? toDate,
        string? cashierUserName,
        Guid? productId,
        Guid? categoryId,
        PaymentMethodType? paymentMethod,
        int? latestTransactionsLimit,
        CancellationToken cancellationToken)
    {
        period = string.IsNullOrWhiteSpace(period) ? "month" : period.ToLowerInvariant();
        var localNow = dateTimeProvider.UtcNow.ToOffset(TimeSpan.FromHours(-6));
        var transactionLimit = latestTransactionsLimit is 10 or 20 or 30 or 50 ? latestTransactionsLimit.Value : 10;
        var today = localNow.Date;
        if (period == "today") { fromDate = today; toDate = today; }
        else if (period == "week") { fromDate = today.AddDays(-6); toDate = today; }
        else if (period == "month") { fromDate = new DateTime(today.Year, today.Month, 1); toDate = today; }
        else if (period == "year") { fromDate = new DateTime(today.Year, 1, 1); toDate = today; }
        else if (period == "custom" && fromDate is null && toDate is null)
        {
            fromDate = new DateTime(today.Year, today.Month, 1);
            toDate = today;
        }

        var customRangeError = period == "custom" && (!fromDate.HasValue || !toDate.HasValue)
            ? "Seleccione la fecha inicial y la fecha final para actualizar el reporte."
            : period == "custom" && fromDate > toDate
                ? "La fecha inicial no puede ser posterior a la fecha final."
                : null;

        var products = await catalogService.SearchProductsAsync(null, cancellationToken);
        var categories = await catalogService.GetCategoriesAsync(cancellationToken);
        var settings = await settingsService.GetSystemSettingAsync(cancellationToken);
        var dailySalesHistory = await reportService.GetDailySalesHistoryAsync(localNow.Offset, cancellationToken);
        IReadOnlyList<SalesReportRow> rows = [];
        if (customRangeError is null)
        {
            var reportFilter = new SalesReportFilter(
                fromDate is null ? null : new DateTimeOffset(DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Unspecified), localNow.Offset).ToUniversalTime(),
                toDate is null ? null : new DateTimeOffset(DateTime.SpecifyKind(toDate.Value.Date.AddDays(1), DateTimeKind.Unspecified), localNow.Offset).ToUniversalTime(),
                cashierUserName,
                productId,
                categoryId,
                paymentMethod);
            rows = await reportService.GetSalesReportAsync(reportFilter, cancellationToken);
        }
        return View(new SalesReportViewModel
        {
            Period = period,
            FromDate = fromDate,
            ToDate = toDate,
            CustomRangeError = customRangeError,
            CashierUserName = cashierUserName,
            ProductId = productId,
            CategoryId = categoryId,
            PaymentMethod = paymentMethod,
            Products = products.Where(x => !x.ManagesVariants).Select(x => new SelectListItem($"{x.InternalCode} - {x.DisplayName ?? x.Name}", x.Id.ToString())).ToList(),
            Categories = categories.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList(),
            Rows = rows,
            DailySalesHistory = dailySalesHistory,
            CurrentYear = today.Year,
            CurrentMonth = today.Month,
            CurrentDay = today.Day,
            LatestTransactionsLimit = transactionLimit,
            CurrencySymbol = settings.CurrencySymbol,
            CurrencyDecimals = settings.CurrencyDecimals,
            CurrencySymbolPosition = settings.CurrencySymbolPosition,
            IndicatorSummary = SalesIndicatorSummary.From(rows)
        });
    }

    public async Task<IActionResult> CashierSales(
        DateTime? fromDate,
        DateTime? toDate,
        string? cashierUserName,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        var localNow = dateTimeProvider.UtcNow.ToOffset(TimeSpan.FromHours(-6));
        var reportFilter = new SalesReportFilter(
            fromDate is null ? null : new DateTimeOffset(DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Unspecified), localNow.Offset).ToUniversalTime(),
            toDate is null ? null : new DateTimeOffset(DateTime.SpecifyKind(toDate.Value.Date.AddDays(1), DateTimeKind.Unspecified), localNow.Offset).ToUniversalTime(),
            null,
            null,
            null,
            null);
        var cashierRows = await reportService.GetCashierSalesReportAsync(reportFilter, cancellationToken);
        var settings = await settingsService.GetSystemSettingAsync(cancellationToken);
        var selectedCashier = string.IsNullOrWhiteSpace(cashierUserName) ? null : cashierUserName.Trim();

        return View(new CashierSalesViewModel
        {
            FromDate = fromDate,
            ToDate = toDate,
            CashierUserName = selectedCashier,
            ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null,
            Cashiers = cashierRows
                .Select(x => x.CashierUserName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .Select(x => new SelectListItem(x, x))
                .ToList(),
            Rows = selectedCashier is null
                ? cashierRows
                : cashierRows.Where(x => string.Equals(x.CashierUserName, selectedCashier, StringComparison.OrdinalIgnoreCase)).ToList(),
            CurrencySymbol = settings.CurrencySymbol,
            CurrencyDecimals = settings.CurrencyDecimals,
            CurrencySymbolPosition = settings.CurrencySymbolPosition
        });
    }

    public async Task<IActionResult> ProductSalesRanking(
        string? sort,
        DateTime? fromDate,
        DateTime? toDate,
        Guid? productId,
        Guid? categoryId,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        var localNow = dateTimeProvider.UtcNow.ToOffset(TimeSpan.FromHours(-6));
        var rows = await reportService.GetProductSalesRankingAsync(
            new ProductSalesRankingFilter(
                fromDate is null ? null : new DateTimeOffset(DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Unspecified), localNow.Offset).ToUniversalTime(),
                toDate is null ? null : new DateTimeOffset(DateTime.SpecifyKind(toDate.Value.Date.AddDays(1), DateTimeKind.Unspecified), localNow.Offset).ToUniversalTime(),
                productId,
                categoryId),
            cancellationToken);

        var products = await catalogService.SearchProductsAsync(null, cancellationToken);
        var categories = await catalogService.GetCategoriesAsync(cancellationToken);
        var settings = await settingsService.GetSystemSettingAsync(cancellationToken);
        var isMostSold = !string.Equals(sort, "least", StringComparison.OrdinalIgnoreCase);
        rows = isMostSold
            ? rows.OrderByDescending(x => x.UnitsSold).ThenBy(x => x.ProductName).ToList()
            : rows.OrderBy(x => x.UnitsSold).ThenBy(x => x.ProductName).ToList();
        return View(new ProductSalesRankingViewModel
        {
            IsMostSold = isMostSold,
            FromDate = fromDate,
            ToDate = toDate,
            ProductId = productId,
            CategoryId = categoryId,
            ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null,
            Products = products.Where(x => !x.ManagesVariants).Select(x => new SelectListItem($"{x.InternalCode} - {x.DisplayName ?? x.Name}", x.Id.ToString())).ToList(),
            Categories = categories.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList(),
            Rows = rows,
            CurrencySymbol = settings.CurrencySymbol,
            CurrencyDecimals = settings.CurrencyDecimals,
            CurrencySymbolPosition = settings.CurrencySymbolPosition
        });
    }

    public async Task<IActionResult> Inventory(CancellationToken cancellationToken) =>
        View(new InventoryReportViewModel
        {
            Report = await reportService.GetInventoryReportAsync(cancellationToken)
        });

    public async Task<IActionResult> Products(CancellationToken cancellationToken) =>
        View(new ProductReportViewModel
        {
            Report = await reportService.GetProductReportAsync(cancellationToken)
        });

    public async Task<IActionResult> Purchases(
        DateTime? fromDate,
        DateTime? toDate,
        Guid? supplierId,
        Guid? productId,
        CancellationToken cancellationToken)
    {
        var suppliers = await supplierService.GetSuppliersAsync(cancellationToken);
        var products = await catalogService.SearchProductsAsync(null, cancellationToken);
        var rows = await reportService.GetPurchaseReportAsync(
            new PurchaseReportFilter(
                fromDate is null ? null : new DateTimeOffset(fromDate.Value.Date, TimeSpan.Zero),
                toDate is null ? null : new DateTimeOffset(toDate.Value.Date.AddDays(1).AddTicks(-1), TimeSpan.Zero),
                supplierId,
                productId),
            cancellationToken);

        return View(new PurchaseReportViewModel
        {
            FromDate = fromDate,
            ToDate = toDate,
            SupplierId = supplierId,
            ProductId = productId,
            Suppliers = suppliers.Select(x => new SelectListItem(x.TradeName ?? x.LegalName, x.Id.ToString())).ToList(),
            Products = products.Where(x => !x.ManagesVariants).Select(x => new SelectListItem($"{x.InternalCode} - {x.DisplayName ?? x.Name}", x.Id.ToString())).ToList(),
            Rows = rows
        });
    }

    public async Task<IActionResult> Cash(CancellationToken cancellationToken) =>
        View(new CashReportViewModel
        {
            Rows = await reportService.GetCashReportAsync(cancellationToken)
        });
}
