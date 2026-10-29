using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Application.Catalog;

public sealed class UnitConversionService(ICatalogRepository repository) : IUnitConversionService
{
    public async Task<decimal> ConvertAsync(
        decimal quantity,
        Guid fromUnitId,
        Guid toUnitId,
        Guid? productId = null,
        CancellationToken cancellationToken = default)
    {
        if (fromUnitId == Guid.Empty || toUnitId == Guid.Empty)
            throw new DomainRuleException("Las unidades de origen y destino son requeridas.");

        if (fromUnitId == toUnitId)
            return quantity;

        var fromUnit = await repository.GetUnitOfMeasureAsync(fromUnitId, cancellationToken)
            ?? throw new DomainRuleException("La unidad de medida de origen no existe.");

        var toUnit = await repository.GetUnitOfMeasureAsync(toUnitId, cancellationToken)
            ?? throw new DomainRuleException("La unidad de medida de destino no existe.");

        var factor = await ResolveFactorAsync(fromUnit, toUnit, productId, cancellationToken);
        var converted = quantity * factor;

        return decimal.Round(converted, toUnit.Precision);
    }

    public async Task<decimal> ConvertByCodeAsync(
        decimal quantity,
        string fromCode,
        string toCode,
        Guid? productId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fromCode) || string.IsNullOrWhiteSpace(toCode))
            throw new DomainRuleException("Los códigos de unidad son requeridos.");

        var fromUnit = await repository.GetUnitOfMeasureByCodeAsync(fromCode.Trim().ToLowerInvariant(), cancellationToken)
            ?? throw new DomainRuleException($"La unidad con código '{fromCode}' no existe.");

        var toUnit = await repository.GetUnitOfMeasureByCodeAsync(toCode.Trim().ToLowerInvariant(), cancellationToken)
            ?? throw new DomainRuleException($"La unidad con código '{toCode}' no existe.");

        return await ConvertAsync(quantity, fromUnit.Id, toUnit.Id, productId, cancellationToken);
    }

    public async Task<bool> AreCompatibleAsync(
        Guid fromUnitId,
        Guid toUnitId,
        Guid? productId = null,
        CancellationToken cancellationToken = default)
    {
        if (fromUnitId == toUnitId) return true;

        var fromUnit = await repository.GetUnitOfMeasureAsync(fromUnitId, cancellationToken);
        var toUnit = await repository.GetUnitOfMeasureAsync(toUnitId, cancellationToken);
        if (fromUnit is null || toUnit is null) return false;

        try
        {
            var factor = await ResolveFactorAsync(fromUnit, toUnit, productId, cancellationToken);
            return factor > 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<decimal> ResolveFactorAsync(
        UnitOfMeasure fromUnit,
        UnitOfMeasure toUnit,
        Guid? productId,
        CancellationToken cancellationToken)
    {
        // 1. Buscar conversiones registradas específicas del producto
        if (productId.HasValue)
        {
            var productConversions = await repository.GetUnitConversionsAsync(productId.Value, cancellationToken);
            var direct = productConversions.FirstOrDefault(x => x.IsActive && x.FromUnitId == fromUnit.Id && x.ToUnitId == toUnit.Id);
            if (direct is not null)
            {
                if (direct.Factor <= 0) throw new DomainRuleException("El factor de conversión debe ser mayor a cero.");
                return direct.Factor;
            }

            var inverse = productConversions.FirstOrDefault(x => x.IsActive && x.FromUnitId == toUnit.Id && x.ToUnitId == fromUnit.Id);
            if (inverse is not null)
            {
                if (inverse.Factor <= 0) throw new DomainRuleException("El factor de conversión debe ser mayor a cero.");
                return 1m / inverse.Factor;
            }
        }

        // 2. Buscar conversiones globales registradas en la base de datos
        var globalConversions = await repository.GetUnitConversionsAsync(null, cancellationToken);
        var directGlobal = globalConversions.FirstOrDefault(x => x.IsActive && x.FromUnitId == fromUnit.Id && x.ToUnitId == toUnit.Id);
        if (directGlobal is not null)
        {
            if (directGlobal.Factor <= 0) throw new DomainRuleException("El factor de conversión debe ser mayor a cero.");
            return directGlobal.Factor;
        }

        var inverseGlobal = globalConversions.FirstOrDefault(x => x.IsActive && x.FromUnitId == toUnit.Id && x.ToUnitId == fromUnit.Id);
        if (inverseGlobal is not null)
        {
            if (inverseGlobal.Factor <= 0) throw new DomainRuleException("El factor de conversión debe ser mayor a cero.");
            return 1m / inverseGlobal.Factor;
        }

        // 3. Si son de dimensiones distintas y no hay factor específico de producto, rechazar
        if (fromUnit.Dimension != toUnit.Dimension)
        {
            throw new DomainRuleException(
                $"No es posible convertir entre dimensiones incompatibles ({fromUnit.Dimension} y {toUnit.Dimension}) sin una conversión específica del producto.");
        }

        // 4. Conversiones estándar del sistema para unidades de la misma dimensión
        var fromCode = fromUnit.Code.Trim().ToLowerInvariant();
        var toCode = toUnit.Code.Trim().ToLowerInvariant();

        if (fromUnit.Dimension == UnitDimension.Mass)
        {
            if (fromCode == "kg" && toCode == "g") return 1000m;
            if (fromCode == "g" && toCode == "kg") return 0.001m;
            if (fromCode == "lb" && toCode == "g") return 453.592m;
            if (fromCode == "g" && toCode == "lb") return 1m / 453.592m;
            if (fromCode == "lb" && toCode == "kg") return 0.453592m;
            if (fromCode == "kg" && toCode == "lb") return 1m / 0.453592m;
        }
        else if (fromUnit.Dimension == UnitDimension.Volume)
        {
            if (fromCode == "l" && toCode == "ml") return 1000m;
            if (fromCode == "ml" && toCode == "l") return 0.001m;
        }
        else if (fromUnit.Dimension == UnitDimension.Count)
        {
            if (fromCode == "unidad" && toCode == "porcion") return 1m;
            if (fromCode == "porcion" && toCode == "unidad") return 1m;
        }

        throw new DomainRuleException(
            $"No existe una regla de conversión definida entre '{fromUnit.Name}' y '{toUnit.Name}'.");
    }
}
