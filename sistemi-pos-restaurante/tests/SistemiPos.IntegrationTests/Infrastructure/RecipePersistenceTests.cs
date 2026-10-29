using Microsoft.EntityFrameworkCore;
using Npgsql;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Catalog;
using SistemiPos.Application.Recipes;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Infrastructure.Catalog;
using SistemiPos.Infrastructure.Persistence;
using SistemiPos.Infrastructure.Recipes;

namespace SistemiPos.IntegrationTests.Infrastructure;

public sealed class RecipePersistenceTests
{
    [PostgresFact]
    public async Task Migrated_Recipes_Persist_Component_Edits_And_Protect_Catalog_Without_Changing_Inventory()
    {
        var source = Environment.GetEnvironmentVariable("SISTEMI_TEST_CONNECTION");
        var databaseName = "sistemi_recipe_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(source)
        { Database = "postgres", Pooling = false }.ConnectionString);
        await admin.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin))
            await command.ExecuteNonQueryAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(source)
        { Database = databaseName, Pooling = false }.ConnectionString).Options;
        try
        {
            await using var db = new AppDbContext(options);
            await db.Database.MigrateAsync();
            var category = new Category("Recetas", null);
            var gram = new UnitOfMeasure("g", "Gramo", UnitDimension.Mass, "g", 2);
            var kilogram = new UnitOfMeasure("kg", "Kilogramo", UnitDimension.Mass, "kg", 3);
            var portion = new UnitOfMeasure("porcion", "Porción", UnitDimension.Count, "porción", 0);
            Product Product(string code, ArticleType type, UnitOfMeasure unit)
            {
                var product = new Product(code, null, code, null, category.Id, "Marca", unit.Name, 0, 0, 0, 0, false);
                product.SetArticleType(type);
                product.SetBaseUnit(unit.Id, unit.Name);
                return product;
            }
            var pizza = Product("PIZZA", ArticleType.Preparado, portion);
            var flour = Product("HARINA", ArticleType.Insumo, gram);
            var cheese = Product("QUESO", ArticleType.Insumo, gram);
            db.Categories.Add(category);
            db.UnitsOfMeasure.AddRange(gram, kilogram, portion);
            db.Products.AddRange(pizza, flour, cheese);
            await db.SaveChangesAsync();
            var catalog = new CatalogRepository(db);
            var service = new RecipeService(new RecipeRepository(db), db, new UnitConversionService(catalog),
                new AllowedAuthorization(), new TestUser());
            var request = new SaveRecipeRequest(pizza.Id, 10, [new(flour.Id, 2, kilogram.Id)]);
            var id = await service.CreateRecipeAsync(request);
            var original = (await service.GetRecipeAsync(id))!;
            Assert.Equal(2000m, original.Ingredients.Single().BaseQuantity);
            db.ChangeTracker.Clear();
            await service.UpdateRecipeAsync(id, request with { Version = original.Version,
                Ingredients = [new(flour.Id, 3, kilogram.Id), new(cheese.Id, 500, gram.Id)] });
            db.ChangeTracker.Clear();
            var updated = (await service.GetRecipeAsync(id))!;
            Assert.Equal(2, updated.Ingredients.Count);
            Assert.Equal(3000m, updated.Ingredients.Single(x => x.ProductId == flour.Id).BaseQuantity);
            Assert.NotEqual(original.Version, updated.Version);
            await Assert.ThrowsAsync<DomainRuleException>(() => service.UpdateRecipeAsync(id, request with { Version = original.Version }));
            db.ChangeTracker.Clear();
            await service.UpdateRecipeAsync(id, request with { Version = updated.Version });
            db.ChangeTracker.Clear();
            Assert.Single((await service.GetRecipeAsync(id))!.Ingredients);
            Assert.True(await catalog.HasProductUsageAsync(flour.Id));
            Assert.True(await catalog.HasRecipeRelationsAsync(pizza.Id));
            Assert.Empty(await db.InventoryMovements.ToListAsync());
            Assert.Empty(await db.InventoryStocks.ToListAsync());
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class TestUser : ICurrentUserAccessor
    {
        public Guid? UserId => Guid.Parse("0b843d47-30dc-49eb-9abc-4898650ebac2");
        public string UserName => "recipe-test";
    }
    private sealed class AllowedAuthorization : IOperationAuthorizationService
    {
        public Task<bool> HasPermissionAsync(string user, string permission, CancellationToken ct = default) => Task.FromResult(true);
        public Task<string> ValidateCredentialsAsync(string user, string password, string? permission = null, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
