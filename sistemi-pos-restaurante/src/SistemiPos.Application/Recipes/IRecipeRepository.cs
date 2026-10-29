using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Recipes;

namespace SistemiPos.Application.Recipes;

public interface IRecipeRepository
{
    Task<List<Recipe>> GetRecipesAsync(CancellationToken cancellationToken);
    Task<Recipe?> GetRecipeAsync(Guid id, CancellationToken cancellationToken);
    Task<List<Product>> GetProductsAsync(CancellationToken cancellationToken);
    Task<List<UnitOfMeasure>> GetUnitsAsync(CancellationToken cancellationToken);
    Task AddRecipeAsync(Recipe recipe, CancellationToken cancellationToken);
    Task UpdateRecipeAsync(Recipe recipe, CancellationToken cancellationToken);
    Task<List<RecipeVersion>> GetVersionsAsync(CancellationToken cancellationToken) => Task.FromResult(new List<RecipeVersion>());
    Task CaptureVersionAsync(Recipe recipe, CancellationToken cancellationToken);
    Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken);
}
