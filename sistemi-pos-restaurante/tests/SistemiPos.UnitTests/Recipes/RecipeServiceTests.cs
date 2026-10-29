using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Catalog;
using SistemiPos.Application.Recipes;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Recipes;

namespace SistemiPos.UnitTests.Recipes;

public sealed class RecipeServiceTests
{
    [Fact]
    public async Task Create_Converts_Ingredients_And_Preserves_Yield()
    {
        var fixture = new Fixture();
        var id = await fixture.Service.CreateRecipeAsync(fixture.Request());
        var recipe = Assert.Single(fixture.Repository.Recipes);
        Assert.Equal(id, recipe.Id);
        Assert.Equal(10m, recipe.YieldQuantity);
        Assert.Equal("porcion", recipe.YieldUnitCode);
        var line = Assert.Single(recipe.Ingredients);
        Assert.Equal(2m, line.Quantity);
        Assert.Equal(2000m, line.BaseQuantity);
        Assert.Equal("g", line.BaseUnitCode);
        Assert.Equal(1, fixture.Work.Saves);
        Assert.Equal(1, fixture.Repository.Transactions);
    }

    [Fact]
    public async Task Update_Preserves_Line_Identity_And_Changes_Revision_For_Ingredient_Only_Edit()
    {
        var fixture = new Fixture();
        var id = await fixture.Service.CreateRecipeAsync(fixture.Request());
        var recipe = fixture.Repository.Recipes.Single();
        var lineId = recipe.Ingredients.Single().Id;
        var request = fixture.Request() with { Ingredients = [new(fixture.Flour.Id, 3, fixture.Kilogram.Id)], Version = recipe.Version };
        await fixture.Service.UpdateRecipeAsync(id, request);
        Assert.Equal(lineId, recipe.Ingredients.Single().Id);
        Assert.Equal(3000m, recipe.Ingredients.Single().BaseQuantity);
        Assert.Equal(2, recipe.Revision);
    }

    [Fact]
    public async Task Create_Rejects_Duplicate_Recipe_For_Product()
    {
        var fixture = new Fixture();
        await fixture.Service.CreateRecipeAsync(fixture.Request());
        await Assert.ThrowsAsync<DomainRuleException>(() => fixture.Service.CreateRecipeAsync(fixture.Request()));
        Assert.Single(fixture.Repository.Recipes);
        Assert.Equal(1, fixture.Work.Saves);
    }

