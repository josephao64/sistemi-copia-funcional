using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemiPos.Application.Reports;
using SistemiPos.Application.Security;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Cash;
using SistemiPos.Application.Exports;
using SistemiPos.Application.Finance;
using SistemiPos.Application.Inventory;
using SistemiPos.Application.Purchases;
using SistemiPos.Application.Settings;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Purchases;
using SistemiPos.Web.Models.Home;

namespace SistemiPos.Web.Controllers;

[Authorize(Policy = PermissionCodes.DashboardView)]
public sealed class HomeController(
    IReportService reportService,
    IDateTimeProvider dateTimeProvider,
    IExcelExportService excelExportService,
    ICashService cashService,
    IFinancialService financialService,
    IInventoryQueryService inventoryQueryService,
    IPurchaseService purchaseService,
    ISettingsService settingsService,
    IOperationContextAccessor? operationContext = null) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var localNow = dateTimeProvider.UtcNow.ToOffset(TimeSpan.FromHours(-6));
        var cashSessions = await cashService.GetSessionsAsync(cancellationToken);
        var receivables = await financialService.GetReceivablesAsync(new ReceivableQuery(), cancellationToken);
        var dashboard = await GetDashboardAsync(cancellationToken);
        var expiryLots = await inventoryQueryService.GetExpiryLotsAsync(cancellationToken);
        var purchaseOrders = await purchaseService.GetPurchaseOrdersAsync(cancellationToken);
        var settings = await settingsService.GetSystemSettingAsync(cancellationToken);
        var currentBranchId = operationContext?.CurrentBranchId;

        return View(new DashboardOperationalViewModel
        {
            UpdatedAt = localNow,
            OpenCashSessionsCount = cashSessions.Count(x => x.Status == CashSessionStatus.Active && (currentBranchId == null || x.BranchId == null || x.BranchId == currentBranchId)),
            OverdueReceivablesCount = receivables.Summary.OverdueCount + receivables.Summary.DelinquentCount,
            OverdueReceivablesAmount = receivables.Summary.OverdueAmount + receivables.Summary.DelinquentAmount,
            LowStockCount = dashboard.LowStock.Count,
            OutOfStockCount = dashboard.OutOfStock.Count,
            ExpiringLotsCount = expiryLots.Count,
            PendingReceiptsCount = purchaseOrders.Count(x =>
                (x.Status is PurchaseOrderStatus.Requested or PurchaseOrderStatus.PartiallyReceived)
                && x.Items.Any(item => item.PendingQuantity > 0)),
            CurrencySymbol = settings.CurrencySymbol,
            CurrencyDecimals = settings.CurrencyDecimals,
            CurrencySymbolPosition = settings.CurrencySymbolPosition
        });
    }

    [HttpGet]
    public async Task<IActionResult> LowStock(CancellationToken cancellationToken) =>
        View("Detail", BuildDetail(await GetDashboardAsync(cancellationToken), DashboardDetailSection.LowStock));

    [HttpGet]
    public async Task<IActionResult> OutOfStock(CancellationToken cancellationToken) =>
        View("Detail", BuildDetail(await GetDashboardAsync(cancellationToken), DashboardDetailSection.OutOfStock));

    [HttpGet]
    public async Task<IActionResult> PendingOrders(CancellationToken cancellationToken) =>
        View("Detail", BuildDetail(await GetDashboardAsync(cancellationToken), DashboardDetailSection.PendingOrders));

    [HttpGet]
    public async Task<IActionResult> WithoutMovement(CancellationToken cancellationToken) =>
        View("Detail", BuildDetail(await GetDashboardAsync(cancellationToken), DashboardDetailSection.WithoutMovement));

    [HttpGet]
    public async Task<IActionResult> Export(string section, CancellationToken cancellationToken)
    {
        var dashboard = await GetDashboardAsync(cancellationToken);
        var (sheetName, columns, rows, filePrefix) = section switch
        {
            "low-stock" => (
                "Existencia baja",
                (IReadOnlyList<ExcelColumn>)[
                    new("Código", ExcelCellType.Text, 16),
                    new("Producto", ExcelCellType.Text, 34),
                    new("Existencia actual", ExcelCellType.Decimal, 18),
                    new("Existencia mínima", ExcelCellType.Decimal, 18)],
                dashboard.LowStock.Select(x => (IReadOnlyList<object?>)[x.InternalCode, x.ProductName, x.CurrentStock, x.MinimumStock]).ToList(),
                "productos-existencia-baja"),
            "out-of-stock" => (
                "Productos agotados",
                (IReadOnlyList<ExcelColumn>)[
                    new("Código", ExcelCellType.Text, 16),
                    new("Producto", ExcelCellType.Text, 34),
                    new("Existencia actual", ExcelCellType.Decimal, 18)],
                dashboard.OutOfStock.Select(x => (IReadOnlyList<object?>)[x.InternalCode, x.ProductName, x.CurrentStock]).ToList(),
                "productos-agotados"),
            "pending-orders" => (
                "Órdenes pendientes",
                (IReadOnlyList<ExcelColumn>)[
                    new("Orden", ExcelCellType.Text, 18),
                    new("Proveedor", ExcelCellType.Text, 34),
                    new("Fecha de solicitud", ExcelCellType.Date, 20),
                    new("Días transcurridos", ExcelCellType.Integer, 18),
                    new("Estado", ExcelCellType.Text, 24)],
                dashboard.PendingPurchaseOrders.Select(x => (IReadOnlyList<object?>)[
                    x.Number,
                    x.SupplierName,
                    x.RequestedAtUtc.ToOffset(dashboard.UpdatedAt.Offset),
                    x.ElapsedDays,
                    PurchaseOrderStatusLabel(x.Status)]).ToList(),
                "pedidos-pendientes"),
            "without-movement" => (
                "Sin movimiento",
                (IReadOnlyList<ExcelColumn>)[
                    new("Código", ExcelCellType.Text, 16),
                    new("Producto", ExcelCellType.Text, 34),
                    new("Existencia actual", ExcelCellType.Decimal, 18),
                    new("Fecha de última venta", ExcelCellType.Date, 22),
                    new("Días sin movimiento", ExcelCellType.Integer, 20),
                    new("Días configurados", ExcelCellType.Integer, 18)],
                dashboard.ProductsWithoutMovement.Select(x => (IReadOnlyList<object?>)[
                    x.InternalCode,
                    x.ProductName,
                    x.CurrentStock,
                    x.LastSaleAtUtc?.ToOffset(dashboard.UpdatedAt.Offset),
                    x.DaysWithoutMovement,
                    x.ConfiguredDays]).ToList(),
                "productos-sin-movimiento"),
            _ => throw new ArgumentException("La sección solicitada no es válida.", nameof(section))
        };

        var content = excelExportService.CreateWorkbook(sheetName, columns, rows);
        return File(
            content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"{filePrefix}-{dashboard.UpdatedAt:yyyyMMdd-HHmm}.xlsx");
    }

    private Task<DashboardSummary> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var localNow = dateTimeProvider.UtcNow.ToOffset(TimeSpan.FromHours(-6));
        return reportService.GetDashboardSummaryAsync(localNow, cancellationToken);
    }

    private static DashboardDetailViewModel BuildDetail(DashboardSummary dashboard, DashboardDetailSection section) =>
        section switch
        {
            DashboardDetailSection.LowStock => new DashboardDetailViewModel { Section = section, Title = "Productos con existencia baja", ExportSection = "low-stock", UpdatedAt = dashboard.UpdatedAt, StockRows = dashboard.LowStock },
            DashboardDetailSection.OutOfStock => new DashboardDetailViewModel { Section = section, Title = "Productos agotados", ExportSection = "out-of-stock", UpdatedAt = dashboard.UpdatedAt, StockRows = dashboard.OutOfStock },
            DashboardDetailSection.PendingOrders => new DashboardDetailViewModel { Section = section, Title = "Órdenes pendientes", ExportSection = "pending-orders", UpdatedAt = dashboard.UpdatedAt, PendingOrders = dashboard.PendingPurchaseOrders },
            _ => new DashboardDetailViewModel { Section = section, Title = "Productos sin movimiento", ExportSection = "without-movement", UpdatedAt = dashboard.UpdatedAt, ProductsWithoutMovement = dashboard.ProductsWithoutMovement }
        };

    private static string PurchaseOrderStatusLabel(PurchaseOrderStatus status) => status switch
    {
        PurchaseOrderStatus.Draft => "Borrador",
        PurchaseOrderStatus.Requested => "Solicitada",
        PurchaseOrderStatus.PartiallyReceived => "Recibida parcialmente",
        PurchaseOrderStatus.Received => "Recibida",
        _ => "Cancelado"
    };
}
