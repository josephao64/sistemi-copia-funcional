using SistemiPos.Application.Catalog;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Inventory;

namespace SistemiPos.UnitTests.Catalog;

public sealed class UnitConversionServiceTests
{
    private readonly UnitOfMeasure _gram = new("g", "Gramo", UnitDimension.Mass, "g", 2);
    private readonly UnitOfMeasure _kilogram = new("kg", "Kilogramo", UnitDimension.Mass, "kg", 3);
    private readonly UnitOfMeasure _pound = new("lb", "Libra", UnitDimension.Mass, "lb", 2);
    private readonly UnitOfMeasure _milliliter = new("ml", "Mililitro", UnitDimension.Volume, "ml", 2);
    private readonly UnitOfMeasure _liter = new("l", "Litro", UnitDimension.Volume, "L", 3);
    private readonly UnitOfMeasure _portion = new("porcion", "Porción", UnitDimension.Count, "pza", 0);

    private FakeConversionRepository CreateRepo(params ProductUnitConversion[] conversions)
    {
        var repo = new FakeConversionRepository();
        repo.Units.AddRange([_gram, _kilogram, _pound, _milliliter, _liter, _portion]);
        repo.Conversions.AddRange(conversions);
        return repo;
    }

    [Fact]
    public async Task ConvertAsync_SameUnit_ReturnsSameQuantity()
    {
        var repo = CreateRepo();
        var service = new UnitConversionService(repo);
        var result = await service.ConvertAsync(15.5m, _gram.Id, _gram.Id);
        Assert.Equal(15.5m, result);
    }

    [Fact]
    public async Task ConvertAsync_UniversalStandard_KgToGrams_Success()
    {
        var repo = CreateRepo();
        var service = new UnitConversionService(repo);
        var result = await service.ConvertAsync(2.5m, _kilogram.Id, _gram.Id);
        Assert.Equal(2500m, result);
    }

    [Fact]
    public async Task ConvertAsync_UniversalStandard_GramsToKg_Success()
    {
        var repo = CreateRepo();
        var service = new UnitConversionService(repo);
        var result = await service.ConvertAsync(1500m, _gram.Id, _kilogram.Id);
        Assert.Equal(1.5m, result);
    }

    [Fact]
    public async Task ConvertAsync_UniversalStandard_LitersToMl_Success()
    {
        var repo = CreateRepo();
        var service = new UnitConversionService(repo);
        var result = await service.ConvertAsync(1.75m, _liter.Id, _milliliter.Id);
        Assert.Equal(1750m, result);
    }

    [Fact]
    public async Task ConvertAsync_IncompatibleDimensions_WithoutProductConversion_ThrowsDomainRuleException()
    {
        var repo = CreateRepo();
        var service = new UnitConversionService(repo);
        // Grams (Mass) to Milliliters (Volume) without custom density conversion
        await Assert.ThrowsAsync<DomainRuleException>(() => service.ConvertAsync(500m, _gram.Id, _milliliter.Id));
    }

    [Fact]
    public async Task ConvertAsync_IncompatibleDimensions_WithProductConversion_Succeeds()
    {
        var productId = Guid.NewGuid();
        // E.g., 1 portion of cheese = 120 grams
        var conversion = new ProductUnitConversion(productId, _portion.Id, _gram.Id, 120m);
        var repo = CreateRepo(conversion);
        var service = new UnitConversionService(repo);

        var result = await service.ConvertAsync(3m, _portion.Id, _gram.Id, productId);
        Assert.Equal(360m, result);

        // Reverse conversion: 240 grams = 2 portions
        var reverseResult = await service.ConvertAsync(240m, _gram.Id, _portion.Id, productId);
        Assert.Equal(2m, reverseResult);
    }

    [Fact]
    public void ProductUnitConversion_FactorZeroOrNegative_ThrowsDomainRuleException()
    {
        var productId = Guid.NewGuid();
        Assert.Throws<DomainRuleException>(() => new ProductUnitConversion(productId, _portion.Id, _gram.Id, 0m));
        Assert.Throws<DomainRuleException>(() => new ProductUnitConversion(productId, _portion.Id, _gram.Id, -5m));
    }

    [Fact]
    public void ProductPurchasePresentation_QuantityZeroOrNegative_ThrowsDomainRuleException()
    {
        var productId = Guid.NewGuid();
        Assert.Throws<DomainRuleException>(() => new ProductPurchasePresentation(productId, "Bolsa", 0m, _kilogram.Id));
        Assert.Throws<DomainRuleException>(() => new ProductPurchasePresentation(productId, "Bolsa", -1m, _kilogram.Id));
    }

    [Fact]
    public async Task ConvertAsync_DecimalPrecision_PreservedCorrectly()
    {
        var repo = CreateRepo();
        var service = new UnitConversionService(repo);
        // 1 lb = 453.592 g. 2 lb = 907.184 g -> rounded to 2 decimals = 907.18 g
        var result = await service.ConvertAsync(2m, _pound.Id, _gram.Id);
        Assert.Equal(907.18m, result);
    }

    private sealed class FakeConversionRepository : ICatalogRepository
    {
        public Task<bool> HasProductUsageAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task RemoveUnusedProductAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public List<UnitOfMeasure> Units { get; } = [];
        public List<ProductUnitConversion> Conversions { get; } = [];

        public Task<List<UnitOfMeasure>> GetUnitsOfMeasureAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Units);

        public Task<UnitOfMeasure?> GetUnitOfMeasureAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Units.FirstOrDefault(u => u.Id == id));

        public Task<UnitOfMeasure?> GetUnitOfMeasureByCodeAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult(Units.FirstOrDefault(u => u.Code.Equals(code, StringComparison.OrdinalIgnoreCase)));

        public Task<List<ProductUnitConversion>> GetUnitConversionsAsync(Guid? productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Conversions.Where(c => c.ProductId == productId).ToList());

        // Interface methods not used by UnitConversionService
        public Task<List<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<Category>());
        public Task<Category?> GetCategoryAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Category?>(null);
        public Task<bool> ExistsCategoryByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddCategoryAsync(Category category, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<Product>> SearchProductsAsync(string? query, CancellationToken cancellationToken = default) => Task.FromResult(new List<Product>());
        public Task<List<Product>> GetProductsByNameAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(new List<Product>());
        public Task<Product?> GetProductAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(null);
        public Task<List<Product>> GetProductVariantsAsync(Guid parentProductId, CancellationToken cancellationToken = default) => Task.FromResult(new List<Product>());
        public Task<string> GetNextInternalCodeAsync(CancellationToken cancellationToken = default) => Task.FromResult("P-000001");
        public Task<bool> ExistsInternalCodeAsync(string internalCode, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ExistsBarcodeAsync(string barcode, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddProductAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<VariantType>> GetVariantTypesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<VariantType>());
        public Task<VariantType?> GetVariantTypeByNameAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult<VariantType?>(null);
        public Task<VariantOption?> GetVariantOptionByValueAsync(string value, CancellationToken cancellationToken = default) => Task.FromResult<VariantOption?>(null);
        public Task AddVariantTypeAsync(VariantType variantType, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddVariantOptionAsync(VariantOption option, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddProductVariantOptionsAsync(IEnumerable<ProductVariantOption> options, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<InventoryStock?> GetInventoryStockAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<InventoryStock?>(null);
        public Task AddInventoryStockAsync(InventoryStock stock, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
