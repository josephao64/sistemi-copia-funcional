using System.Globalization;
using System.Text;
using SistemiPos.Application.Reports;

namespace SistemiPos.Infrastructure.Reports;

// Dependency-free PDF with wrapped cells, repeated headers and WinAnsi text.
public sealed class TablePdfWriter : ITablePdfWriter
{
    public byte[] Write(string title, string context, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var pages = new List<StringBuilder>(); var page = new StringBuilder(); double y = 0;
        var width = 760d / Math.Max(1, headers.Count);
        string Escape(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        void Text(string value, double x, double top, int size)
        {
            page.Append(FormattableString.Invariant($"BT /F1 {size} Tf 1 0 0 1 {x:0.##} {top:0.##} Tm ({Escape(value)}) Tj ET\n"));
        }
        void Start()
        {
            page = new(); pages.Add(page); y = 545;
            Text(title, 30, y, 14); y -= 22;
            foreach(var line in Wrap(context,160)) { Text(line,30,y,8);y-=10; }
            y -= 12;
            var headerLines=headers.Select(h=>Wrap(h,Math.Max(6,(int)(width/4.1)))).ToList();
            for(var col=0;col<headerLines.Count;col++)
                for(var line=0;line<headerLines[col].Count;line++) Text(headerLines[col][line],30+col*width,y-line*10,8);
            y -= headerLines.Max(x=>x.Count)*10+8;
        }
        Start();
        foreach (var row in rows)
        {
            var lines = row.Select(v => Wrap(v.Replace("\r", " ").Replace("\n", " "), Math.Max(6, (int)(width / 4.1)))).ToList();
            var height = lines.Max(x => x.Count) * 10 + 8;
            if (y - height < 30) Start();
            for (var col = 0; col < lines.Count; col++)
                for (var line = 0; line < lines[col].Count; line++) Text(lines[col][line], 30 + col * width, y - line * 10, 8);
            y -= height;
        }
        if (rows.Count == 0) Text("Sin registros para estos filtros.", 30, y, 10);
        var objects = new List<byte[]>();
        static byte[] Bytes(string s) => Encoding.Latin1.GetBytes(s);
        objects.Add(Bytes("<< /Type /Catalog /Pages 2 0 R >>"));
        objects.Add(Bytes($"<< /Type /Pages /Count {pages.Count} /Kids [{string.Join(" ", Enumerable.Range(0,pages.Count).Select(i => (4+i*2)+ " 0 R"))}] >>"));
        objects.Add(Bytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"));
        for (var i=0;i<pages.Count;i++)
        {
            objects.Add(Bytes($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 842 595] /Resources << /Font << /F1 3 0 R >> >> /Contents {5+i*2} 0 R >>"));
            var data = Bytes(pages[i].ToString());
            objects.Add(Bytes("<< /Length "+data.Length+" >>\nstream\n").Concat(data).Concat(Bytes("\nendstream")).ToArray());
        }
        using var output = new MemoryStream(); output.Write(Bytes("%PDF-1.4\n"));
        var offsets = new List<long> {0};
        for(var i=0;i<objects.Count;i++) { offsets.Add(output.Position); output.Write(Bytes((i+1)+" 0 obj\n")); output.Write(objects[i]); output.Write(Bytes("\nendobj\n")); }
        var xref = output.Position; output.Write(Bytes($"xref\n0 {offsets.Count}\n0000000000 65535 f \n"));
        foreach(var offset in offsets.Skip(1)) output.Write(Bytes(offset.ToString("D10",CultureInfo.InvariantCulture)+" 00000 n \n"));
        output.Write(Bytes($"trailer << /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF"));
        return output.ToArray();
    }
    private static string Clip(string text, int size) => text.Length <= size ? text : text[..Math.Max(1,size-1)] + ".";
    private static List<string> Wrap(string text, int size)
    {
        // Audit JSON can be long: its full text remains available in Excel and the UI.
        if (text.Length > 700) text = text[..700] + " [ver detalle en Excel]";
        var result = new List<string>();
        while (text.Length > size) { var cut = text.LastIndexOf(' ',size); if(cut<1)cut=size; result.Add(text[..cut]); text=text[cut..].TrimStart(); }
        result.Add(text); return result;
    }
}

