using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SistemiPos.Application.Recipes;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Recipes;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Recipes;

public sealed class RecipeRepository(AppDbContext dbContext) : IRecipeRepository
{
    public Task<List<RecipeVersion>> GetVersionsAsync(CancellationToken cancellationToken) =>
        dbContext.RecipeVersions.AsNoTracking().ToListAsync(cancellationToken);
    private IQueryable<Recipe> Query() => dbContext.Recipes.Include(x => x.Product)
        .Include(x => x.Ingredients).ThenInclude(x => x.Product).AsSplitQuery();
    public Task<List<Recipe>> GetRecipesAsync(CancellationToken cancellationToken) =>
        Query().AsNoTracking().ToListAsync(cancellationToken);
    public Task<Recipe?> GetRecipeAsync(Guid id, CancellationToken cancellationToken) =>
        Query().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<List<Product>> GetProductsAsync(CancellationToken cancellationToken) =>
        dbContext.Products.Include(x => x.BaseUnit).Include(x => x.VariantOptions).ThenInclude(x => x.VariantOption).ThenInclude(x => x.VariantType).AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
    public Task<List<UnitOfMeasure>> GetUnitsAsync(CancellationToken cancellationToken) =>
        dbContext.UnitsOfMeasure.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
    public Task AddRecipeAsync(Recipe recipe, CancellationToken cancellationToken) =>
        dbContext.Recipes.AddAsync(recipe, cancellationToken).AsTask();

    public async Task UpdateRecipeAsync(Recipe recipe, CancellationToken cancellationToken)
    {
        // Entity assigns GUIDs before EF sees new lines; explicitly identify inserts.
        var persistedIds = await dbContext.RecipeIngredients.Where(x => x.RecipeId == recipe.Id)
            .Select(x => x.Id).ToHashSetAsync(cancellationToken);
        foreach (var ingredient in recipe.Ingredients.Where(x => !persistedIds.Contains(x.Id)))
            dbContext.Entry(ingredient).State = EntityState.Added;
    }

    public async Task CaptureVersionAsync(Recipe recipe, CancellationToken cancellationToken)
    {
        var products = (await GetProductsAsync(cancellationToken)).ToDictionary(x => x.Id);
        var output = products[recipe.ProductId];
        var components = new List<RecipeComponentSnapshot>();
        foreach (var item in recipe.Ingredients)
        {
            var product = products[item.ProductId];
            Guid? versionId = null;
            if (item.SubrecipeId.HasValue)
                versionId = await dbContext.RecipeVersions.Where(x => x.RecipeId == item.SubrecipeId)
                    .OrderByDescending(x => x.Number).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken)
                    ?? throw new DomainRuleException("La subreceta no tiene una versión publicada.");
            components.Add(new(product.Id, product.InternalCode, product.DisplayName, item.UnitId, item.UnitCode,
                item.Quantity, product.BaseUnitId!.Value, item.BaseUnitCode, product.BaseUnit!.Precision,
                item.BaseQuantity, versionId));
        }
        dbContext.RecipeVersions.Add(new RecipeVersion(recipe.Id, recipe.Revision, DateTimeOffset.UtcNow,
            new(recipe.ProductId, output.DisplayName, recipe.YieldUnitId, recipe.YieldUnitCode,
                output.BaseUnit!.Precision, recipe.YieldQuantity, recipe.Instructions, recipe.ConsumptionMode, components)));
    }

    public async Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var result = await action();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateConcurrencyException)
        { throw new DomainRuleException("La receta fue modificada por otro usuario. Recarga los datos antes de guardar."); }
        catch (Exception ex) when (ex is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure }
            || ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
        { throw new DomainRuleException("Otra operación modificó las recetas. Recarga los datos y vuelve a guardar."); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Recipes_ProductId" })
        { throw new DomainRuleException("Este producto ya tiene una receta. Edita la receta existente."); }
    }
}
