namespace SistemiPos.Domain.Customers;

public static class NitNormalizer
{
    public static string? Normalize(string? nit)
    {
        if (string.IsNullOrWhiteSpace(nit))
        {
            return null;
        }

        var normalized = new string(nit
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
