using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.UnitTests.Catalog;

public sealed class ArticleTypeInvariantsTests
{
    [Fact]
    public void ArticleTypes_Enum_Contains_All_Required_Types()
    {
        Assert.Equal(1, (int)ArticleType.Insumo);
        Assert.Equal(2, (int)ArticleType.Preparado);
        Assert.Equal(3, (int)ArticleType.Reventa);
        Assert.Equal(4, (int)ArticleType.Subreceta);
        Assert.Equal(5, (int)ArticleType.Empaque);
    }

    [Fact]
    public void Product_Can_Set_And_Change_ArticleType()
    {
        var product = new Product("P-001", "123456", "Masa Madre", null, Guid.NewGuid(), "Panadería", "unidad", 5m, 4m, 12m, 2m, true);
        
        // Default when not specified is Reventa
        Assert.Equal(ArticleType.Reventa, product.ArticleType);

        product.SetArticleType(ArticleType.Subreceta);
        Assert.Equal(ArticleType.Subreceta, product.ArticleType);

        product.SetArticleType(ArticleType.Insumo);
        Assert.Equal(ArticleType.Insumo, product.ArticleType);
    }

    [Fact]
    public void Product_Can_Assign_BaseUnit()
    {
        var product = new Product("P-001", "123456", "Harina de Trigo", null, Guid.NewGuid(), "Materia Prima", "kg", 8m, 7m, 12m, 10m, true);
        var baseUnitId = Guid.NewGuid();

        product.SetBaseUnit(baseUnitId);

        Assert.Equal(baseUnitId, product.BaseUnitId);
    }

    [Fact]
    public void CatalogChangeLog_Records_Audited_Field_Modifications()
    {
        var productId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var log = new CatalogChangeLog(
            productId,
            "P-001",
            "SalePrice",
            "10.00",
            "12.50",
            "admin@sistemi.gt",
            now,
            branchId,
            "Ajuste por inflación"
        );

        Assert.Equal(productId, log.ProductId);
        Assert.Equal("P-001", log.ProductInternalCode);
        Assert.Equal(branchId, log.BranchId);
        Assert.Equal("SalePrice", log.Field);
        Assert.Equal("10.00", log.OldValue);
        Assert.Equal("12.50", log.NewValue);
        Assert.Equal("admin@sistemi.gt", log.Actor);
        Assert.Equal("Ajuste por inflación", log.Reason);
        Assert.True(log.TimestampUtc <= DateTime.UtcNow);
    }
}
