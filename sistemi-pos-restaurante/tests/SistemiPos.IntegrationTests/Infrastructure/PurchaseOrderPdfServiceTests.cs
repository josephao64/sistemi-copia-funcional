using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Purchases;
using SistemiPos.Domain.Suppliers;
using SistemiPos.Infrastructure.Purchases;

namespace SistemiPos.IntegrationTests.Infrastructure;

public sealed class PurchaseOrderPdfServiceTests
{
    [Fact]
    public void BuildPdf_Creates_A_Valid_Formal_Purchase_Order()
    {
        var supplier = new Supplier("Distribuidora Central, S.A.", "1234567-8", "Distribuidora Central",
            "5555-0101", "compras@proveedor.test", "Zona 1, Guatemala", "Ana Pérez");
        var product = new Product("PROD-001", null, "Café molido premium 1 libra", null, Guid.NewGuid(),
            "Marca", "Unidad", 28m, 25m, 40m, 2m, true);
        var item = new PurchaseOrderItem(product.Id, 2m, Guid.NewGuid(), "Caja", 24) { Product = product };
        var order = new PurchaseOrder("PC-000123", supplier.Id,
            new DateTimeOffset(2026, 9, 3, 15, 0, 0, TimeSpan.Zero), "tester", [item],
            "Entregar en bodega principal.") { Supplier = supplier };
        order.Submit(new DateTimeOffset(2026, 9, 3, 15, 5, 0, TimeSpan.Zero), "tester");

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "src", "SistemiPos.Web"))) root = root.Parent;
        var logoPath = root is null ? string.Empty : Path.Combine(root.FullName, "src", "SistemiPos.Web", "wwwroot", "images", "business", "miscelanea-nc-logo.jpg");
        var pdf = PurchaseOrderPdfService.BuildPdf(order, SystemSetting.CreateDefault(), File.Exists(logoPath) ? File.ReadAllBytes(logoPath) : null);

        Assert.True(pdf.Length > 50_000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        var output = Environment.GetEnvironmentVariable("PDF_SAMPLE_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllBytes(output, pdf);
        }
    }
}
