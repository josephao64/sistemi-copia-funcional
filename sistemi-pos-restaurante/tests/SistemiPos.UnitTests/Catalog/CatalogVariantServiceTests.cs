using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Catalog;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Inventory;

namespace SistemiPos.UnitTests.Catalog;

public sealed class CatalogVariantServiceTests
{
    [Fact]
    public async Task SearchProducts_Loads_Stocks_Once_For_The_Whole_Catalog_And_Preserves_Quantities()
    {
        var category = new Category("Prueba", null);
        var repository = new FakeCatalogRepository(category);
        for (var i = 0; i < 25; i++)
            repository.Products.Add(new Product($"P-{i}", null, $"Producto {i}", null, category.Id,
                "Marca", "Unidad", 0, 0, 10, 0, false) { Category = category });
        var stock = new InventoryStock(repository.Products[0].Id);
        stock.ReplaceForSystem(7.123456m);
        repository.Stocks.Add(stock);
        var service = new CatalogService(repository, new FakeUnitOfWork());

        var products = await service.SearchProductsAsync(null);

        Assert.Equal(25, products.Count);
        Assert.Equal(1, repository.StockBatchReads);
        Assert.Equal(0, repository.SingleStockReads);
        Assert.Equal(7.123456m, products.Single(x => x.Id == stock.ProductId).CurrentStock);
        Assert.All(products.Where(x => x.Id != stock.ProductId), x => Assert.Equal(0m, x.CurrentStock));
    }

    [Fact]
    public async Task CreateVariantTypeAsync_Creates_A_Reusable_Attribute()
    {
        var repository = new FakeCatalogRepository(new Category("Ropa", null));
        var service = new CatalogService(repository, new FakeUnitOfWork());

        var result = await service.CreateVariantTypeAsync("  Talla  ");

        Assert.Equal("Talla", result.Name);
        Assert.Empty(result.Options);
        Assert.Equal(result.Id, Assert.Single(repository.VariantTypes).Id);
    }

    [Fact]
    public async Task CreateVariantTypeAsync_Rejects_An_Existing_Attribute()
    {
        var repository = new FakeCatalogRepository(new Category("Ropa", null));
        repository.VariantTypes.Add(new VariantType("Talla"));
        var service = new CatalogService(repository, new FakeUnitOfWork());

        var exception = await Assert.ThrowsAsync<SistemiPos.Domain.Common.Exceptions.DomainRuleException>(
            () => service.CreateVariantTypeAsync("talla"));

        Assert.Equal("Ya existe un atributo con ese nombre.", exception.Message);
        Assert.Single(repository.VariantTypes);
    }

    [Fact]
    public async Task CreateVariantOptionAsync_Creates_A_Value_For_The_Selected_Attribute()
    {
        var repository = new FakeCatalogRepository(new Category("Ropa", null));
        var type = new VariantType("Talla");
        repository.VariantTypes.Add(type);
        var service = new CatalogService(repository, new FakeUnitOfWork());

        var result = await service.CreateVariantOptionAsync("Talla", "  Mediana  ");

        Assert.Equal("Mediana", result.Value);
        Assert.Equal(result.Id, Assert.Single(type.Options).Id);
    }

    [Fact]
    public async Task CreateVariantOptionAsync_Rejects_A_Duplicate_Value()
    {
        var repository = new FakeCatalogRepository(new Category("Ropa", null));
        var type = new VariantType("Talla");
        type.GetOrAddOption("Mediana");
        repository.VariantTypes.Add(type);
        var service = new CatalogService(repository, new FakeUnitOfWork());

        var exception = await Assert.ThrowsAsync<SistemiPos.Domain.Common.Exceptions.DomainRuleException>(
            () => service.CreateVariantOptionAsync("Talla", "mediana"));

        Assert.Equal("Ya existe un valor con ese nombre.", exception.Message);
        Assert.Single(type.Options);
    }

