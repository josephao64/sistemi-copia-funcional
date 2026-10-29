using SistemiPos.Application.Reports;

namespace SistemiPos.Web.Models.Home;

public enum DashboardDetailSection
{
    LowStock,
    OutOfStock,
    PendingOrders,
    WithoutMovement
}

public sealed class DashboardDetailViewModel
{
    public DashboardDetailSection Section { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ExportSection { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; init; }
    public IReadOnlyList<InventoryStockReportRow> StockRows { get; init; } = [];
    public IReadOnlyList<PendingPurchaseOrderRow> PendingOrders { get; init; } = [];
    public IReadOnlyList<NoMovementProductRow> ProductsWithoutMovement { get; init; } = [];
    public int Count => Section switch
    {
        DashboardDetailSection.PendingOrders => PendingOrders.Count,
        DashboardDetailSection.WithoutMovement => ProductsWithoutMovement.Count,
        _ => StockRows.Count
    };
}
