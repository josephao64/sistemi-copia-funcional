namespace SistemiPos.Application.Reports;

public interface IReportService
{
    Task<IReadOnlyList<SalesReportRow>> GetSalesReportAsync(SalesReportFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductSalesRankingRow>> GetProductSalesRankingAsync(ProductSalesRankingFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CashierSalesReportRow>> GetCashierSalesReportAsync(SalesReportFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DailySalesTotal>> GetDailySalesHistoryAsync(TimeSpan localOffset, CancellationToken cancellationToken = default);
    Task<InventoryReport> GetInventoryReportAsync(CancellationToken cancellationToken = default);
    Task<ProductReport> GetProductReportAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseReportRow>> GetPurchaseReportAsync(PurchaseReportFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CashReportRow>> GetCashReportAsync(CancellationToken cancellationToken = default);
    Task<DashboardSummary> GetDashboardSummaryAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
