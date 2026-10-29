using System.IO.Compression;
using System.Xml.Linq;
using SistemiPos.Application.Exports;
using SistemiPos.Infrastructure.Exports;

namespace SistemiPos.IntegrationTests.Infrastructure;

public sealed class ExcelExportServiceTests
{
    [Fact]
    public void CreateWorkbook_Generates_Valid_OpenXml_With_Typed_Values()
    {
        var service = new ExcelExportService();

        var content = service.CreateWorkbook(
            "Existencia baja",
            [
                new ExcelColumn("Producto"),
                new ExcelColumn("Existencia", ExcelCellType.Decimal),
                new ExcelColumn("Fecha", ExcelCellType.Date)
            ],
            [
                (IReadOnlyList<object?>)["Lápices", 12.5m, new DateTime(2026, 8, 31)]
            ]);

        using var stream = new MemoryStream(content);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        Assert.NotNull(archive.GetEntry("[Content_Types].xml"));
        Assert.NotNull(archive.GetEntry("xl/workbook.xml"));
        Assert.NotNull(archive.GetEntry("xl/styles.xml"));
        var sheet = archive.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(sheet);

        using var sheetStream = sheet!.Open();
        var document = XDocument.Load(sheetStream);
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        Assert.Equal(2, document.Descendants(spreadsheet + "row").Count());
        Assert.Contains(document.Descendants(spreadsheet + "t"), x => x.Value == "Lápices");
        Assert.Contains(document.Descendants(spreadsheet + "v"), x => x.Value == "12.5");
        using var stylesStream = archive.GetEntry("xl/styles.xml")!.Open();
        var styles = XDocument.Load(stylesStream);
        Assert.Contains(styles.Descendants(spreadsheet + "numFmt"), x => (string?)x.Attribute("formatCode") == "#,##0.##");
    }
}
