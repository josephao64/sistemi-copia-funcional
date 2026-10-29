using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Common.Exceptions;
using SistemiPos.Application.Purchases;
using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Purchases;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Purchases;

public sealed class PurchaseOrderPdfService(AppDbContext dbContext) : IPurchaseOrderPdfService
{
    public async Task<byte[]> GenerateAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.PurchaseOrders.AsNoTracking()
            .Include(x => x.Supplier).Include(x => x.Items).ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken)
            ?? throw new NotFoundException("Orden de compra no encontrada.");
        if (order.Status is not (PurchaseOrderStatus.Requested or PurchaseOrderStatus.PartiallyReceived))
            throw new DomainRuleException("El PDF formal solo está disponible para órdenes solicitadas.");
        var settings = await dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? SystemSetting.CreateDefault();
        return BuildPdf(order, settings, TryLoadLogo(settings.BusinessLogoUrl));
    }

    internal static byte[] BuildPdf(PurchaseOrder order, SystemSetting settings, byte[]? logoBytes = null)
    {
        const int perPage = 30;
        var chunks = order.Items.Chunk(perPage).ToList();
        if (chunks.Count == 0) chunks.Add([]);
        var pageCount = chunks.Count;
        var fontRegular = 3 + pageCount * 2;
        var fontBold = fontRegular + 1;
        var logo = TryParseJpeg(logoBytes);
        var imageObject = fontBold + 1;
        var objects = new Dictionary<int, string>();
        objects[1] = "<< /Type /Catalog /Pages 2 0 R >>";
        var kids = string.Join(' ', Enumerable.Range(0, pageCount).Select(i => $"{3 + i * 2} 0 R"));
        objects[2] = $"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>";

        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            var pageObject = 3 + pageIndex * 2;
            var contentObject = pageObject + 1;
            var content = BuildPage(order, settings, chunks[pageIndex], pageIndex + 1, pageCount, logo is not null);
            var xObject = logo is null ? "" : $" /XObject << /Im1 {imageObject} 0 R >>";
            objects[pageObject] = $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents {contentObject} 0 R /Resources << /Font << /F1 {fontRegular} 0 R /F2 {fontBold} 0 R >>{xObject} >> >>";
            objects[contentObject] = $"<< /Length {Encoding.ASCII.GetByteCount(content)} >> stream\n{content}endstream";
        }
        objects[fontRegular] = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>";
        objects[fontBold] = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>";
        if (logo is not null)
        {
            var hexadecimal = Convert.ToHexString(logo.Value.Bytes) + ">";
            objects[imageObject] = $"<< /Type /XObject /Subtype /Image /Width {logo.Value.Width} /Height {logo.Value.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter [/ASCIIHexDecode /DCTDecode] /Length {hexadecimal.Length} >> stream\n{hexadecimal}\nendstream";
        }
        return Serialize(objects);
    }

    private static string BuildPage(PurchaseOrder order, SystemSetting settings, PurchaseOrderItem[] items, int page, int pages, bool hasLogo)
    {
        var b = new StringBuilder("BT\n");
        if (hasLogo) b.AppendLine("ET q 70 0 0 55 40 760 cm /Im1 Do Q BT");
        var companyX = hasLogo ? 125 : 40;
        Text(b, settings.BusinessName, companyX, 806, 15, true);
        Text(b, $"NIT: {settings.BusinessNit ?? "No registrado"}", companyX, 789, 8);
        Text(b, $"Teléfono de contacto: {settings.BusinessPhone ?? "No registrado"}", companyX, 775, 8);
        Text(b, "ORDEN DE COMPRA", 385, 806, 13, true);
        Text(b, $"No. {order.Number}", 385, 788, 9, true);
        Text(b, $"Emisión: {(order.SubmittedAtUtc ?? order.RequestedAtUtc):dd/MM/yyyy}", 385, 774, 8);
        Line(b, 40, 760, 555, 760);
        Text(b, "Proveedor", 40, 744, 8, true);
        Text(b, Clip(order.Supplier.LegalName, 75), 40, 729, 10, true);
        Text(b, $"Página {page} de {pages}", 490, 729, 7);
        Line(b, 40, 716, 555, 716);
        Text(b, "Cantidad", 40, 700, 8, true); Text(b, "Producto", 92, 700, 8, true);
        Text(b, "Presentación", 250, 700, 8, true); Text(b, "Unidades", 335, 700, 8, true);
        Text(b, "Producto según proveedor", 390, 700, 8, true);
        Line(b, 40, 691, 555, 691);
        var y = 676;
        foreach (var item in items)
        {
            Text(b, item.PackageQuantity.ToString("0.##", CultureInfo.InvariantCulture), 40, y, 8);
            Text(b, Clip(item.Product.DisplayName, 34), 92, y, 8);
            Text(b, Clip($"{item.PresentationName} ({item.UnitsPerPackage})", 20), 250, y, 8);
            Text(b, item.RequestedQuantity.ToString("0.##", CultureInfo.InvariantCulture), 335, y, 8);
            Text(b, Clip(item.Product.SupplierInvoiceName ?? string.Empty, 36), 390, y, 8);
            y -= 19;
        }
        Line(b, 40, y + 7, 555, y + 7);
        b.Append("ET\n");
        return b.ToString();
    }

    private static void Text(StringBuilder b, string value, int x, int y, int size, bool bold = false) =>
        b.AppendLine($"/{(bold ? "F2" : "F1")} {size} Tf 1 0 0 1 {x} {y} Tm ({Escape(Normalize(value))}) Tj");
    private static void Line(StringBuilder b, int x1, int y1, int x2, int y2) => b.AppendLine($"ET {x1} {y1} m {x2} {y2} l S BT");
    private static string Clip(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
    private static IEnumerable<string> Wrap(string value, int max) { for (var i = 0; i < value.Length; i += max) yield return value.Substring(i, Math.Min(max, value.Length - i)); }
    private static string Normalize(string value) { var n=value.Normalize(NormalizationForm.FormD); var b=new StringBuilder(); foreach(var c in n) if(CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark&&c<=sbyte.MaxValue)b.Append(c); return b.ToString(); }
    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);
    private static byte[] Serialize(Dictionary<int,string> objects)
    {
        using var stream=new MemoryStream(); using var writer=new StreamWriter(stream,Encoding.ASCII,leaveOpen:true); writer.WriteLine("%PDF-1.4");writer.Flush();var offsets=new List<long>{0};
        foreach(var pair in objects.OrderBy(x=>x.Key)){offsets.Add(stream.Position);writer.WriteLine($"{pair.Key} 0 obj {pair.Value} endobj");writer.Flush();}
        var xref=stream.Position;writer.WriteLine($"xref\n0 {objects.Count+1}");writer.WriteLine("0000000000 65535 f ");foreach(var offset in offsets.Skip(1))writer.WriteLine($"{offset:D10} 00000 n ");writer.WriteLine($"trailer << /Size {objects.Count+1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");writer.Flush();return stream.ToArray();
    }

    private static byte[]? TryLoadLogo(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath) || configuredPath.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return null;
        var relative = configuredPath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relative),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "SistemiPos.Web", "wwwroot", relative),
            Path.Combine(AppContext.BaseDirectory, "wwwroot", relative)
        };
        var path = candidates.FirstOrDefault(File.Exists);
        if (path is null || !string.Equals(Path.GetExtension(path), ".jpg", StringComparison.OrdinalIgnoreCase) && !string.Equals(Path.GetExtension(path), ".jpeg", StringComparison.OrdinalIgnoreCase)) return null;
        try { return File.ReadAllBytes(path); } catch { return null; }
    }

    private static (byte[] Bytes, int Width, int Height)? TryParseJpeg(byte[]? bytes)
    {
        if (bytes is null || bytes.Length < 12 || bytes[0] != 0xFF || bytes[1] != 0xD8) return null;
        for (var i = 2; i + 9 < bytes.Length; i++)
        {
            if (bytes[i] != 0xFF || bytes[i + 1] is not (0xC0 or 0xC1 or 0xC2)) continue;
            var height = bytes[i + 5] * 256 + bytes[i + 6];
            var width = bytes[i + 7] * 256 + bytes[i + 8];
            return width > 0 && height > 0 ? (bytes, width, height) : null;
        }
        return null;
    }
}
