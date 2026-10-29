namespace SistemiPos.Application.Catalog;

public interface ICatalogService
{
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<CategoryDto?> GetCategoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateCategoryAsync(SaveCategoryRequest request, CancellationToken cancellationToken = default);
    Task UpdateCategoryAsync(Guid id, SaveCategoryRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductDto>> SearchProductsAsync(string? query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductDto>> SearchProductsAsync(string? query, Guid? branchId, CancellationToken cancellationToken = default) =>
        SearchProductsAsync(query, cancellationToken);
    Task<IReadOnlyList<string>> GetProductBrandsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);
    Task<string> CreateBrandAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(name.Trim());
    Task<ProductDto?> GetProductAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductDto?> GetProductByBranchAsync(Guid id, Guid branchId, CancellationToken cancellationToken = default) =>
        GetProductAsync(id, cancellationToken);
    Task<IReadOnlyList<ProductDto>> GetProductVariantsAsync(Guid parentProductId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductDto>>([]);
    Task<IReadOnlyList<VariantTypeDto>> GetVariantTypesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<VariantTypeDto>>([]);
    Task<VariantTypeDto> CreateVariantTypeAsync(string name, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    Task<VariantOptionDto> CreateVariantOptionAsync(string attributeName, string value, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    Task<string> GenerateProductCodeAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    Task<Guid> CreateProductAsync(SaveProductRequest request, CancellationToken cancellationToken = default);
    Task UpdateProductAsync(Guid id, SaveProductRequest request, CancellationToken cancellationToken = default);
    Task DeleteProductAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UnitOfMeasureDto>> GetUnitsOfMeasureAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UnitOfMeasureDto>>([]);
    Task<UnitOfMeasureDto?> GetUnitOfMeasureAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<UnitOfMeasureDto?>(null);
    Task<Guid> CreateUnitOfMeasureAsync(SaveUnitOfMeasureRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    Task<IReadOnlyList<ProductUnitConversionDto>> GetUnitConversionsAsync(Guid? productId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProductUnitConversionDto>>([]);
    Task<Guid> CreateUnitConversionAsync(SaveUnitConversionRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    Task<IReadOnlyList<ProductPurchasePresentationDto>> GetPurchasePresentationsAsync(Guid productId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProductPurchasePresentationDto>>([]);
    Task<Guid> CreatePurchasePresentationAsync(SavePurchasePresentationRequest request, Guid productId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    Task<IReadOnlyList<ProductBranchSettingDto>> GetProductBranchSettingsAsync(Guid productId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProductBranchSettingDto>>([]);
    Task SaveProductBranchSettingsAsync(Guid productId, IReadOnlyList<SaveProductBranchSettingRequest> settings, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
    Task<IReadOnlyList<CatalogChangeLogDto>> GetCatalogChangeLogsAsync(Guid productId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CatalogChangeLogDto>>([]);
}
