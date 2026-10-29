using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Catalog;

public sealed class ProductUnitConversion : AuditableEntity
{
    public Guid? ProductId { get; private set; }
    public Product? Product { get; set; }
    public Guid FromUnitId { get; private set; }
    public UnitOfMeasure FromUnit { get; set; } = null!;
    public Guid ToUnitId { get; private set; }
    public UnitOfMeasure ToUnit { get; set; } = null!;
    public decimal Factor { get; private set; }
    public bool IsActive { get; private set; } = true;

    private ProductUnitConversion()
    {
    }

    public ProductUnitConversion(
        Guid? productId,
        Guid fromUnitId,
        Guid toUnitId,
        decimal factor,
        bool isActive = true)
    {
        if (fromUnitId == Guid.Empty || toUnitId == Guid.Empty)
            throw new DomainRuleException("Las unidades de origen y destino son requeridas.");
        if (fromUnitId == toUnitId)
            throw new DomainRuleException("La unidad de origen y destino no pueden ser iguales.");
        if (factor <= 0)
            throw new DomainRuleException("El factor de conversión debe ser mayor a cero.");

        ProductId = productId;
        FromUnitId = fromUnitId;
        ToUnitId = toUnitId;
        Factor = factor;
        IsActive = isActive;
    }

    public void UpdateFactor(decimal factor)
    {
        if (factor <= 0)
            throw new DomainRuleException("El factor de conversión debe ser mayor a cero.");
        Factor = factor;
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}
