using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SistemiPos.Application.Catalog;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Inventory;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Catalog;

public sealed class CatalogRepository(AppDbContext dbContext) : ICatalogRepository
{
    public Task<List<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        dbContext.Categories.AsNoTracking().ToListAsync(cancellationToken);

    public Task<Category?> GetCategoryAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Categories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> ExistsCategoryByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default) =>
        dbContext.Categories.AnyAsync(
            x => x.Name == name.Trim() && (!excludeId.HasValue || x.Id != excludeId.Value),
            cancellationToken);

    public Task AddCategoryAsync(Category category, CancellationToken cancellationToken = default) =>
        dbContext.Categories.AddAsync(category, cancellationToken).AsTask();

    public Task<List<Product>> SearchProductsAsync(string? query, CancellationToken cancellationToken = default)
    {
        var products = dbContext.Products
            .Include(x => x.BaseUnit)
            .Include(x => x.PurchasePresentations)
            .Include(x => x.Category)
            .Include(x => x.PriceRules)
            .Include(x => x.VariantOptions).ThenInclude(x => x.VariantOption).ThenInclude(x => x.VariantType)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLower();
            products = products.Where(x =>
                x.InternalCode.ToLower().Contains(term)
                || (x.Barcode != null && x.Barcode.ToLower().Contains(term))
                || x.Name.ToLower().Contains(term)
                || (x.SupplierInvoiceName != null && x.SupplierInvoiceName.ToLower().Contains(term))
                || (x.SupplierDescription != null && x.SupplierDescription.ToLower().Contains(term))
                || x.Brand.ToLower().Contains(term)
                || (x.Description != null && x.Description.ToLower().Contains(term))
                || x.Category.Name.ToLower().Contains(term)
                || (x.VariantValue != null && x.VariantValue.ToLower().Contains(term))
                || x.VariantOptions.Any(y => y.VariantOption.Value.ToLower().Contains(term)
                    || y.VariantOption.VariantType.Name.ToLower().Contains(term)));
        }

        return products.AsNoTracking().AsSplitQuery().ToListAsync(cancellationToken);
    }

    public Task<List<string>> GetProductBrandsAsync(CancellationToken cancellationToken = default) =>
        dbContext.Brands.AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => x.Name)
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsBrandByNameAsync(string name, CancellationToken cancellationToken = default) =>
        dbContext.Brands.AnyAsync(x => x.Name == name.Trim(), cancellationToken);

    public Task AddBrandAsync(Brand brand, CancellationToken cancellationToken = default) =>
        dbContext.Brands.AddAsync(brand, cancellationToken).AsTask();

    public Task<List<Product>> GetProductsByNameAsync(string name, CancellationToken cancellationToken = default) =>
        dbContext.Products
            .Include(x => x.Category)
            .Include(x => x.VariantOptions).ThenInclude(x => x.VariantOption).ThenInclude(x => x.VariantType)
            .Where(x => x.Name == name.Trim())
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<Product?> GetProductAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Products
            .Include(x => x.Category)
            .Include(x => x.BaseUnit)
            .Include(x => x.PriceRules)
            .Include(x => x.VariantOptions).ThenInclude(x => x.VariantOption).ThenInclude(x => x.VariantType)
            .Include(x => x.BranchSettings).ThenInclude(x => x.Branch)
            .Include(x => x.PurchasePresentations).ThenInclude(x => x.PurchaseUnit)
            .Include(x => x.UnitConversions).ThenInclude(x => x.FromUnit)
            .Include(x => x.UnitConversions).ThenInclude(x => x.ToUnit)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<List<Product>> GetProductVariantsAsync(Guid parentProductId, CancellationToken cancellationToken = default) =>
        dbContext.Products
            .Include(x => x.Category)
            .Include(x => x.PriceRules)
            .Include(x => x.VariantOptions).ThenInclude(x => x.VariantOption).ThenInclude(x => x.VariantType)
            .Where(x => x.ParentProductId == parentProductId)
            .ToListAsync(cancellationToken);

    public async Task<string> GetNextInternalCodeAsync(CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync(cancellationToken);

        long value;
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT nextval('public.\"ProductInternalCodeSequence\"');";
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            var result = await command.ExecuteScalarAsync(cancellationToken);
            value = Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }

        return $"P-{value:000000}";
    }

    public async Task<bool> ExistsInternalCodeAsync(string internalCode, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var value = internalCode.Trim();
        return dbContext.Products.Local.Any(x => x.InternalCode == value && (!excludeId.HasValue || x.Id != excludeId.Value))
            || await dbContext.Products.AnyAsync(x => x.InternalCode == value && (!excludeId.HasValue || x.Id != excludeId.Value), cancellationToken);
    }

    public async Task<bool> ExistsBarcodeAsync(string barcode, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var value = barcode.Trim();
        return dbContext.Products.Local.Any(x => x.Barcode == value && (!excludeId.HasValue || x.Id != excludeId.Value))
            || await dbContext.Products.AnyAsync(x => x.Barcode == value && (!excludeId.HasValue || x.Id != excludeId.Value), cancellationToken);
    }

    public async Task<bool> HasProductUsageAsync(Guid productId, CancellationToken cancellationToken = default) =>
        await dbContext.Set<SistemiPos.Domain.Orders.RestaurantOrderLine>().AnyAsync(x => x.ProductId == productId, cancellationToken) ||
        await HasRecipeRelationsAsync(productId, cancellationToken) ||
        await dbContext.InventoryMovements.AnyAsync(x => x.ProductId == productId, cancellationToken) ||
        await dbContext.PurchaseItems.AnyAsync(x => x.ProductId == productId, cancellationToken) ||
        await dbContext.PurchaseOrderItems.AnyAsync(x => x.ProductId == productId, cancellationToken) ||
        await dbContext.PurchaseReceiptItems.AnyAsync(x => x.ProductId == productId, cancellationToken) ||
        await dbContext.SaleItems.AnyAsync(x => x.ProductId == productId, cancellationToken) ||
        await dbContext.SaleReturnItems.AnyAsync(x => x.ProductId == productId, cancellationToken) ||
        await dbContext.DiscountApprovalItems.AnyAsync(x => x.ProductId == productId, cancellationToken) ||
        await dbContext.PhysicalInventoryCountItems.AnyAsync(x => x.ProductId == productId, cancellationToken) ||
        await dbContext.InventoryExpiryLots.AnyAsync(x => x.ProductId == productId, cancellationToken) ||
        await dbContext.ProductSuppliers.AnyAsync(x => x.ProductId == productId, cancellationToken) ||
        await dbContext.SupplierProductPriceHistory.AnyAsync(x => x.ProductId == productId, cancellationToken) ||
        await dbContext.InventoryStocks.AnyAsync(x => x.ProductId == productId && x.CurrentStock != 0, cancellationToken) ||
        await dbContext.Products.AnyAsync(x => x.ParentProductId == productId, cancellationToken);

    public async Task<bool> HasRecipeRelationsAsync(Guid productId, CancellationToken cancellationToken = default) =>
        await dbContext.Recipes.AnyAsync(x => x.ProductId == productId || x.Product.ParentProductId == productId, cancellationToken) ||
        await dbContext.RecipeIngredients.AnyAsync(x => x.ProductId == productId || x.Product.ParentProductId == productId, cancellationToken) ||
        await HasHistoricalRecipeComponentAsync(productId, cancellationToken);

    private async Task<bool> HasHistoricalRecipeComponentAsync(Guid productId, CancellationToken ct)
    {
        var ids = await dbContext.Products.Where(x => x.Id == productId || x.ParentProductId == productId).Select(x => x.Id).ToListAsync(ct);
        var versions = await dbContext.RecipeVersions.AsNoTracking().ToListAsync(ct);
        return versions.Any(x => x.Read().Components.Any(c => ids.Contains(c.ProductId)));
    }

    public async Task RemoveUnusedProductAsync(Product product, CancellationToken cancellationToken = default)
    {
        dbContext.InventoryStocks.RemoveRange(await dbContext.InventoryStocks
            .Where(x => x.ProductId == product.Id && x.CurrentStock == 0).ToListAsync(cancellationToken));
        dbContext.Products.Remove(product);
    }

    public Task AddProductAsync(Product product, CancellationToken cancellationToken = default) =>
        dbContext.Products.AddAsync(product, cancellationToken).AsTask();

    public Task<List<VariantType>> GetVariantTypesAsync(CancellationToken cancellationToken = default) =>
        dbContext.VariantTypes.Include(x => x.Options).AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<VariantType?> GetVariantTypeByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var value = name.Trim();
        var local = dbContext.VariantTypes.Local.FirstOrDefault(x =>
            string.Equals(x.Name, value, StringComparison.OrdinalIgnoreCase));
        return local is not null
            ? Task.FromResult<VariantType?>(local)
            : dbContext.VariantTypes.Include(x => x.Options).FirstOrDefaultAsync(x => x.Name == value, cancellationToken);
    }

    public Task<VariantOption?> GetVariantOptionByValueAsync(string value, CancellationToken cancellationToken = default)
    {
        var normalized = value.Trim();
        var local = dbContext.VariantOptions.Local.FirstOrDefault(x =>
            string.Equals(x.Value, normalized, StringComparison.OrdinalIgnoreCase));
        return local is not null
            ? Task.FromResult<VariantOption?>(local)
            : dbContext.VariantOptions.Include(x => x.VariantType)
                .FirstOrDefaultAsync(x => x.Value.ToUpper() == normalized.ToUpper(), cancellationToken);
    }

    public Task AddVariantTypeAsync(VariantType variantType, CancellationToken cancellationToken = default) =>
        dbContext.VariantTypes.AddAsync(variantType, cancellationToken).AsTask();

    public Task AddVariantOptionAsync(VariantOption option, CancellationToken cancellationToken = default) =>
        dbContext.VariantOptions.AddAsync(option, cancellationToken).AsTask();

    public Task AddProductVariantOptionsAsync(IEnumerable<ProductVariantOption> options, CancellationToken cancellationToken = default) =>
        dbContext.ProductVariantOptions.AddRangeAsync(options, cancellationToken);

    public Task<InventoryStock?> GetInventoryStockAsync(Guid productId, CancellationToken cancellationToken = default) =>
        dbContext.InventoryStocks.AsNoTracking().FirstOrDefaultAsync(x => x.ProductId == productId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetStockQuantitiesAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0) return new Dictionary<Guid, decimal>();
        // Preserve the existing first-stock lookup. Do not sum or invent a warehouse selection here.
        return await dbContext.Products.AsNoTracking().Where(x => productIds.Contains(x.Id))
            .Select(product => new
            {
                product.Id,
                Quantity = dbContext.InventoryStocks.Where(stock => stock.ProductId == product.Id)
                    .Select(stock => (decimal?)stock.CurrentStock).FirstOrDefault() ?? 0m
            })
            .ToDictionaryAsync(x => x.Id, x => x.Quantity, cancellationToken);
    }

    public Task AddInventoryStockAsync(InventoryStock stock, CancellationToken cancellationToken = default) =>
        dbContext.InventoryStocks.AddAsync(stock, cancellationToken).AsTask();

    public Task<List<UnitOfMeasure>> GetUnitsOfMeasureAsync(CancellationToken cancellationToken = default) =>
        dbContext.UnitsOfMeasure.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<UnitOfMeasure?> GetUnitOfMeasureAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.UnitsOfMeasure.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<UnitOfMeasure?> GetUnitOfMeasureByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim().ToLowerInvariant();
        return dbContext.UnitsOfMeasure.FirstOrDefaultAsync(x => x.Code == normalized, cancellationToken);
    }

    public Task AddUnitOfMeasureAsync(UnitOfMeasure unit, CancellationToken cancellationToken = default) =>
        dbContext.UnitsOfMeasure.AddAsync(unit, cancellationToken).AsTask();

    public Task<List<ProductUnitConversion>> GetUnitConversionsAsync(Guid? productId, CancellationToken cancellationToken = default) =>
        dbContext.ProductUnitConversions
            .Include(x => x.FromUnit)
            .Include(x => x.ToUnit)
            .Where(x => x.ProductId == productId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task AddUnitConversionAsync(ProductUnitConversion conversion, CancellationToken cancellationToken = default) =>
        dbContext.ProductUnitConversions.AddAsync(conversion, cancellationToken).AsTask();

    public Task<List<ProductPurchasePresentation>> GetPurchasePresentationsAsync(Guid productId, CancellationToken cancellationToken = default) =>
        dbContext.ProductPurchasePresentations
            .Include(x => x.PurchaseUnit)
            .Where(x => x.ProductId == productId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task AddPurchasePresentationAsync(ProductPurchasePresentation presentation, CancellationToken cancellationToken = default) =>
        dbContext.ProductPurchasePresentations.AddAsync(presentation, cancellationToken).AsTask();

    public Task<List<ProductBranchSetting>> GetProductBranchSettingsAsync(Guid productId, CancellationToken cancellationToken = default) =>
        dbContext.ProductBranchSettings
            .Include(x => x.Branch)
            .Where(x => x.ProductId == productId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<ProductBranchSetting?> GetProductBranchSettingAsync(Guid productId, Guid branchId, CancellationToken cancellationToken = default) =>
        dbContext.ProductBranchSettings
            .Include(x => x.Branch)
            .FirstOrDefaultAsync(x => x.ProductId == productId && x.BranchId == branchId, cancellationToken);

    public Task AddProductBranchSettingAsync(ProductBranchSetting setting, CancellationToken cancellationToken = default) =>
        dbContext.ProductBranchSettings.AddAsync(setting, cancellationToken).AsTask();

    public Task AddCatalogChangeLogAsync(CatalogChangeLog log, CancellationToken cancellationToken = default) =>
        dbContext.CatalogChangeLogs.AddAsync(log, cancellationToken).AsTask();

    public Task<List<CatalogChangeLog>> GetCatalogChangeLogsAsync(Guid productId, CancellationToken cancellationToken = default) =>
        dbContext.CatalogChangeLogs
            .Where(x => x.ProductId == productId)
            .OrderByDescending(x => x.TimestampUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
}