    [Fact]
    public async Task CreateProductAsync_Rejects_An_Attribute_Not_Created_In_The_Catalog()
    {
        var category = new Category("Ropa", null);
        var service = new CatalogService(new FakeCatalogRepository(category), new FakeUnitOfWork());
        var request = new SaveProductRequest(
            "ROP-000001", null, "Camiseta", null, category.Id, "Marca", "Unidad",
            10m, 0m, 20m, 0m, true, true, NoMovementDaysOverride: 30, BaseUnitId: TestUnit.Id,
            Attributes: [new SaveProductAttributeRequest("Talla", "M")]);

        var exception = await Assert.ThrowsAsync<SistemiPos.Domain.Common.Exceptions.DomainRuleException>(
            () => service.CreateProductAsync(request));

        Assert.Equal("Seleccione un atributo existente o créelo antes de guardar el producto.", exception.Message);
    }

    [Fact]
    public async Task CreateProductAsync_Creates_One_Product_With_Reusable_Attributes()
    {
        var category = new Category("Ropa", null);
        var repository = new FakeCatalogRepository(category);
        var size = new VariantType("Talla");
        size.GetOrAddOption("L");
        var color = new VariantType("Color");
        color.GetOrAddOption("Azul");
        repository.VariantTypes.AddRange([size, color]);
        var service = new CatalogService(repository, new FakeUnitOfWork());
        var request = new SaveProductRequest(
            string.Empty, null, "Pantalón", null, category.Id, "Marca", "Unidad",
            50m, 45m, 100m, 1m, true, true, NoMovementDaysOverride: 30, BaseUnitId: TestUnit.Id,
            Attributes:
            [
                new SaveProductAttributeRequest("Talla", "L"),
                new SaveProductAttributeRequest("Color", "Azul")
            ]);

        var parentId = await service.CreateProductAsync(request);

        var product = Assert.Single(repository.Products, x => x.Id == parentId);
        Assert.False(product.ManagesVariants);
        Assert.Null(product.ParentProductId);
        Assert.Equal(2, product.VariantOptions.Count);
        Assert.Single(repository.Products);
        Assert.Single(repository.Stocks);
        Assert.Equal(2, repository.VariantTypes.Count);
    }

    [Fact]
    public async Task CreateProductAsync_Rejects_An_Existing_Product_And_Returns_Its_Details()
    {
        var category = new Category("Bebidas", null);
        var repository = new FakeCatalogRepository(category);
        var service = new CatalogService(repository, new FakeUnitOfWork());
        var request = new SaveProductRequest(
            string.Empty, "123456", "Coca Cola", null, category.Id, "Coca-Cola", "Unidad",
            3m, 0m, 5m, 1m, true, true, NoMovementDaysOverride: 30, BaseUnitId: TestUnit.Id,
            Attributes: []);

        await service.CreateProductAsync(request);
        var exception = await Assert.ThrowsAsync<SistemiPos.Domain.Common.Exceptions.DomainRuleException>(
            () => service.CreateProductAsync(request));

        Assert.Contains("Este producto ya existe", exception.Message);
        Assert.Contains("BEB-000001", exception.Message);
        Assert.Contains("123456", exception.Message);
        Assert.Single(repository.Products);
    }

    [Fact]
    public async Task CreateProductAsync_Preserves_Manual_Code_And_Creates_Stable_Variant_Codes()
    {
        var category = new Category("Ropa", null);
        var repository = new FakeCatalogRepository(category);
        var size = new VariantType("Talla");
        size.GetOrAddOption("30");
        size.GetOrAddOption("32");
        var color = new VariantType("Color");
        color.GetOrAddOption("Azul");
        repository.VariantTypes.AddRange([size, color]);
        var service = new CatalogService(repository, new FakeUnitOfWork());

        var id = await service.CreateProductAsync(new SaveProductRequest(
            "LEV-501", null, "Pantalón Levi's 501", null, category.Id, "Levi's", "Unidad",
            50m, 0m, 100m, 1m, true, true, NoMovementDaysOverride: 30, BaseUnitId: TestUnit.Id, ManagesVariants: true, ManagesLots: false,
            Combinations:
            [
                new SaveProductCombinationRequest(null, "Talla=30|Color=Azul", "750000001", 50m, 100m, 1m, true),
                new SaveProductCombinationRequest(null, "Talla=32|Color=Azul", "750000002", 50m, 100m, 1m, true)
            ]));

        var parent = Assert.Single(repository.Products, x => x.Id == id);
        Assert.Equal("LEV-501", parent.InternalCode);
        Assert.True(parent.ManagesVariants);
        Assert.All(repository.Products, x => Assert.False(x.ManagesLots));
        Assert.Equal(new[] { "LEV-501-01", "LEV-501-02" }, repository.Products
            .Where(x => x.ParentProductId == id).Select(x => x.InternalCode).OrderBy(x => x).ToArray());
        Assert.Equal(2, repository.Stocks.Count);
    }

