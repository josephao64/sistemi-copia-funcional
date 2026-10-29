using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Inventory;

namespace SistemiPos.Application.Catalog;

public interface ICatalogRepository
{
    Task<List<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<Category?> GetCategoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsCategoryByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task AddCategoryAsync(Category category, CancellationToken cancellationToken = default);

    Task<List<Product>> SearchProductsAsync(string? query, CancellationToken cancellationToken = default);
    Task<List<Product>> GetProductsByNameAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<Product>());
    Task<List<string>> GetProductBrandsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<string>());
    Task<bool> ExistsBrandByNameAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(false);
    Task AddBrandAsync(Brand brand, CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task<Product?> GetProductAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Product>> GetProductVariantsAsync(Guid parentProductId, CancellationToken cancellationToken = default) => Task.FromResult(new List<Product>());
    Task<string> GetNextInternalCodeAsync(CancellationToken cancellationToken = default) => Task.FromResult($"P-{DateTime.UtcNow.Ticks:000000}");
    Task<bool> ExistsInternalCodeAsync(string internalCode, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> ExistsBarcodeAsync(string barcode, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task AddProductAsync(Product product, CancellationToken cancellationToken = default);
    Task<bool> HasProductUsageAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<bool> HasRecipeRelationsAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    Task RemoveUnusedProductAsync(Product product, CancellationToken cancellationToken = default);

    Task<List<VariantType>> GetVariantTypesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<VariantType>());
    Task<VariantType?> GetVariantTypeByNameAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult<VariantType?>(null);
    Task<VariantOption?> GetVariantOptionByValueAsync(string value, CancellationToken cancellationToken = default) => Task.FromResult<VariantOption?>(null);
    Task AddVariantTypeAsync(VariantType variantType, CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task AddVariantOptionAsync(VariantOption option, CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task AddProductVariantOptionsAsync(IEnumerable<ProductVariantOption> options, CancellationToken cancellationToken = default) => Task.CompletedTask;

    Task<InventoryStock?> GetInventoryStockAsync(Guid productId, CancellationToken cancellationToken = default);
    async Task<IReadOnlyDictionary<Guid, decimal>> GetStockQuantitiesAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default)
    {
        var quantities = new Dictionary<Guid, decimal>();
        foreach (var id in productIds.Distinct())
            quantities[id] = (await GetInventoryStockAsync(id, cancellationToken))?.CurrentStock ?? 0m;
        return quantities;
    }
    Task AddInventoryStockAsync(InventoryStock stock, CancellationToken cancellationToken = default);

    Task<List<UnitOfMeasure>> GetUnitsOfMeasureAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<UnitOfMeasure>());
    Task<UnitOfMeasure?> GetUnitOfMeasureAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<UnitOfMeasure?>(null);
    Task<UnitOfMeasure?> GetUnitOfMeasureByCodeAsync(string code, CancellationToken cancellationToken = default) => Task.FromResult<UnitOfMeasure?>(null);
    Task AddUnitOfMeasureAsync(UnitOfMeasure unit, CancellationToken cancellationToken = default) => Task.CompletedTask;

    Task<List<ProductUnitConversion>> GetUnitConversionsAsync(Guid? productId, CancellationToken cancellationToken = default) => Task.FromResult(new List<ProductUnitConversion>());
    Task AddUnitConversionAsync(ProductUnitConversion conversion, CancellationToken cancellationToken = default) => Task.CompletedTask;

    Task<List<ProductPurchasePresentation>> GetPurchasePresentationsAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult(new List<ProductPurchasePresentation>());
    Task AddPurchasePresentationAsync(ProductPurchasePresentation presentation, CancellationToken cancellationToken = default) => Task.CompletedTask;

    Task<List<ProductBranchSetting>> GetProductBranchSettingsAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult(new List<ProductBranchSetting>());
    Task<ProductBranchSetting?> GetProductBranchSettingAsync(Guid productId, Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult<ProductBranchSetting?>(null);
    Task AddProductBranchSettingAsync(ProductBranchSetting setting, CancellationToken cancellationToken = default) => Task.CompletedTask;

    Task AddCatalogChangeLogAsync(CatalogChangeLog log, CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task<List<CatalogChangeLog>> GetCatalogChangeLogsAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult(new List<CatalogChangeLog>());
}
