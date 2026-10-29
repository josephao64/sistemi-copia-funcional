using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Common;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Sales;
using SistemiPos.Domain.Purchases;

namespace SistemiPos.Application.Reports;

public sealed record SalesReportFilter(
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    string? CashierUserName,
    Guid? ProductId,
    Guid? CategoryId,
    PaymentMethodType? PaymentMethod);

public sealed record ProductSalesRankingFilter(
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    Guid? ProductId,
    Guid? CategoryId);

public sealed record ProductSalesRankingRow(
    Guid ProductId,
    string ProductName,
    string CategoryName,
    decimal UnitsSold,
    decimal TotalSold);

public sealed record CashierSalesReportRow(
    string CashierUserName,
    int SalesCount,
    decimal UnitsSold,
    decimal TotalSold);

public sealed record SalesReportRow(
    Guid SaleId,
    string Number,
    DateTimeOffset SoldAtUtc,
    string CashierUserName,
    string CustomerName,
    PaymentMethodType PaymentMethod,
    SaleStatus Status,
    decimal Total,
    decimal DiscountTotal = 0m,
    decimal TaxTotal = 0m,
    decimal UnitsSold = 0m,
    decimal CostOfSales = 0m,
    IReadOnlyList<SalesReportProductRow>? Products = null);

public sealed record SalesReportProductRow(Guid ProductId, string ProductName, decimal Quantity);

public sealed record DailySalesTotal(int Year, int Month, int Day, decimal Total);

public sealed record SalesIndicatorSummary(
    decimal TotalSold,
    decimal SalesWithoutTaxes,
    decimal Taxes,
    decimal GrossProfit,
    decimal CostOfSales,
    decimal ProfitPercentage,
    int SalesCount,
    decimal AverageTicket,
    decimal UnitsSold,
    decimal Discounts,
    int ReturnsAndCancellations,
    decimal CashSales,
    decimal TransferSales,
    decimal PosSales,
    decimal CreditSales)
{
    public static SalesIndicatorSummary From(IReadOnlyList<SalesReportRow> rows)
    {
        var completedSales = rows.Where(x => x.Status == SaleStatus.Confirmed).ToList();
        var totalSold = completedSales.Sum(x => x.Total);
        var taxes = completedSales.Sum(x => x.TaxTotal);
        var salesWithoutTaxes = totalSold - taxes;
        var costOfSales = completedSales.Sum(x => x.CostOfSales);
        var grossProfit = salesWithoutTaxes - costOfSales;

        return new SalesIndicatorSummary(
            totalSold,
            salesWithoutTaxes,
            taxes,
            grossProfit,
            costOfSales,
            salesWithoutTaxes == 0 ? 0m : grossProfit / salesWithoutTaxes * 100m,
            completedSales.Count,
            completedSales.Count == 0 ? 0m : totalSold / completedSales.Count,
            completedSales.Sum(x => x.UnitsSold),
            rows.Sum(x => x.DiscountTotal),
            rows.Count(x => x.Status != SaleStatus.Confirmed),
            completedSales.Where(x => x.PaymentMethod == PaymentMethodType.Cash).Sum(x => x.Total),
            completedSales.Where(x => x.PaymentMethod == PaymentMethodType.Transfer).Sum(x => x.Total),
            completedSales.Where(x => x.PaymentMethod == PaymentMethodType.Pos).Sum(x => x.Total),
            completedSales.Where(x => x.PaymentMethod == PaymentMethodType.Credit).Sum(x => x.Total));
    }
}

public sealed record InventoryMovementReportRow(
    DateTimeOffset OccurredAtUtc,
    string ProductName,
    InventoryMovementType Type,
    decimal Quantity,
    decimal PreviousStock,
    decimal NewStock,
    string UserName,
    string? SourceDocument);

public sealed record InventoryStockReportRow(
    Guid ProductId,
    string InternalCode,
    string ProductName,
    string CategoryName,
    decimal CurrentStock,
    decimal MinimumStock);

public sealed record InventoryReport(
    IReadOnlyList<InventoryStockReportRow> Stocks,
    IReadOnlyList<InventoryStockReportRow> LowStock,
    IReadOnlyList<InventoryStockReportRow> OutOfStock,
    IReadOnlyList<InventoryMovementReportRow> Entries,
    IReadOnlyList<InventoryMovementReportRow> Exits,
    IReadOnlyList<InventoryMovementReportRow> Adjustments,
    IReadOnlyList<InventoryMovementReportRow> Kardex);

public sealed record ProductReportRow(
    Guid ProductId,
    string InternalCode,
    string ProductName,
    decimal Quantity,
    string Metric);

public sealed record ProductReport(
    IReadOnlyList<ProductReportRow> MostSold,
    IReadOnlyList<ProductReportRow> LeastSold,
    IReadOnlyList<ProductReportRow> WithoutMovement)
{
    public static ProductReport FromSales(IReadOnlyList<SalesReportRow> rows)
    {
        var soldProducts = rows
            .Where(x => x.Status == SaleStatus.Confirmed)
            .SelectMany(x => x.Products ?? [])
            .GroupBy(x => new { x.ProductId, x.ProductName })
            .Select(x => new ProductReportRow(x.Key.ProductId, string.Empty, x.Key.ProductName, x.Sum(item => item.Quantity), "Ventas"))
            .ToList();

        return new ProductReport(
            soldProducts.OrderByDescending(x => x.Quantity).Take(10).ToList(),
            soldProducts.OrderBy(x => x.Quantity).Take(10).ToList(),
            []);
    }
}

public sealed record PurchaseReportFilter(
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    Guid? SupplierId,
    Guid? ProductId);

public sealed record PurchaseReportRow(
    Guid PurchaseId,
    string Number,
    DateTimeOffset PurchaseDateUtc,
    string SupplierName,
    string UserName,
    DocumentStatus Status,
    decimal Total);

public sealed record CashReportRow(
    Guid CashSessionId,
    string RegisterCode,
    string CashierUserName,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    decimal OpeningAmount,
    decimal? TotalSold,
    decimal? TotalCashSales,
    decimal? TotalTransferSales,
    decimal? TotalPosSales,
    decimal? OtherIncomeTotal,
    decimal? ExpenseTotal,
    decimal? TotalCashRefunds,
    decimal? TotalTransferRefunds,
    decimal? TotalPosRefunds,
    decimal? ExpectedCash,
    decimal? Difference);

public sealed record RegisterSalesRow(string RegisterCode, decimal TotalSold);

public sealed record PendingPurchaseOrderRow(
    Guid OrderId,
    string Number,
    string SupplierName,
    DateTimeOffset RequestedAtUtc,
    int ElapsedDays,
    PurchaseOrderStatus Status);

public sealed record NoMovementProductRow(
    Guid ProductId,
    string InternalCode,
    string ProductName,
    decimal CurrentStock,
    DateTimeOffset? LastSaleAtUtc,
    int DaysWithoutMovement,
    int ConfiguredDays);

public sealed record DashboardSummary(
    DateTimeOffset UpdatedAt,
    string CurrencySymbol,
    decimal TodaySalesTotal,
    IReadOnlyList<RegisterSalesRow> SalesByRegister,
    IReadOnlyList<InventoryStockReportRow> LowStock,
    IReadOnlyList<InventoryStockReportRow> OutOfStock,
    IReadOnlyList<PendingPurchaseOrderRow> PendingPurchaseOrders,
    IReadOnlyList<NoMovementProductRow> ProductsWithoutMovement);
