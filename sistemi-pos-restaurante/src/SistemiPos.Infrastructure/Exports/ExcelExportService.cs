using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using SistemiPos.Application.Exports;

namespace SistemiPos.Infrastructure.Exports;

public sealed class ExcelExportService : IExcelExportService
{
    private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public byte[] CreateWorkbook(
        string sheetName,
        IReadOnlyList<ExcelColumn> columns,
        IReadOnlyList<IReadOnlyList<object?>> rows,
        IReadOnlyList<ExcelReportLine>? headerLines = null,
        IReadOnlyList<ExcelReportLine>? summaryLines = null)
    {
        if (columns.Count == 0)
        {
            throw new ArgumentException("El archivo Excel debe incluir al menos una columna.", nameof(columns));
        }

        var safeSheetName = SanitizeSheetName(sheetName);
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddTextEntry(archive, "[Content_Types].xml", ContentTypesXml);
            AddTextEntry(archive, "_rels/.rels", RootRelationshipsXml);
            AddTextEntry(archive, "xl/workbook.xml", CreateWorkbookXml(safeSheetName));
            AddTextEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationshipsXml);
            AddTextEntry(archive, "xl/styles.xml", StylesXml);
            AddSheetEntry(archive, columns, rows, headerLines ?? [], summaryLines ?? []);
        }

        return output.ToArray();
    }

    private static void AddSheetEntry(
        ZipArchive archive,
        IReadOnlyList<ExcelColumn> columns,
        IReadOnlyList<IReadOnlyList<object?>> rows,
        IReadOnlyList<ExcelReportLine> headerLines,
        IReadOnlyList<ExcelReportLine> summaryLines)
    {
        var columnHeaderRow = headerLines.Count == 0 ? 1 : headerLines.Count + 2;
        var firstDataRow = columnHeaderRow + 1;
        var lastDataRow = columnHeaderRow + rows.Count;
        var summaryStartRow = summaryLines.Count == 0 ? lastDataRow : lastDataRow + 2;
        var lastRow = summaryLines.Count == 0 ? Math.Max(columnHeaderRow, lastDataRow) : summaryStartRow + summaryLines.Count - 1;
        var entry = archive.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Fastest);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false });
        writer.WriteStartDocument();
        writer.WriteStartElement("worksheet", SpreadsheetNamespace);
        writer.WriteStartElement("dimension");
        writer.WriteAttributeString("ref", $"A1:{ColumnName(columns.Count)}{lastRow}");
        writer.WriteEndElement();
        writer.WriteStartElement("sheetViews");
        writer.WriteStartElement("sheetView");
        writer.WriteAttributeString("workbookViewId", "0");
        writer.WriteStartElement("pane");
        writer.WriteAttributeString("ySplit", columnHeaderRow.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("topLeftCell", $"A{firstDataRow}");
        writer.WriteAttributeString("activePane", "bottomLeft");
        writer.WriteAttributeString("state", "frozen");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("cols");
        for (var index = 0; index < columns.Count; index++)
        {
            writer.WriteStartElement("col");
            writer.WriteAttributeString("min", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("max", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("width", columns[index].Width.ToString("0.##", CultureInfo.InvariantCulture));
            writer.WriteAttributeString("customWidth", "1");
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteStartElement("sheetData");

        for (var headerIndex = 0; headerIndex < headerLines.Count; headerIndex++)
        {
            var rowNumber = headerIndex + 1;
            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
            WriteTextCell(writer, $"A{rowNumber}", headerLines[headerIndex].Label, 1);
            WriteCell(writer, $"B{rowNumber}", headerLines[headerIndex].ValueType, headerLines[headerIndex].Value);
            writer.WriteEndElement();
        }

        writer.WriteStartElement("row");
        writer.WriteAttributeString("r", columnHeaderRow.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("ht", "24");
        writer.WriteAttributeString("customHeight", "1");
        for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
        {
            WriteTextCell(writer, $"{ColumnName(columnIndex + 1)}{columnHeaderRow}", columns[columnIndex].Header, 1);
        }
        writer.WriteEndElement();

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            writer.WriteStartElement("row");
            var rowNumber = firstDataRow + rowIndex;
            writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
            for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                var value = columnIndex < rows[rowIndex].Count ? rows[rowIndex][columnIndex] : null;
                WriteCell(writer, $"{ColumnName(columnIndex + 1)}{rowNumber}", columns[columnIndex].Type, value);
            }
            writer.WriteEndElement();
        }

        for (var summaryIndex = 0; summaryIndex < summaryLines.Count; summaryIndex++)
        {
            var rowNumber = summaryStartRow + summaryIndex;
            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
            WriteTextCell(writer, $"A{rowNumber}", summaryLines[summaryIndex].Label, 1);
            WriteCell(writer, $"B{rowNumber}", summaryLines[summaryIndex].ValueType, summaryLines[summaryIndex].Value);
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteStartElement("autoFilter");
        writer.WriteAttributeString("ref", $"A{columnHeaderRow}:{ColumnName(columns.Count)}{Math.Max(columnHeaderRow, lastDataRow)}");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteCell(XmlWriter writer, string reference, ExcelCellType type, object? value)
    {
        if (value is null)
        {
            writer.WriteStartElement("c");
            writer.WriteAttributeString("r", reference);
            writer.WriteEndElement();
            return;
        }

        if (type == ExcelCellType.Text)
        {
            WriteTextCell(writer, reference, Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty, 0);
            return;
        }

        writer.WriteStartElement("c");
        writer.WriteAttributeString("r", reference);
        writer.WriteAttributeString("s", type switch { ExcelCellType.Decimal => "2", ExcelCellType.Date => "3", ExcelCellType.DateTime => "5", _ => "4" });
        writer.WriteStartElement("v");
        var serialized = type is ExcelCellType.Date or ExcelCellType.DateTime
            ? ToDateValue(value).ToString("0.########", CultureInfo.InvariantCulture)
            : Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        writer.WriteString(serialized);
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteTextCell(XmlWriter writer, string reference, string value, int style)
    {
        writer.WriteStartElement("c");
        writer.WriteAttributeString("r", reference);
        writer.WriteAttributeString("t", "inlineStr");
        if (style > 0) writer.WriteAttributeString("s", style.ToString(CultureInfo.InvariantCulture));
        writer.WriteStartElement("is");
        writer.WriteStartElement("t");
        writer.WriteAttributeString("xml", "space", null, "preserve");
        writer.WriteString(value);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static double ToDateValue(object value) => value switch
    {
        DateTimeOffset date => date.DateTime.ToOADate(),
        DateTime date => date.ToOADate(),
        _ => throw new ArgumentException("El valor de fecha no es válido.")
    };

    private static string ColumnName(int number)
    {
        var result = string.Empty;
        while (number > 0)
        {
            number--;
            result = (char)('A' + number % 26) + result;
            number /= 26;
        }
        return result;
    }

    private static string SanitizeSheetName(string value)
    {
        var invalid = new HashSet<char>(['[', ']', ':', '*', '?', '/', '\\']);
        var sanitized = new string(value.Where(x => !invalid.Contains(x)).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(sanitized)) sanitized = "Datos";
        return sanitized.Length > 31 ? sanitized[..31] : sanitized;
    }

    private static string CreateWorkbookXml(string sheetName)
    {
        var escaped = System.Security.SecurityElement.Escape(sheetName);
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"{SpreadsheetNamespace}\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"{escaped}\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>";
    }

    private static void AddTextEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private const string ContentTypesXml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/></Types>";
    private const string RootRelationshipsXml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>";
    private const string WorkbookRelationshipsXml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>";
    private const string StylesXml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><numFmts count=\"3\"><numFmt numFmtId=\"164\" formatCode=\"dd/mm/yyyy\"/><numFmt numFmtId=\"165\" formatCode=\"#,##0.##\"/><numFmt numFmtId=\"166\" formatCode=\"dd/mm/yyyy hh:mm\"/></numFmts><fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><color rgb=\"FFFFFFFF\"/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF12263A\"/><bgColor indexed=\"64\"/></patternFill></fill></fills><borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs><cellXfs count=\"6\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf><xf numFmtId=\"165\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/><xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/><xf numFmtId=\"1\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/><xf numFmtId=\"166\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/></cellXfs><cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>";
}