    [Fact]
    public async Task Product_Requires_Its_Own_Movement_Threshold_And_A_Valid_Base_Unit()
    {
        var category = new Category("Embutidos", null);
        var repository = new FakeCatalogRepository(category);
        var service = new CatalogService(repository, new FakeUnitOfWork());
        var request = new SaveProductRequest("PEP-001", null, "Pepperoni", null, category.Id,
            "Sin marca", "Unidad", 999, 888, 0, 500, false, true,
            ArticleType: ArticleType.Insumo, BaseUnitId: TestUnit.Id);
        await Assert.ThrowsAsync<SistemiPos.Domain.Common.Exceptions.DomainRuleException>(
            () => service.CreateProductAsync(request));
        await Assert.ThrowsAsync<SistemiPos.Domain.Common.Exceptions.DomainRuleException>(
            () => service.CreateProductAsync(request with { NoMovementDaysOverride = 30, BaseUnitId = Guid.NewGuid() }));
        Assert.Empty(repository.Products);
        await service.CreateProductAsync(request with { NoMovementDaysOverride = 30 });
        var product = Assert.Single(repository.Products);
        Assert.Equal(30, product.NoMovementDaysOverride);
        Assert.Equal(0, product.LastCost);
        Assert.Equal(0, product.PreviousCost);
    }

    [Fact]
    public async Task Used_Product_Cannot_Be_Deleted_Or_Change_Base_Unit()
    {
        var category = new Category("Embutidos", null);
        var repository = new FakeCatalogRepository(category);
        var service = new CatalogService(repository, new FakeUnitOfWork());
        var request = new SaveProductRequest("PEP-001", null, "Pepperoni", null, category.Id,
            "Sin marca", "Unidad", 0, 0, 0, 500, false, true,
            NoMovementDaysOverride: 30, ArticleType: ArticleType.Insumo, BaseUnitId: TestUnit.Id);
        var id = await service.CreateProductAsync(request);
        repository.HasUsage = true;
        var product = Assert.Single(repository.Products);
        product.SetBaseUnit(Guid.NewGuid());
        await Assert.ThrowsAsync<SistemiPos.Domain.Common.Exceptions.DomainRuleException>(
            () => service.UpdateProductAsync(id, request));
        await Assert.ThrowsAsync<SistemiPos.Domain.Common.Exceptions.DomainRuleException>(
            () => service.DeleteProductAsync(id));
        Assert.Single(repository.Products);
    }

    [Fact]
    public async Task Unused_Product_Can_Be_Deleted()
    {
        var category = new Category("Embutidos", null);
        var repository = new FakeCatalogRepository(category);
        var service = new CatalogService(repository, new FakeUnitOfWork());
        var id = await service.CreateProductAsync(new SaveProductRequest("PEP-001", null, "Pepperoni", null,
            category.Id, "Sin marca", "Unidad", 0, 0, 0, 500, false, true,
            NoMovementDaysOverride: 30, ArticleType: ArticleType.Insumo, BaseUnitId: TestUnit.Id));
        await service.DeleteProductAsync(id);
        Assert.Empty(repository.Products);
    }

