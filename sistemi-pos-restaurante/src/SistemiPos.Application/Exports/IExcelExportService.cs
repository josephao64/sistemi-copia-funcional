namespace SistemiPos.Application.Exports;

public enum ExcelCellType
{
    Text,
    Decimal,
    Integer,
    Date,
    DateTime
}

public sealed record ExcelColumn(string Header, ExcelCellType Type = ExcelCellType.Text, double Width = 18);
public sealed record ExcelReportLine(string Label, object? Value, ExcelCellType ValueType = ExcelCellType.Text);

public interface IExcelExportService
{
    byte[] CreateWorkbook(
        string sheetName,
        IReadOnlyList<ExcelColumn> columns,
        IReadOnlyList<IReadOnlyList<object?>> rows,
        IReadOnlyList<ExcelReportLine>? headerLines = null,
        IReadOnlyList<ExcelReportLine>? summaryLines = null);
}
