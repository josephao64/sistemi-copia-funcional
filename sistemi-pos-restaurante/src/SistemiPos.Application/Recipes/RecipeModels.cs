using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Recipes;

namespace SistemiPos.Application.Recipes;

public sealed record SaveRecipeRequest(Guid ProductId, decimal YieldQuantity,
    IReadOnlyList<RecipeIngredientRequest> Ingredients, string? Instructions = null,
    bool IsActive = true, uint? Version = null, RecipeConsumptionMode? ConsumptionMode = null);
public sealed record RecipeIngredientRequest(Guid ProductId, decimal Quantity, Guid UnitId);
public sealed record RecipeDto(Guid Id, Guid ProductId, string ProductCode, string ProductName,
    ArticleType ArticleType, decimal YieldQuantity, string YieldUnitCode, bool IsActive,
    string? Instructions, uint Version, IReadOnlyList<RecipeIngredientDto> Ingredients,
    int Revision = 1, RecipeConsumptionMode ConsumptionMode = RecipeConsumptionMode.OnSale);
public sealed record RecipeIngredientDto(Guid ProductId, string ProductCode, string ProductName,
    Guid UnitId, string UnitCode, decimal Quantity, decimal BaseQuantity, string BaseUnitCode,
    Guid? SubrecipeId);
public sealed record RecipeProductOption(Guid Id, string Code, string Name, ArticleType ArticleType,
    Guid BaseUnitId, string BaseUnitCode, Guid? RecipeId);
public sealed record RecipeUnitOption(Guid Id, string Name, string Code, int Precision);
public sealed record RecipeOptions(IReadOnlyList<RecipeProductOption> Outputs,
    IReadOnlyList<RecipeProductOption> Ingredients, IReadOnlyList<RecipeUnitOption> Units);
