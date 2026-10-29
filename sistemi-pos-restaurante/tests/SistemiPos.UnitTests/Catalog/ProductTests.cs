using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.UnitTests.Catalog;

public sealed class ProductTests
{
    [Fact]
    public void Expiry_Requires_Lots_And_Tracking_Changes_Are_Atomic()
    {
        var product = new Product("P-LOT", null, "Producto", null, Guid.NewGuid(), "Marca", "Unidad", 0, 0, 1, 0, false);
        product.ConfigureLotTracking(false);
        Assert.Throws<DomainRuleException>(() => product.ConfigureExpiry(true, 10));
        Assert.False(product.ManagesLots);
        Assert.False(product.ManagesExpiry);
        product.ConfigureInventoryTracking(true, true, 10);
        Assert.Throws<DomainRuleException>(() => product.ConfigureLotTracking(false));
        Assert.True(product.ManagesLots);
        Assert.True(product.ManagesExpiry);
    }
    [Fact]
    public void Constructor_Allows_Optional_Barcode()
    {
        var product = new Product("P-001", null, "Café", null, Guid.NewGuid(), "Marca", "Unidad", 10m, 9m, 15m, 2m, true);

        Assert.Null(product.Barcode);
    }

    [Fact]
    public void Supplier_Invoice_Name_Is_Stored_Separately_From_The_System_Name()
    {
        var product = new Product("P-001", null, "Lápiz Mongol No. 2", "Lápiz amarillo", Guid.NewGuid(),
            "Marca", "Unidad", 1m, 1m, 2m, 0m, true);

        product.SetSupplierInvoiceName("  LAPIZ GRAFITO MONGOL 482 N2 AMARILLO  ");

        Assert.Equal("Lápiz Mongol No. 2", product.Name);
        Assert.Equal("Lapiz Grafito Mongol 482 N2 Amarillo", product.SupplierInvoiceName);
        Assert.Equal("Lápiz amarillo", product.Description);
    }

    [Fact]
    public void Update_Rejects_Negative_Values()
    {
        Assert.Throws<DomainRuleException>(() =>
            new Product("P-001", "123", "Café", null, Guid.NewGuid(), "Marca", "Unidad", -1m, 0m, 10m, 1m, true));
    }

    [Fact]
    public void Variant_DisplayName_Concatenates_Base_Name_And_Value()
    {
        var product = new Product("P-001", "123", "Café molido", null, Guid.NewGuid(), "Marca", "Unidad", 10m, 9m, 15m, 2m, true);

        product.SetVariant(Guid.NewGuid(), "100 g");

        Assert.Equal("Café Molido 100 g", product.DisplayName);
    }

    [Fact]
    public void Variant_Requires_Parent_And_Value_Together()
    {
        var product = new Product("P-001", "123", "Café", null, Guid.NewGuid(), "Marca", "Unidad", 10m, 9m, 15m, 2m, true);

        Assert.Throws<DomainRuleException>(() => product.SetVariant(null, "100 g"));
        Assert.Throws<DomainRuleException>(() => product.SetVariant(Guid.NewGuid(), " "));
    }
}
