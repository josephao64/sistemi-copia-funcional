using System.Globalization;
using SistemiPos.Web.Formatting;

namespace SistemiPos.IntegrationTests.Infrastructure;

public sealed class DisplayFormatsTests
{
    [Fact]
    public void Quantity_Hides_Unnecessary_Decimals_And_Preserves_Fractions()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

            Assert.Equal("45", DisplayFormats.Quantity(45m));
            Assert.Equal("1.5", DisplayFormats.Quantity(1.50m));
            Assert.Equal("1.25", DisplayFormats.Quantity(1.25m));
            Assert.Equal("0.000001", DisplayFormats.Quantity(0.000001m));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }
}
