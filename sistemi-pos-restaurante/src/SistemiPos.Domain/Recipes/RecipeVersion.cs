using System.Text.Json;
using SistemiPos.Domain.Common;

namespace SistemiPos.Domain.Recipes;

public enum RecipeConsumptionMode { OnSale = 1, FromProduction = 2 }

public sealed record RecipeComponentSnapshot(Guid ProductId, string Code, string Name,
    Guid UnitId, string UnitCode, decimal Quantity, Guid BaseUnitId, string BaseUnitCode,
    int Precision, decimal BaseQuantity, Guid? SubrecipeVersionId);

public sealed record RecipeSnapshot(Guid ProductId, string ProductName, Guid YieldUnitId,
    string YieldUnitCode, int YieldPrecision, decimal YieldQuantity, string? Instructions,
    RecipeConsumptionMode ConsumptionMode, IReadOnlyList<RecipeComponentSnapshot> Components);

public sealed class RecipeVersion : Entity
{
    public Guid RecipeId { get; private set; }
    public int Number { get; private set; }
    public DateTimeOffset EffectiveAtUtc { get; private set; }
    public string DefinitionJson { get; private set; } = "{}";
    public RecipeSnapshot Read() => JsonSerializer.Deserialize<RecipeSnapshot>(DefinitionJson)!;
    private RecipeVersion() { }
    public RecipeVersion(Guid recipeId, int number, DateTimeOffset effectiveAtUtc, RecipeSnapshot definition)
    {
        RecipeId = recipeId; Number = number; EffectiveAtUtc = effectiveAtUtc;
        DefinitionJson = JsonSerializer.Serialize(definition);
    }
}

public sealed record RecipeConsumption(Guid ProductId, string Name, string UnitCode, decimal Quantity,
    decimal UnitCost, string CostSource);

public static class RecipeQuantities
{
    public static decimal Scale(decimal quantity, decimal output, decimal yield, int precision)
    {
        var result = decimal.Round(quantity * output / yield, precision, MidpointRounding.AwayFromZero);
        RecipeRules.ValidateQuantity(result, "La cantidad calculada del componente");
        return result;
    }
}
