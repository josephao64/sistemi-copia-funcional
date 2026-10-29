using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Catalog;
using SistemiPos.Application.Common.Exceptions;
using SistemiPos.Application.Sales;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Sales;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Sales;

public sealed class SaleReceiptService(AppDbContext dbContext, IProductPricingService? productPricingService = null,
    SistemiPos.Application.Abstractions.IOperationContextAccessor? context = null) : ISaleReceiptService
{
    private readonly IProductPricingService _productPricingService = productPricingService ?? ProductPricingService.Instance;

    public async Task<string> GenerateTicketAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        var sale = await GetSaleAsync(saleId, cancellationToken);
        var settings = await GetSettingsAsync(cancellationToken);
        return BuildTicket(sale, settings);
    }

    public async Task<byte[]> GeneratePdfAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        var sale = await GetSaleAsync(saleId, cancellationToken);
        var settings = await GetSettingsAsync(cancellationToken);
        var ticket = BuildTicket(sale, settings)
            .Split(Environment.NewLine, StringSplitOptions.None)
            .Select(NormalizePdfText)
            .ToList();

        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine("BT");
        contentBuilder.AppendLine("/F1 10 Tf");

        var y = 800;
        foreach (var line in ticket)
        {
            contentBuilder.AppendLine($"1 0 0 1 40 {y} Tm ({EscapePdf(line)}) Tj");
            y -= 14;
        }

        contentBuilder.AppendLine("ET");
        var contentStream = contentBuilder.ToString();
        var objects = new List<string>
        {
            "1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj",
            "2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj",
            "3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >> endobj",
            $"4 0 obj << /Length {Encoding.ASCII.GetByteCount(contentStream)} >> stream\n{contentStream}endstream endobj",
            "5 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj"
        };

        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.WriteLine("%PDF-1.4");
        writer.Flush();

        var offsets = new List<long> { 0 };
        foreach (var obj in objects)
        {
            offsets.Add(stream.Position);
            writer.WriteLine(obj);
            writer.Flush();
        }

        var xrefPosition = stream.Position;
        writer.WriteLine($"xref\n0 {objects.Count + 1}");
        writer.WriteLine("0000000000 65535 f ");
        foreach (var offset in offsets.Skip(1))
        {
            writer.WriteLine($"{offset:D10} 00000 n ");
        }

        writer.WriteLine($"trailer << /Size {objects.Count + 1} /Root 1 0 R >>");
        writer.WriteLine("startxref");
        writer.WriteLine(xrefPosition);
        writer.Write("%%EOF");
        writer.Flush();

        return stream.ToArray();
    }

    public async Task<byte[]> GenerateProformaPdfAsync(ProformaRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0)
        {
            throw new DomainRuleException("Debe agregar al menos un producto para generar la proforma.");
        }

        var settings = await GetSettingsAsync(cancellationToken);
        var requestedProductIds = request.Items.Select(x => x.ProductId).Distinct().ToList();
        var products = await dbContext.Products.AsNoTracking()
            .Include(x => x.PriceRules)
            .Include(x => x.VariantOptions).ThenInclude(x => x.VariantOption).ThenInclude(x => x!.VariantType)
            .Where(x => requestedProductIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var calculatedItems = new List<SaleItem>();
        foreach (var requestItem in request.Items)
        {
            if (!products.TryGetValue(requestItem.ProductId, out var product))
            {
                throw new NotFoundException("Producto no encontrado.");
            }
            if (!product.IsActive)
            {
                throw new DomainRuleException("No se puede generar una proforma con un producto inactivo.");
            }
            if (requestItem.Quantity <= 0)
            {
                throw new DomainRuleException("La cantidad vendida debe ser mayor que cero.");
            }
            if (!Enum.IsDefined(requestItem.DiscountType) || requestItem.DiscountValue < 0)
            {
                throw new DomainRuleException("El descuento no es válido.");
            }

            var quote = _productPricingService.Calculate(product, requestItem.Quantity);
            var discountType = requestItem.DiscountValue == 0m ? SaleDiscountType.None : requestItem.DiscountType;
            if (discountType == SaleDiscountType.None && requestItem.DiscountValue != 0m)
            {
                throw new DomainRuleException("El tipo de descuento no es válido.");
            }
            if (discountType == SaleDiscountType.Percentage && requestItem.DiscountValue > 100m)
            {
                throw new DomainRuleException("El descuento no puede superar el total de la línea.");
            }

            var fixedDiscount = discountType == SaleDiscountType.FixedAmount ? requestItem.DiscountValue : (decimal?)null;
            calculatedItems.Add(new SaleItem(
                product.Id,
                product.DisplayName,
                requestItem.Quantity,
                quote.UnitPrice,
                discountType == SaleDiscountType.Percentage ? requestItem.DiscountValue : 0m,
                settings.TaxPercent,
                !product.AppliesTax,
                product.LastCost,
                quote.GrossAmount,
                fixedDiscount));
        }

        var customerName = request.CustomerName?.Trim();
        if (string.IsNullOrWhiteSpace(customerName) && request.CustomerId is Guid customerId)
        {
            customerName = await dbContext.Customers.AsNoTracking()
                .Where(x => x.Id == customerId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var proforma = new ProformaDocument(
            calculatedItems,
            customerName,
            request.CustomerNit?.Trim(),
            request.CustomerAddress?.Trim(),
            DateTimeOffset.Now);
        return BuildProformaPdf(proforma, settings, TryLoadLogo(settings.BusinessLogoUrl));
    }

    private async Task<Sale> GetSaleAsync(Guid saleId, CancellationToken cancellationToken)
    {
        return await dbContext.Sales
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .Include(x => x.AccountParts)
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == saleId && (context == null || !context.CurrentBranchId.HasValue
                || dbContext.CashSessions.Any(s => s.Id == x.CashSessionId && s.BranchId == context.CurrentBranchId)), cancellationToken)
            ?? throw new NotFoundException("Venta no encontrada.");
    }

    private async Task<SystemSetting> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await dbContext.SystemSettings.FirstOrDefaultAsync(cancellationToken);
        return settings ?? SystemSetting.CreateDefault();
    }

    private static string BuildTicket(Sale sale, SystemSetting settings)
    {
        var sb = new StringBuilder();
        sb.AppendLine(settings.BusinessName);
        if (!string.IsNullOrWhiteSpace(settings.BusinessNit))
        {
            sb.AppendLine($"NIT: {settings.BusinessNit}");
        }

        if (!string.IsNullOrWhiteSpace(settings.BusinessAddress))
        {
            sb.AppendLine(settings.BusinessAddress);
        }

        if (!string.IsNullOrWhiteSpace(settings.BusinessPhone))
        {
            sb.AppendLine($"Tel: {settings.BusinessPhone}");
        }

        sb.AppendLine($"Venta: {sale.Number}");
        sb.AppendLine($"Fecha: {sale.SoldAtUtc:dd/MM/yyyy HH:mm}");
        sb.AppendLine($"Cajero: {sale.CashierUserName}");
        sb.AppendLine($"Cliente: {(sale.CustomerId.HasValue ? sale.Customer?.Name ?? "Cliente" : "Consumidor final")}");
        sb.AppendLine(new string('-', 40));

        foreach (var item in sale.Items)
        {
            sb.AppendLine(item.ProductName);
            sb.AppendLine($"{item.Quantity:N2} x {FormatMoney(item.UnitPrice, sale)} = {FormatMoney(item.Subtotal, sale)}");
            if (item.DiscountAmount > 0)
            {
                sb.AppendLine($"Desc. {item.DiscountPercent:N2}%: {FormatMoney(item.DiscountAmount, sale)}");
            }

            if (settings.ShowTaxOnReceipt && item.TaxAmount > 0)
            {
                sb.AppendLine($"Imp. {item.TaxPercent:N2}%: {FormatMoney(item.TaxAmount, sale)}");
            }
        }

        sb.AppendLine(new string('-', 40));
        sb.AppendLine($"Subtotal: {FormatMoney(sale.Subtotal, sale)}");
        sb.AppendLine($"Descuento: {FormatMoney(sale.DiscountTotal, sale)}");
        if (settings.ShowTaxOnReceipt)
        {
            sb.AppendLine($"Base imponible: {FormatMoney(sale.TaxableBaseTotal, sale)}");
            sb.AppendLine($"Impuesto: {FormatMoney(sale.TaxTotal, sale)}");
        }
        sb.AppendLine($"Total: {FormatMoney(sale.Total, sale)}");
        sb.AppendLine($"Pago: {(sale.Payments.Count > 1 ? "Pagos mixtos" : sale.Payment.DefinitionName ?? sale.Payment.PaymentMethod.ToString())}");
        foreach (var payment in sale.Payments)
            sb.AppendLine($"{payment.DefinitionName ?? payment.PaymentMethod.ToString()}: {FormatMoney(payment.TotalAmount, sale)}" +
                (payment.ChangeAmount > 0 ? $"; cambio: {FormatMoney(payment.ChangeAmount.Value, sale)}" : string.Empty));

        if (sale.Payment.PaymentMethod == PaymentMethodType.Cash)
        {
            sb.AppendLine($"Recibido: {FormatMoney(sale.Payment.ReceivedAmount ?? 0m, sale)}");
            sb.AppendLine($"Cambio: {FormatMoney(sale.Payment.ChangeAmount ?? 0m, sale)}");
        }
        else if (!string.IsNullOrWhiteSpace(sale.Payment.Reference))
        {
            sb.AppendLine($"Referencia: {sale.Payment.Reference}");
        }

        if (!string.IsNullOrWhiteSpace(settings.ReceiptFooter))
        {
            sb.AppendLine(settings.ReceiptFooter);
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatMoney(decimal amount, Sale sale)
    {
        var formatted = amount.ToString($"N{sale.CurrencyDecimals}", CultureInfo.InvariantCulture)
            .Replace(",", "_tmp_", StringComparison.Ordinal)
            .Replace(".", sale.DecimalSeparator, StringComparison.Ordinal)
            .Replace("_tmp_", sale.ThousandsSeparator, StringComparison.Ordinal);

        return sale.CurrencySymbolPosition == "After"
            ? $"{formatted} {sale.CurrencySymbol}"
            : $"{sale.CurrencySymbol} {formatted}";
    }

    private static string NormalizePdfText(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var buffer = new StringBuilder();
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark && character <= sbyte.MaxValue)
            {
                buffer.Append(character);
            }
        }

        return buffer.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string EscapePdf(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);

    private sealed record ProformaDocument(
        IReadOnlyList<SaleItem> Items,
        string? CustomerName,
        string? CustomerNit,
        string? CustomerAddress,
        DateTimeOffset GeneratedAt)
    {
        public decimal Subtotal => decimal.Round(Items.Sum(x => x.GrossAmount), 2);
        public decimal DiscountTotal => decimal.Round(Items.Sum(x => x.DiscountAmount), 2);
        public decimal Total => decimal.Round(Items.Sum(x => x.Subtotal), 2);
    }

    private static byte[] BuildProformaPdf(ProformaDocument proforma, SystemSetting settings, byte[]? logoBytes)
    {
        const int itemsPerPage = 24;
        var pages = proforma.Items.Chunk(itemsPerPage).ToList();
        if (pages.Count == 0) pages.Add([]);

        var pageCount = pages.Count;
        var regularFontObject = 3 + pageCount * 2;
        var boldFontObject = regularFontObject + 1;
        var logo = TryParseJpeg(logoBytes);
        var logoObject = boldFontObject + 1;
        var pageReferences = string.Join(' ', Enumerable.Range(0, pageCount).Select(i => $"{3 + i * 2} 0 R"));
        var objects = new Dictionary<int, string>
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R >>",
            [2] = $"<< /Type /Pages /Kids [{pageReferences}] /Count {pageCount} >>",
            [regularFontObject] = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            [boldFontObject] = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>"
        };

        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            var pageObject = 3 + pageIndex * 2;
            var contentObject = pageObject + 1;
            var content = BuildProformaPage(proforma, settings, pages[pageIndex], pageIndex + 1, pageCount, logo is not null);
            var xObject = logo is null ? string.Empty : $" /XObject << /Im1 {logoObject} 0 R >>";
            objects[pageObject] = $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents {contentObject} 0 R /Resources << /Font << /F1 {regularFontObject} 0 R /F2 {boldFontObject} 0 R >>{xObject} >> >>";
            objects[contentObject] = $"<< /Length {Encoding.ASCII.GetByteCount(content)} >> stream\n{content}endstream";
        }

        if (logo is not null)
        {
            var hexadecimal = Convert.ToHexString(logo.Value.Bytes) + ">";
            objects[logoObject] = $"<< /Type /XObject /Subtype /Image /Width {logo.Value.Width} /Height {logo.Value.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter [/ASCIIHexDecode /DCTDecode] /Length {hexadecimal.Length} >> stream\n{hexadecimal}\nendstream";
        }

        return SerializePdf(objects);
    }

    private static string BuildProformaPage(
        ProformaDocument proforma,
        SystemSetting settings,
        SaleItem[] items,
        int page,
        int pages,
        bool hasLogo)
    {
        var content = new StringBuilder("BT\n");
        if (hasLogo) content.AppendLine("ET q 70 0 0 55 40 760 cm /Im1 Do Q BT");

        var companyX = hasLogo ? 125 : 40;
        var businessName = string.IsNullOrWhiteSpace(settings.BusinessLegalName) ? settings.BusinessName : settings.BusinessLegalName;
        PdfText(content, businessName, companyX, 806, 15, true);
        if (!string.Equals(businessName, settings.BusinessName, StringComparison.OrdinalIgnoreCase)) PdfText(content, settings.BusinessName, companyX, 790, 9);
        PdfText(content, $"NIT: {settings.BusinessNit ?? "No registrado"}", companyX, 775, 8);
        if (!string.IsNullOrWhiteSpace(settings.BusinessAddress)) PdfText(content, ClipPdfText(settings.BusinessAddress, 57), companyX, 762, 8);
        if (!string.IsNullOrWhiteSpace(settings.BusinessPhone)) PdfText(content, $"Tel.: {settings.BusinessPhone}", companyX, 749, 8);
        if (!string.IsNullOrWhiteSpace(settings.BusinessEmail)) PdfText(content, ClipPdfText(settings.BusinessEmail, 45), companyX, 736, 8);

        PdfText(content, "PROFORMA", 405, 806, 16, true);
        PdfText(content, "DOCUMENTO NO FISCAL", 386, 787, 9, true);
        PdfText(content, $"Fecha: {proforma.GeneratedAt:dd/MM/yyyy HH:mm}", 396, 772, 8);
        PdfText(content, $"Página {page} de {pages}", 448, 758, 7);
        PdfLine(content, 40, 722, 555, 722);

        var customerY = 705;
        if (!string.IsNullOrWhiteSpace(proforma.CustomerName))
        {
            PdfText(content, "Cliente", 40, customerY, 8, true);
            PdfText(content, ClipPdfText(proforma.CustomerName, 62), 40, customerY - 14, 10, true);
            if (!string.IsNullOrWhiteSpace(proforma.CustomerNit)) PdfText(content, $"NIT: {ClipPdfText(proforma.CustomerNit, 32)}", 335, customerY - 14, 8);
            if (!string.IsNullOrWhiteSpace(proforma.CustomerAddress)) PdfText(content, ClipPdfText(proforma.CustomerAddress, 62), 40, customerY - 27, 8);
            customerY -= 46;
        }

        PdfLine(content, 40, customerY, 555, customerY);
        var headerY = customerY - 15;
        PdfText(content, "Producto", 40, headerY, 8, true);
        PdfText(content, "Cant.", 245, headerY, 8, true);
        PdfText(content, "P. unitario", 292, headerY, 8, true);
        PdfText(content, "Descuento", 384, headerY, 8, true);
        PdfText(content, "Subtotal", 477, headerY, 8, true);
        PdfLine(content, 40, headerY - 9, 555, headerY - 9);

        var y = headerY - 25;
        foreach (var item in items)
        {
            PdfText(content, ClipPdfText(item.ProductName, 34), 40, y, 8);
            PdfText(content, item.Quantity.ToString("0.##", CultureInfo.InvariantCulture), 245, y, 8);
            PdfText(content, FormatMoney(item.UnitPrice, settings), 292, y, 8);
            PdfText(content, item.DiscountAmount == 0m ? "-" : $"-{FormatMoney(item.DiscountAmount, settings)}", 384, y, 8);
            PdfText(content, FormatMoney(item.Subtotal, settings), 477, y, 8);
            y -= 19;
        }
        PdfLine(content, 40, y + 7, 555, y + 7);

        if (page == pages)
        {
            var totalY = y - 17;
            PdfText(content, "Subtotal", 390, totalY, 9);
            PdfText(content, FormatMoney(proforma.Subtotal, settings), 477, totalY, 9);
            if (proforma.DiscountTotal > 0m)
            {
                totalY -= 16;
                PdfText(content, "Descuentos", 390, totalY, 9);
                PdfText(content, $"-{FormatMoney(proforma.DiscountTotal, settings)}", 477, totalY, 9);
            }
            totalY -= 20;
            PdfLine(content, 385, totalY + 8, 555, totalY + 8);
            PdfText(content, $"TOTAL ({settings.CurrencyCode})", 390, totalY - 7, 11, true);
            PdfText(content, FormatMoney(proforma.Total, settings), 477, totalY - 7, 11, true);
            PdfText(content, "Esta proforma no constituye factura ni registra una venta.", 40, 55, 8);
            PdfText(content, "DOCUMENTO NO FISCAL", 225, 40, 9, true);
        }

        content.Append("ET\n");
        return content.ToString();
    }

    private static string FormatMoney(decimal amount, SystemSetting settings)
    {
        var formatted = amount.ToString($"N{settings.CurrencyDecimals}", CultureInfo.InvariantCulture)
            .Replace(",", "_tmp_", StringComparison.Ordinal)
            .Replace(".", settings.DecimalSeparator, StringComparison.Ordinal)
            .Replace("_tmp_", settings.ThousandsSeparator, StringComparison.Ordinal);
        return settings.CurrencySymbolPosition == CurrencySymbolPosition.After
            ? $"{formatted} {settings.CurrencySymbol}"
            : $"{settings.CurrencySymbol} {formatted}";
    }

    private static void PdfText(StringBuilder content, string value, int x, int y, int size, bool bold = false) =>
        content.AppendLine($"/{(bold ? "F2" : "F1")} {size} Tf 1 0 0 1 {x} {y} Tm ({EscapePdf(NormalizePdfText(value))}) Tj");

    private static void PdfLine(StringBuilder content, int x1, int y1, int x2, int y2) =>
        content.AppendLine($"ET {x1} {y1} m {x2} {y2} l S BT");

    private static string ClipPdfText(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..(maximumLength - 1)] + "…";

    private static byte[] SerializePdf(Dictionary<int, string> objects)
    {
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.WriteLine("%PDF-1.4");
        writer.Flush();

        var offsets = new List<long> { 0 };
        foreach (var pair in objects.OrderBy(x => x.Key))
        {
            offsets.Add(stream.Position);
            writer.WriteLine($"{pair.Key} 0 obj {pair.Value} endobj");
            writer.Flush();
        }

        var xrefPosition = stream.Position;
        writer.WriteLine($"xref\n0 {objects.Count + 1}");
        writer.WriteLine("0000000000 65535 f ");
        foreach (var offset in offsets.Skip(1)) writer.WriteLine($"{offset:D10} 00000 n ");
        writer.WriteLine($"trailer << /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefPosition}\n%%EOF");
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[]? TryLoadLogo(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath) || configuredPath.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return null;

        var candidates = Path.IsPathRooted(configuredPath)
            ? new[] { configuredPath }
            : new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", configuredPath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar)),
                Path.Combine(Directory.GetCurrentDirectory(), "src", "SistemiPos.Web", "wwwroot", configuredPath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar)),
                Path.Combine(AppContext.BaseDirectory, "wwwroot", configuredPath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar))
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
