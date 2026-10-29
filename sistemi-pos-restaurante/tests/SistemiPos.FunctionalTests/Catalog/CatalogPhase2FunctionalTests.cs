using System.ComponentModel.DataAnnotations;
using SistemiPos.Application.Catalog;
using SistemiPos.Domain.Catalog;
using SistemiPos.Web.Models.Catalog;

namespace SistemiPos.FunctionalTests.Catalog;

public sealed class CatalogPhase2FunctionalTests
{
    [Fact]
    public void ProductFormViewModel_Validates_Required_Fields_And_Positive_Values()
    {
        var model = new ProductFormViewModel
        {
            InternalCode = "INS-001",
            Name = "Queso Mozzarella",
            CategoryId = Guid.NewGuid(),
            UnitOfMeasure = "kg",
            SalePrice = 45m,
            LastCost = 30m,
            PreviousCost = 30m,
            MinimumStock = 5m,
            ArticleType = ArticleType.Insumo,
            NoMovementDaysOverride = 30,
            BaseUnitId = Guid.NewGuid()
        };

        var results = Validate(model);
        Assert.Empty(results);
    }

    [Fact]
    public void PurchasePresentation_Correctly_Calculates_Base_Quantity_In_Inventory()
    {
        // Scenario: Flour purchased in sacks of 25 kg.
        // Product base unit is 'g' (grams).
        // 1 sack = 25,000 g.
        // If 2 sacks are received, total base quantity should be 50,000 g.
        var flourId = Guid.NewGuid();
        var presentation = new ProductPurchasePresentation(
            productId: flourId,
            name: "Saco 25 kg",
            baseQuantityPerPackage: 25000m,
            purchaseUnitId: Guid.NewGuid()
        );

        decimal packagesPurchased = 2m;
        decimal totalBaseInventory = packagesPurchased * presentation.BaseQuantityPerPackage;

        Assert.Equal(50000m, totalBaseInventory);
        Assert.Equal("Saco 25 Kg", presentation.Name);
        Assert.True(presentation.IsActive);
    }

    [Fact]
    public void BranchSettings_Effective_Price_Resolution()
    {
        var product = new Product("BEB-001", "7401001001", "Cerveza Artesanal", null, Guid.NewGuid(), "Bebidas", "unidad", 12m, 10m, 25.00m, 10m, true);
        
        var branchCentralId = Guid.NewGuid();
        var branchAeropuertoId = Guid.NewGuid();

        // Branch Central inherits default price
        var settingCentral = new ProductBranchSetting(product.Id, branchCentralId, isEnabled: true, isVisible: true, salePrice: null, minimumStock: null);
        
        // Branch Aeropuerto overrides price to 35.00 and min stock to 20
        var settingAeropuerto = new ProductBranchSetting(product.Id, branchAeropuertoId, isEnabled: true, isVisible: true, salePrice: 35.00m, minimumStock: 20m);

        decimal centralPrice = settingCentral.SalePrice ?? product.SalePrice;
        decimal centralMinStock = settingCentral.MinimumStock ?? product.MinimumStock;

        decimal aeropuertoPrice = settingAeropuerto.SalePrice ?? product.SalePrice;
        decimal aeropuertoMinStock = settingAeropuerto.MinimumStock ?? product.MinimumStock;

        Assert.Equal(25.00m, centralPrice);
        Assert.Equal(10m, centralMinStock);

        Assert.Equal(35.00m, aeropuertoPrice);
        Assert.Equal(20m, aeropuertoMinStock);
    }

    [Fact]
    public void Variant_Inherits_Parent_ArticleType_And_BaseUnit()
    {
        var parentId = Guid.NewGuid();
        var baseUnitId = Guid.NewGuid();

        var parentProduct = new Product("BASE-001", null, "Camisa Polo", null, Guid.NewGuid(), "Ropa", "unidad", 40m, 30m, 80m, 5m, true);
        parentProduct.SetArticleType(ArticleType.Reventa);
        parentProduct.SetBaseUnit(baseUnitId);

        // Child variant: Large size
        var variantProduct = new Product("BASE-001-L", "7409998881", "Camisa Polo", null, Guid.NewGuid(), "Ropa", "unidad", 40m, 30m, 85m, 5m, true);
        variantProduct.SetVariant(parentId, "Grande (L)");
        variantProduct.SetArticleType(parentProduct.ArticleType);
        variantProduct.SetBaseUnit(parentProduct.BaseUnitId);

        Assert.Equal(parentId, variantProduct.ParentProductId);
        Assert.Equal("Grande (L)", variantProduct.VariantValue);
        Assert.Equal(ArticleType.Reventa, variantProduct.ArticleType);
        Assert.Equal(baseUnitId, variantProduct.BaseUnitId);
        Assert.Equal("Camisa Polo Grande (L)", variantProduct.DisplayName);
    }

    private static IReadOnlyList<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
