using System.Text.Json;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Recipes;

namespace SistemiPos.Application.Orders;

public sealed record RecipeFormula(Guid ProductId, string Code, string Name, decimal YieldQuantity,
    string YieldUnitCode, string? Instructions, IReadOnlyList<RecipeFormulaLine> Ingredients);
public sealed record RecipeFormulaLine(Guid ProductId, string Code, string Name, Guid UnitId,
    string UnitCode, decimal Quantity, decimal BaseQuantity, string BaseUnitCode, RecipeFormula? Subrecipe);

public static class OrderRecipeSnapshot
{
    public static string? Modifiers(SistemiPos.Domain.Catalog.Product product) => product.ParentProductId.HasValue
        ? JsonSerializer.Serialize(new { product.ParentProductId, product.VariantValue,
            Options = product.VariantOptions.OrderBy(x => x.SortOrder).Select(x => new { x.VariantOptionId,
                Attribute = x.VariantOption.VariantType.Name, x.VariantOption.Value }).ToList() }) : null;
    public static bool ContainsProduct(RecipeFormula formula, Guid productId) => formula.Ingredients
        .Any(x => x.Subrecipe is null ? x.ProductId == productId : ContainsProduct(x.Subrecipe, productId));
    public static RecipeFormula Capture(Recipe recipe, IReadOnlyList<Recipe> recipes,
        IReadOnlyList<RecipeVersion> versions)
    {
        if (!recipe.IsActive) throw new DomainRuleException("La receta debe estar activa.");
        var published = versions.SingleOrDefault(x => x.RecipeId == recipe.Id && x.Number == recipe.Revision);
        return published is null ? Capture(recipe, recipes, versions, []) : FromVersion(published, versions, []);
    }

    private static RecipeFormula Capture(Recipe recipe, IReadOnlyList<Recipe> recipes,
        IReadOnlyList<RecipeVersion> versions, HashSet<Guid> path)
    {
        if (!recipe.IsActive || !path.Add(recipe.Id))
            throw new DomainRuleException("La receta debe estar activa y no contener ciclos.");
        var lines = recipe.Ingredients.Select(line =>
        {
            if (!line.Product.IsActive)
                throw new DomainRuleException("Todos los componentes de la receta deben estar activos al confirmar el pedido.");
            RecipeFormula? nested = null;
            if (line.SubrecipeId is Guid recipeId)
            {
                var version = versions.Where(x => x.RecipeId == recipeId).OrderByDescending(x => x.Number).FirstOrDefault();
                nested = version is null ? Capture(recipes.Single(x => x.Id == recipeId), recipes, versions, path)
                    : FromVersion(version, versions, []);
            }
            return new RecipeFormulaLine(line.ProductId, line.Product.InternalCode, line.Product.DisplayName, line.UnitId,
                line.UnitCode, line.Quantity, line.BaseQuantity, line.BaseUnitCode, nested);
        }).ToList();
        path.Remove(recipe.Id);
        return new RecipeFormula(recipe.ProductId, recipe.Product.InternalCode, recipe.Product.DisplayName,
            recipe.YieldQuantity, recipe.YieldUnitCode, recipe.Instructions, lines);
    }

    private static RecipeFormula FromVersion(RecipeVersion version, IReadOnlyList<RecipeVersion> versions, HashSet<Guid> path)
    {
        if (!path.Add(version.Id)) throw new DomainRuleException("La versión contiene referencias circulares.");
        var snapshot = version.Read();
        var lines = snapshot.Components.Select(x => new RecipeFormulaLine(x.ProductId, x.Code, x.Name,
            x.UnitId, x.UnitCode, x.Quantity, x.BaseQuantity, x.BaseUnitCode,
            x.SubrecipeVersionId is Guid child ? FromVersion(versions.Single(v => v.Id == child), versions, path) : null)).ToList();
        path.Remove(version.Id);
        return new(snapshot.ProductId, "", snapshot.ProductName, snapshot.YieldQuantity,
            snapshot.YieldUnitCode, snapshot.Instructions, lines);
    }

    public static IReadOnlyDictionary<Guid, decimal> Consumption(RecipeFormula formula, decimal quantity)
    {
        RecipeRules.ValidateQuantity(quantity, "La cantidad a preparar");
        var result = new Dictionary<Guid, decimal>();
        void Expand(RecipeFormula current, decimal units)
        {
            if (current.YieldQuantity <= 0) throw new DomainRuleException("El rendimiento de la fÃ³rmula no es vÃ¡lido.");
            foreach (var line in current.Ingredients)
            {
                var needed = units * line.BaseQuantity / current.YieldQuantity;
                if (line.Subrecipe is { } nested) Expand(nested, needed);
                else result[line.ProductId] = result.GetValueOrDefault(line.ProductId) + needed;
            }
        }
        Expand(formula, quantity);
        foreach (var key in result.Keys.ToList())
        {
            var rounded = decimal.Round(result[key], 6, MidpointRounding.AwayFromZero);
            if (rounded <= 0) throw new DomainRuleException("El consumo es menor a la precisiÃ³n de inventario permitida.");
            result[key] = rounded;
        }
        return result;
    }
}
