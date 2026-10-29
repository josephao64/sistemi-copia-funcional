using System.Globalization;

namespace SistemiPos.Domain.Common;

public static class NameNormalizer
{
    private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-GT");

    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var compact = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return SpanishCulture.TextInfo.ToTitleCase(compact.ToLower(SpanishCulture));
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Normalize(value);

    public static bool Equals(string? left, string? right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
}
