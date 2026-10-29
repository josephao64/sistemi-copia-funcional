using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Recipes;

public sealed class Recipe : AuditableEntity
{
    private readonly List<RecipeIngredient> _ingredients = [];
    public Guid ProductId { get; private set; }
    public Product Product { get; set; } = null!;
    public Guid YieldUnitId { get; private set; }
    public UnitOfMeasure YieldUnit { get; set; } = null!;
    public string YieldUnitCode { get; private set; } = string.Empty;
    public decimal YieldQuantity { get; private set; }
    public string? Instructions { get; private set; }
    public bool IsActive { get; private set; } = true;
    public uint Version { get; private set; }
    public int Revision { get; private set; }
    public RecipeConsumptionMode ConsumptionMode { get; private set; } = RecipeConsumptionMode.OnSale;
    public void SetConsumptionMode(RecipeConsumptionMode mode)
    {
        if (!Enum.IsDefined(mode)) throw new DomainRuleException("Selecciona un modo de consumo válido.");
        ConsumptionMode = mode;
    }
    public IReadOnlyCollection<RecipeIngredient> Ingredients => _ingredients;

    private Recipe() { }

    public Recipe(Guid productId, Guid yieldUnitId, string yieldUnitCode, decimal yieldQuantity,
        IEnumerable<RecipeIngredient> ingredients, string? instructions, bool isActive = true)
    {
        if (productId == Guid.Empty || yieldUnitId == Guid.Empty || string.IsNullOrWhiteSpace(yieldUnitCode))
            throw new DomainRuleException("El producto y su unidad base son requeridos para la receta.");
        ProductId = productId;
        YieldUnitId = yieldUnitId;
        YieldUnitCode = yieldUnitCode;
        Update(yieldQuantity, ingredients, instructions, isActive);
    }

    public void Update(decimal yieldQuantity, IEnumerable<RecipeIngredient> ingredients,
        string? instructions, bool isActive)
    {
        var values = ingredients.ToList();
        RecipeRules.ValidateQuantity(yieldQuantity, "El rendimiento");
        if (values.Count == 0)
            throw new DomainRuleException("La receta debe incluir al menos un ingrediente o subreceta.");
        if (values.Any(x => x.ProductId == ProductId))
            throw new DomainRuleException("El producto elaborado no puede ser ingrediente de su propia receta.");
        if (values.GroupBy(x => x.ProductId).Any(x => x.Count() > 1))
            throw new DomainRuleException("Un ingrediente o subreceta no puede repetirse en la receta.");
        if (instructions?.Trim().Length > 4000)
            throw new DomainRuleException("Las instrucciones admiten hasta 4000 caracteres.");
        YieldQuantity = yieldQuantity;
        Instructions = string.IsNullOrWhiteSpace(instructions) ? null : instructions.Trim();
        IsActive = isActive;
        Revision++;
        foreach (var existing in _ingredients.ToList())
        {
            var value = values.SingleOrDefault(x => x.ProductId == existing.ProductId);
            if (value is null) _ingredients.Remove(existing);
            else existing.Update(value.UnitId, value.UnitCode, value.Quantity, value.BaseQuantity,
                value.BaseUnitCode, value.SubrecipeId);
        }
        var productIds = _ingredients.Select(x => x.ProductId).ToHashSet();
        _ingredients.AddRange(values.Where(x => !productIds.Contains(x.ProductId)));
    }
}

public static class RecipeRules
{
    public static bool CanHaveRecipe(ArticleType type) => type is ArticleType.Preparado or ArticleType.Subreceta;
    public static bool IsIngredient(ArticleType type) => type is ArticleType.Insumo or ArticleType.Ingrediente or ArticleType.Empaque;

    public static void ValidateQuantity(decimal value, string label)
    {
        if (value <= 0 || value > 999999999999.999999m || decimal.Round(value, 6) != value)
            throw new DomainRuleException($"{label} debe ser mayor a cero y admitir como máximo seis decimales.");
    }

    public static void EnsureAcyclic(Guid recipeId, IEnumerable<Guid> dependencies,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> graph)
    {
        var visited = new HashSet<Guid>();
        var pending = new Stack<Guid>(dependencies);
        while (pending.TryPop(out var current))
        {
            if (current == recipeId)
                throw new DomainRuleException("Las subrecetas no pueden formar referencias circulares.");
            if (!visited.Add(current)) continue;
            if (graph.TryGetValue(current, out var children))
                foreach (var child in children) pending.Push(child);
        }
    }
}