    [Theory]
    [InlineData(ArticleType.Insumo)]
    [InlineData(ArticleType.Preparado)]
    [InlineData(ArticleType.Reventa)]
    [InlineData(ArticleType.Subreceta)]
    [InlineData(ArticleType.Empaque)]
    public async Task CreateProductAsync_Accepts_Checklist_Types_And_Optional_Lots(ArticleType type)
    {
        var category = new Category("Productos", null);
        var repository = new FakeCatalogRepository(category);
        var service = new CatalogService(repository, new FakeUnitOfWork());
        var id = await service.CreateProductAsync(MasterRequest(category) with { ArticleType = type, ManagesLots = false });
        var dto = await service.GetProductAsync(id);
        Assert.Equal(type, dto!.ArticleType);
        Assert.False(dto.ManagesLots);
    }

    [Theory]
    [InlineData(ArticleType.Ingrediente)]
    [InlineData(ArticleType.ProductoDeVenta)]
    [InlineData(ArticleType.Bebida)]
    public async Task Historical_Type_Is_Preserved_But_Not_Available_For_New_Products(ArticleType type)
    {
        var category = new Category("Productos", null);
        var repository = new FakeCatalogRepository(category);
        var service = new CatalogService(repository, new FakeUnitOfWork());
        var request = MasterRequest(category) with { ArticleType = type };
        await Assert.ThrowsAsync<SistemiPos.Domain.Common.Exceptions.DomainRuleException>(() => service.CreateProductAsync(request));
        var id = await service.CreateProductAsync(request with { ArticleType = ArticleType.Insumo });
        repository.Products.Single().SetArticleType(type);
        await service.UpdateProductAsync(id, request);
        Assert.Equal(type, repository.Products.Single().ArticleType);
    }

    [Fact]
    public async Task Lot_Setting_Requires_Expiry_Consistency_And_Is_Protected_After_Usage()
    {
        var category = new Category("Productos", null);
        var repository = new FakeCatalogRepository(category);
        var service = new CatalogService(repository, new FakeUnitOfWork());
        var request = MasterRequest(category);
        await Assert.ThrowsAsync<SistemiPos.Domain.Common.Exceptions.DomainRuleException>(() =>
            service.CreateProductAsync(request with { ManagesLots = false, ManagesExpiry = true, ExpiryAlertDays = 10 }));
        Assert.Empty(repository.Products);
        var id = await service.CreateProductAsync(request);
        repository.HasUsage = true;
        await Assert.ThrowsAsync<SistemiPos.Domain.Common.Exceptions.DomainRuleException>(() =>
            service.UpdateProductAsync(id, request with { ManagesLots = false }));
        Assert.True(repository.Products.Single().ManagesLots);
    }

    [Fact]
    public async Task Branch_Minimum_Is_Saved_Resolved_And_Can_Return_To_Global_Value()
    {
        var category = new Category("Productos", null);
        var repository = new FakeCatalogRepository(category);
        var work = new FakeUnitOfWork();
        var service = new CatalogService(repository, work);
        var branch = Guid.NewGuid();
        var request = MasterRequest(category) with
        {
            MinimumStock = 5,
            BranchSettings = [new SaveProductBranchSettingRequest(branch, true, true, 25, 20)]
        };
        var id = await service.CreateProductAsync(request);
        Assert.Equal(20, (await service.GetProductByBranchAsync(id, branch))!.MinimumStock);
        Assert.Equal(5, (await service.GetProductByBranchAsync(id, Guid.Empty))!.MinimumStock);
        Assert.Equal(20, (await service.GetProductBranchSettingsAsync(id)).Single().EffectiveMinimumStock);
        await service.UpdateProductAsync(id, request with
        {
            BranchSettings = [new SaveProductBranchSettingRequest(branch, false, false, null, 0)]
        });
        Assert.Equal(2, work.SaveCount);
        var local = (await service.GetProductByBranchAsync(id, branch))!;
        Assert.Equal(0, local.MinimumStock);
        Assert.False(local.IsActive);
        Assert.False(local.IsVisible);
        Assert.False((await service.GetProductBranchSettingsAsync(id)).Single().IsVisible);
        await service.SaveProductBranchSettingsAsync(id, [new SaveProductBranchSettingRequest(branch, true, true, null, null)]);
        Assert.Equal(5, (await service.GetProductByBranchAsync(id, branch))!.MinimumStock);
        Assert.Contains(repository.Logs, x => x.Field == "BranchMinimumStock" && x.BranchId == branch);
    }

