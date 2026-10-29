using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Recipes;

public sealed class RecipeIngredient : Entity
{
    public Guid RecipeId { get; private set; }
    public Guid ProductId { get; private set; }
    public Product Product { get; set; } = null!;
    public Guid? SubrecipeId { get; private set; }
    public Recipe? Subrecipe { get; set; }
    public Guid UnitId { get; private set; }
    public UnitOfMeasure Unit { get; set; } = null!;
    public string UnitCode { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal BaseQuantity { get; private set; }
    public string BaseUnitCode { get; private set; } = string.Empty;
    private RecipeIngredient() { }

    public RecipeIngredient(Guid productId, Guid unitId, string unitCode, decimal quantity,
        decimal baseQuantity, string baseUnitCode, Guid? subrecipeId = null)
    {
        if (productId == Guid.Empty)
            throw new DomainRuleException("El ingrediente es requerido.");
        ProductId = productId;
        Update(unitId, unitCode, quantity, baseQuantity, baseUnitCode, subrecipeId);
    }

    internal void Update(Guid unitId, string unitCode, decimal quantity, decimal baseQuantity,
        string baseUnitCode, Guid? subrecipeId)
    {
        if (unitId == Guid.Empty || string.IsNullOrWhiteSpace(unitCode) || string.IsNullOrWhiteSpace(baseUnitCode)
            || subrecipeId == Guid.Empty)
            throw new DomainRuleException("Selecciona una unidad válida para el ingrediente.");
        RecipeRules.ValidateQuantity(quantity, "La cantidad del ingrediente");
        RecipeRules.ValidateQuantity(baseQuantity, "La cantidad equivalente en unidad base");
        UnitId = unitId;
        UnitCode = unitCode;
        Quantity = quantity;
        BaseQuantity = baseQuantity;
        BaseUnitCode = baseUnitCode;
        SubrecipeId = subrecipeId;
    }
}
