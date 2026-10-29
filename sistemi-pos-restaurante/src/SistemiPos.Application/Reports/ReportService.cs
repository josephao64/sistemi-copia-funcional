namespace SistemiPos.Application.Reports;

public sealed class ReportService(IReportRepository repository) : IReportService
{
    public Task<IReadOnlyList<SalesReportRow>> GetSalesReportAsync(SalesReportFilter filter, CancellationToken cancellationToken = default) =>
        repository.GetSalesReportAsync(filter, cancellationToken);

    public Task<IReadOnlyList<ProductSalesRankingRow>> GetProductSalesRankingAsync(ProductSalesRankingFilter filter, CancellationToken cancellationToken = default) =>
        repository.GetProductSalesRankingAsync(filter, cancellationToken);

    public Task<IReadOnlyList<CashierSalesReportRow>> GetCashierSalesReportAsync(SalesReportFilter filter, CancellationToken cancellationToken = default) =>
        repository.GetCashierSalesReportAsync(filter, cancellationToken);

    public Task<IReadOnlyList<DailySalesTotal>> GetDailySalesHistoryAsync(TimeSpan localOffset, CancellationToken cancellationToken = default) =>
        repository.GetDailySalesHistoryAsync(localOffset, cancellationToken);

    public Task<InventoryReport> GetInventoryReportAsync(CancellationToken cancellationToken = default) =>
        repository.GetInventoryReportAsync(cancellationToken);

    public Task<ProductReport> GetProductReportAsync(CancellationToken cancellationToken = default) =>
        repository.GetProductReportAsync(cancellationToken);

    public Task<IReadOnlyList<PurchaseReportRow>> GetPurchaseReportAsync(PurchaseReportFilter filter, CancellationToken cancellationToken = default) =>
        repository.GetPurchaseReportAsync(filter, cancellationToken);

    public Task<IReadOnlyList<CashReportRow>> GetCashReportAsync(CancellationToken cancellationToken = default) =>
        repository.GetCashReportAsync(cancellationToken);

    public Task<DashboardSummary> GetDashboardSummaryAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        repository.GetDashboardSummaryAsync(now, cancellationToken);
}