    [Fact]
    public async Task Branch_Settings_Reject_Negative_Values_And_Duplicates_Before_Saving()
    {
        var category = new Category("Productos", null);
        var repository = new FakeCatalogRepository(category);
        var work = new FakeUnitOfWork();
        var service = new CatalogService(repository, work);
        var branch = Guid.NewGuid();
        var setting = new SaveProductBranchSettingRequest(branch, true, true, 10, -1);
        await Assert.ThrowsAsync<SistemiPos.Domain.Common.Exceptions.DomainRuleException>(() =>
            service.CreateProductAsync(MasterRequest(category) with { BranchSettings = [setting] }));
        await Assert.ThrowsAsync<SistemiPos.Domain.Common.Exceptions.DomainRuleException>(() =>
            service.CreateProductAsync(MasterRequest(category) with { BranchSettings = [setting with { MinimumStock = 1 }, setting with { MinimumStock = 2 }] }));
        Assert.Empty(repository.Products);
        Assert.Equal(0, work.SaveCount);
    }

    private static SaveProductRequest MasterRequest(Category category) => new("PRO-001", null, "Producto", null,
        category.Id, "Sin marca", "Unidad", 0, 0, 10, 0, false, true,
        NoMovementDaysOverride: 30, ArticleType: ArticleType.Insumo, BaseUnitId: TestUnit.Id);

    [Fact]
    public async Task Branch_Settings_Require_Authorized_Branches()
    {
        var category = new Category("Productos", null);
        var repository = new FakeCatalogRepository(category);
        var work = new FakeUnitOfWork();
        var service = new CatalogService(repository, work, operationContext: new NoAuthorizedBranches());
        var request = MasterRequest(category) with
        {
            BranchSettings = [new SaveProductBranchSettingRequest(Guid.NewGuid(), true, true, 10, 5)]
        };
        await Assert.ThrowsAsync<SistemiPos.Domain.Common.Exceptions.DomainRuleException>(() => service.CreateProductAsync(request));
        Assert.Empty(repository.Products);
        Assert.Equal(0, work.SaveCount);
    }

    private sealed class NoAuthorizedBranches : IOperationContextAccessor
    {
        public Guid? CurrentBranchId => null;
        public Guid? CurrentCompanyId => null;
        public Guid? CurrentBusinessGroupId => null;
        public Guid? CurrentWarehouseId => null;
        public string? CurrentBranchName => null;
        public string? CurrentCompanyName => null;
        public Task<IReadOnlyList<SistemiPos.Application.Organizations.BranchDto>> GetAuthorizedBranchesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SistemiPos.Application.Organizations.BranchDto>>([]);
        public Task SetCurrentBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static readonly UnitOfMeasure TestUnit = new("unidad", "Unidad", UnitDimension.Count, "u");

    private sealed class FakeCatalogRepository(Category category) : ICatalogRepository
    {
        public Task<UnitOfMeasure?> GetUnitOfMeasureAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<UnitOfMeasure?>(id == TestUnit.Id ? TestUnit : null);
        public bool HasUsage { get; set; }
        public Task<bool> HasProductUsageAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult(HasUsage);
        public Task RemoveUnusedProductAsync(Product product, CancellationToken cancellationToken = default) { Products.Remove(product); return Task.CompletedTask; }

