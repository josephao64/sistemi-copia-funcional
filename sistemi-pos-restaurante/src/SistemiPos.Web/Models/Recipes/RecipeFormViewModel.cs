using System.ComponentModel.DataAnnotations;
using SistemiPos.Application.Recipes;

namespace SistemiPos.Web.Models.Recipes;

public sealed class RecipeFormViewModel
{
    public Guid? Id { get; set; }
    public uint? Version { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? YieldUnitCode { get; set; }
    [Range(typeof(decimal), "0.000001", "999999999999.999999", ErrorMessage = "El rendimiento debe ser mayor a cero.")]
    public decimal YieldQuantity { get; set; } = 1;
    [StringLength(4000, ErrorMessage = "Las instrucciones admiten hasta 4000 caracteres.")]
    public string? Instructions { get; set; }
    public SistemiPos.Domain.Recipes.RecipeConsumptionMode? ConsumptionMode { get; set; }
    public bool IsActive { get; set; } = true;
    public List<RecipeIngredientFormViewModel> Ingredients { get; set; } = [new()];
    public RecipeOptions Options { get; set; } = new([], [], []);
}

public sealed class RecipeIngredientFormViewModel
{
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public Guid UnitId { get; set; }
    [Range(typeof(decimal), "0.000001", "999999999999.999999", ErrorMessage = "La cantidad debe ser mayor a cero.")]
    public decimal Quantity { get; set; }
}

public sealed record RecipesIndexViewModel(IReadOnlyList<RecipeDto> Recipes, string? Query, bool? IsActive);