    [Theory]
    [InlineData(ArticleType.Reventa)]
    [InlineData(ArticleType.Insumo)]
    [InlineData(ArticleType.Empaque)]
    public async Task Create_Rejects_Unsupported_Output_Types(ArticleType type)
    {
        var fixture = new Fixture();
        fixture.Output.SetArticleType(type);
        await Assert.ThrowsAsync<DomainRuleException>(() => fixture.Service.CreateRecipeAsync(fixture.Request()));
        Assert.Equal(0, fixture.Work.Saves);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.5)]
    public async Task Create_Rejects_Nonpositive_Or_Fractional_Portion_Yield(decimal quantity)
    {
        var fixture = new Fixture();
        await Assert.ThrowsAsync<DomainRuleException>(() => fixture.Service.CreateRecipeAsync(fixture.Request() with { YieldQuantity = quantity }));
        Assert.Equal(0, fixture.Work.Saves);
    }

    [Fact]
    public async Task Create_Rejects_Inactive_Ingredient()
    {
        var fixture = new Fixture();
        fixture.Flour.SetActive(false);
        await Assert.ThrowsAsync<DomainRuleException>(() => fixture.Service.CreateRecipeAsync(fixture.Request()));
        Assert.Empty(fixture.Repository.Recipes);
    }

    [Fact]
    public async Task Create_Rejects_Duplicate_Ingredients()
    {
        var fixture = new Fixture();
        var request = fixture.Request();
        await Assert.ThrowsAsync<DomainRuleException>(() => fixture.Service.CreateRecipeAsync(request with { Ingredients = [request.Ingredients[0], request.Ingredients[0]] }));
    }

    [Fact]
    public async Task Create_Rejects_Direct_Self_Reference()
    {
        var fixture = new Fixture();
        await Assert.ThrowsAsync<DomainRuleException>(() => fixture.Service.CreateRecipeAsync(fixture.Request() with
        { Ingredients = [new(fixture.Output.Id, 1, fixture.Portion.Id)] }));
    }

    [Fact]
    public async Task Create_Rejects_Incompatible_Conversion_Before_Saving()
    {
        var fixture = new Fixture();
        fixture.Conversions.Reject = true;
        await Assert.ThrowsAsync<DomainRuleException>(() => fixture.Service.CreateRecipeAsync(fixture.Request()));
        Assert.Empty(fixture.Repository.Recipes);
        Assert.Equal(0, fixture.Work.Saves);
    }

    [Fact]
    public async Task Create_Rejects_Subrecipe_Without_Active_Definition()
    {
        var fixture = new Fixture();
        var sauce = fixture.AddProduct("Salsa", ArticleType.Subreceta, fixture.Gram);
        await Assert.ThrowsAsync<DomainRuleException>(() => fixture.Service.CreateRecipeAsync(fixture.Request() with
        { Ingredients = [new(sauce.Id, 200, fixture.Gram.Id)] }));
    }

    [Fact]
    public async Task Active_Subrecipe_Can_Be_Used_But_Cannot_Be_Deactivated_While_Referenced()
    {
        var fixture = new Fixture();
        var sauce = fixture.AddProduct("Salsa", ArticleType.Subreceta, fixture.Gram);
        var sauceRequest = fixture.Request() with { ProductId = sauce.Id, YieldQuantity = 1000 };
        var sauceId = await fixture.Service.CreateRecipeAsync(sauceRequest);
        await fixture.Service.CreateRecipeAsync(fixture.Request() with { Ingredients = [new(sauce.Id, 200, fixture.Gram.Id)] });
        var parent = fixture.Repository.Recipes.Single(x => x.ProductId == fixture.Output.Id);
        Assert.Equal(sauceId, parent.Ingredients.Single().SubrecipeId);
        await Assert.ThrowsAsync<DomainRuleException>(() => fixture.Service.UpdateRecipeAsync(sauceId,
            sauceRequest with { IsActive = false, Version = 0 }));
        Assert.True(fixture.Repository.Recipes.Single(x => x.Id == sauceId).IsActive);
    }

    [Fact]
    public async Task Update_Rejects_Indirect_Subrecipe_Cycle()
    {
        var fixture = new Fixture();
        fixture.Output.SetArticleType(ArticleType.Subreceta);
        var firstId = await fixture.Service.CreateRecipeAsync(fixture.Request());
        var second = fixture.AddProduct("Masa", ArticleType.Subreceta, fixture.Portion);
        await fixture.Service.CreateRecipeAsync(fixture.Request() with
        { ProductId = second.Id, Ingredients = [new(fixture.Output.Id, 2, fixture.Portion.Id)] });
        await Assert.ThrowsAsync<DomainRuleException>(() => fixture.Service.UpdateRecipeAsync(firstId,
            fixture.Request() with { Ingredients = [new(second.Id, 1, fixture.Portion.Id)], Version = 0 }));
        Assert.Equal(fixture.Flour.Id, fixture.Repository.Recipes.Single(x => x.Id == firstId).Ingredients.Single().ProductId);
    }

    [Fact]
    public async Task Update_Rejects_Stale_Form()
    {
        var fixture = new Fixture();
        var id = await fixture.Service.CreateRecipeAsync(fixture.Request());
        await Assert.ThrowsAsync<DomainRuleException>(() => fixture.Service.UpdateRecipeAsync(id, fixture.Request() with { Version = 99 }));
        Assert.Equal(1, fixture.Work.Saves);
    }

    [Fact]
    public async Task Unauthorized_User_Cannot_Read_Or_Save_Recipes()
    {
        var fixture = new Fixture();
        fixture.Authorization.Allowed = false;
        await Assert.ThrowsAsync<DomainRuleException>(() => fixture.Service.CreateRecipeAsync(fixture.Request()));
        await Assert.ThrowsAsync<DomainRuleException>(() => fixture.Service.GetRecipesAsync());
        Assert.Equal(0, fixture.Repository.Transactions);
        Assert.Equal(0, fixture.Work.Saves);
    }

    [Fact]
    public async Task Recipe_Offers_And_Accepts_Inventory_Resale_Products()
    {
        var f = new Fixture();
        var product = f.AddProduct("Queso del catálogo", ArticleType.Reventa, f.Gram);
        var options = await f.Service.GetOptionsAsync();
        Assert.Contains(options.Ingredients, x => x.Id == product.Id);
        await f.Service.CreateRecipeAsync(f.Request() with { Ingredients = [new(product.Id, 200, f.Gram.Id)] });
        Assert.Equal(product.Id, f.Repository.Recipes.Single().Ingredients.Single().ProductId);
    }
    [Fact]
    public async Task Subrecipe_Defaults_To_Production_Consumption()
    {
        var f = new Fixture(); f.Output.SetArticleType(ArticleType.Subreceta);
        await f.Service.CreateRecipeAsync(f.Request());
        Assert.Equal(RecipeConsumptionMode.FromProduction, f.Repository.Recipes.Single().ConsumptionMode);
    }
    private sealed class Fixture
    {
        public UnitOfMeasure Gram { get; } = new("g", "Gramo", UnitDimension.Mass, "g", 2);
        public UnitOfMeasure Kilogram { get; } = new("kg", "Kilogramo", UnitDimension.Mass, "kg", 3);
        public UnitOfMeasure Portion { get; } = new("porcion", "Porción", UnitDimension.Count, "porción", 0);
        public FakeRepository Repository { get; } = new();
        public FakeWork Work { get; } = new();
        public FakeAuthorization Authorization { get; } = new();
        public FakeConversions Conversions { get; }
        public Product Output { get; }
        public Product Flour { get; }
        public RecipeService Service { get; }
        public Fixture()
        {
            Repository.Units.AddRange([Gram, Kilogram, Portion]);
            Output = AddProduct("Pizza", ArticleType.Preparado, Portion);
            Flour = AddProduct("Harina", ArticleType.Insumo, Gram);
            Conversions = new FakeConversions(Kilogram.Id, Gram.Id);
            Service = new RecipeService(Repository, Work, Conversions, Authorization, new FakeUser());
        }
        public Product AddProduct(string name, ArticleType type, UnitOfMeasure unit)
        {
            var product = new Product(name, null, name, null, Guid.NewGuid(), "Marca", unit.Name, 0, 0, 0, 0, false);
            product.SetArticleType(type);
            product.SetBaseUnit(unit.Id, unit.Name);
            product.BaseUnit = unit;
            Repository.Products.Add(product);
            return product;
        }
        public SaveRecipeRequest Request() => new(Output.Id, 10, [new(Flour.Id, 2, Kilogram.Id)]);
    }
    private sealed class FakeRepository : IRecipeRepository
    {
        public List<Recipe> Recipes { get; } = [];
        public List<Product> Products { get; } = [];
        public List<UnitOfMeasure> Units { get; } = [];
        public int Transactions { get; private set; }
        public Task<List<Recipe>> GetRecipesAsync(CancellationToken ct) => Task.FromResult(Recipes);
        public Task<Recipe?> GetRecipeAsync(Guid id, CancellationToken ct) => Task.FromResult(Recipes.SingleOrDefault(x => x.Id == id));
        public Task<List<Product>> GetProductsAsync(CancellationToken ct) => Task.FromResult(Products);
        public Task<List<UnitOfMeasure>> GetUnitsAsync(CancellationToken ct) => Task.FromResult(Units);
        public Task CaptureVersionAsync(Recipe recipe, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateRecipeAsync(Recipe recipe, CancellationToken ct) => Task.CompletedTask;
        public Task AddRecipeAsync(Recipe recipe, CancellationToken ct)
        {
            recipe.Product = Products.Single(x => x.Id == recipe.ProductId);
            foreach (var line in recipe.Ingredients) line.Product = Products.Single(x => x.Id == line.ProductId);
            Recipes.Add(recipe);
            return Task.CompletedTask;
        }
        public Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct) { Transactions++; return action(); }
    }
    private sealed class FakeWork : IUnitOfWork
    {
        public int Saves { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct = default) { Saves++; return Task.FromResult(1); }
    }
    private sealed class FakeUser : ICurrentUserAccessor
    {
        public Guid? UserId => Guid.NewGuid();
        public string UserName => "tester";
        public bool IsAuthenticated => true;
    }
    private sealed class FakeAuthorization : IOperationAuthorizationService
    {
        public bool Allowed { get; set; } = true;
        public Task<bool> HasPermissionAsync(string user, string code, CancellationToken ct = default) => Task.FromResult(Allowed);
        public Task<string> ValidateCredentialsAsync(string user, string password, string? permission = null, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class FakeConversions(Guid kilogram, Guid gram) : IUnitConversionService
    {
        public bool Reject { get; set; }
        public Task<decimal> ConvertAsync(decimal quantity, Guid from, Guid to, Guid? product = null, CancellationToken ct = default)
        {
            if (Reject) throw new DomainRuleException("No existe conversión entre las unidades.");
            return Task.FromResult(from == kilogram && to == gram ? quantity * 1000m : quantity);
        }
        public Task<decimal> ConvertByCodeAsync(decimal quantity, string from, string to, Guid? product = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> AreCompatibleAsync(Guid from, Guid to, Guid? product = null, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
