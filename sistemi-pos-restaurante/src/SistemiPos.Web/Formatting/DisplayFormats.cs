using System.Globalization;

namespace SistemiPos.Web.Formatting;

public static class DisplayFormats
{
    public static string Quantity(decimal value) =>
        value.ToString("#,##0.######", CultureInfo.CurrentCulture);
}
