using SistemiPos.Application.Exports;
using SistemiPos.Application.Organizations;

namespace SistemiPos.Application.Reports;

public sealed record ControlQuery(string Report = "sales", DateOnly? From = null, DateOnly? To = null,
    Guid? CompanyId = null, Guid? BranchId = null, Guid? WarehouseId = null, string? User = null,
    string? Register = null, int Page = 1, Guid? ProductId = null, Guid? SupplierId = null, bool OutOfStock = false);
public sealed record ControlRow(IReadOnlyList<object?> Cells, string? DetailUrl = null);
public sealed record ControlMetric(string Label, decimal Value, string? Currency = null);
public sealed record ControlAlert(string Label, int Count, string Url);
public sealed record ControlReport(ControlQuery Query, string Title, IReadOnlyList<BranchDto> Branches,
    IReadOnlyList<ExcelColumn> Columns, IReadOnlyList<ControlRow> Rows, int TotalCount, int PageSize,
    IReadOnlyList<ControlMetric> Metrics, IReadOnlyList<ControlAlert> Alerts, DateTimeOffset UpdatedAt,
    string? Note = null, IReadOnlyList<ControlWarehouse>? Warehouses = null, string? ScopeDescription = null);
public sealed record ControlWarehouse(Guid Id, string Name);
public interface IControlReportService
{
    Task<ControlReport> GetAsync(ControlQuery query, CancellationToken ct = default);
    Task<byte[]> ExportExcelAsync(ControlQuery query, CancellationToken ct = default);
    Task<byte[]> ExportPdfAsync(ControlQuery query, CancellationToken ct = default);
}
public interface ITablePdfWriter
{
    byte[] Write(string title, string context, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows);
}