        private long nextCode = 1;
        public List<Product> Products { get; } = [];
        public List<InventoryStock> Stocks { get; } = [];
        public int StockBatchReads { get; private set; }
        public int SingleStockReads { get; private set; }
        public List<VariantType> VariantTypes { get; } = [];
        public List<ProductBranchSetting> BranchSettings { get; } = [];
        public List<CatalogChangeLog> Logs { get; } = [];
        public Task AddProductBranchSettingAsync(ProductBranchSetting setting, CancellationToken cancellationToken = default)
        {
            BranchSettings.Add(setting);
            ((ICollection<ProductBranchSetting>)Products.Single(x => x.Id == setting.ProductId).BranchSettings).Add(setting);
            return Task.CompletedTask;
        }
        public Task<List<ProductBranchSetting>> GetProductBranchSettingsAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(BranchSettings.Where(x => x.ProductId == productId).ToList());
        public Task<ProductBranchSetting?> GetProductBranchSettingAsync(Guid productId, Guid branchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(BranchSettings.SingleOrDefault(x => x.ProductId == productId && x.BranchId == branchId));
        public Task AddCatalogChangeLogAsync(CatalogChangeLog log, CancellationToken cancellationToken = default)
        {
            Logs.Add(log);
            return Task.CompletedTask;
        }

        public Task<List<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<Category> { category });
        public Task<Category?> GetCategoryAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Category?>(id == category.Id ? category : null);
        public Task<bool> ExistsCategoryByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddCategoryAsync(Category value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<Product>> SearchProductsAsync(string? query, CancellationToken cancellationToken = default) => Task.FromResult(Products);
        public Task<List<Product>> GetProductsByNameAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(Products.Where(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList());
        public Task<Product?> GetProductAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Products.FirstOrDefault(x => x.Id == id));
        public Task<List<Product>> GetProductVariantsAsync(Guid parentProductId, CancellationToken cancellationToken = default) => Task.FromResult(Products.Where(x => x.ParentProductId == parentProductId).ToList());
        public Task<string> GetNextInternalCodeAsync(CancellationToken cancellationToken = default) => Task.FromResult($"P-{nextCode++:000000}");
        public Task<bool> ExistsInternalCodeAsync(string internalCode, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(Products.Any(x => x.InternalCode == internalCode && x.Id != excludeId));
        public Task<bool> ExistsBarcodeAsync(string barcode, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(Products.Any(x => x.Barcode == barcode && x.Id != excludeId));
        public Task AddProductAsync(Product product, CancellationToken cancellationToken = default) { Products.Add(product); return Task.CompletedTask; }
        public Task<List<VariantType>> GetVariantTypesAsync(CancellationToken cancellationToken = default) => Task.FromResult(VariantTypes);
        public Task<VariantType?> GetVariantTypeByNameAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(VariantTypes.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
        public Task<VariantOption?> GetVariantOptionByValueAsync(string value, CancellationToken cancellationToken = default) =>
            Task.FromResult(VariantTypes.SelectMany(x => x.Options)
                .FirstOrDefault(x => x.Value.Equals(value, StringComparison.OrdinalIgnoreCase)));
        public Task AddVariantTypeAsync(VariantType variantType, CancellationToken cancellationToken = default) { VariantTypes.Add(variantType); return Task.CompletedTask; }
        public Task AddVariantOptionAsync(VariantOption option, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddProductVariantOptionsAsync(IEnumerable<ProductVariantOption> options, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<InventoryStock?> GetInventoryStockAsync(Guid productId, CancellationToken cancellationToken = default)
        {
            SingleStockReads++;
            return Task.FromResult(Stocks.FirstOrDefault(x => x.ProductId == productId));
        }
        public Task<IReadOnlyDictionary<Guid, decimal>> GetStockQuantitiesAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default)
        {
            StockBatchReads++;
            return Task.FromResult<IReadOnlyDictionary<Guid, decimal>>(productIds.ToDictionary(id => id,
                id => Stocks.FirstOrDefault(x => x.ProductId == id)?.CurrentStock ?? 0m));
        }
        public Task AddInventoryStockAsync(InventoryStock stock, CancellationToken cancellationToken = default) { Stocks.Add(stock); return Task.CompletedTask; }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }
}
