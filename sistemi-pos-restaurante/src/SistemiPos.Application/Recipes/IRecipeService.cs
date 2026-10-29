namespace SistemiPos.Application.Recipes;

public interface IRecipeService
{
    Task<IReadOnlyList<RecipeDto>> GetRecipesAsync(CancellationToken cancellationToken = default);
    Task<RecipeDto?> GetRecipeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RecipeOptions> GetOptionsAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateRecipeAsync(SaveRecipeRequest request, CancellationToken cancellationToken = default);
    Task UpdateRecipeAsync(Guid id, SaveRecipeRequest request, CancellationToken cancellationToken = default);
}
