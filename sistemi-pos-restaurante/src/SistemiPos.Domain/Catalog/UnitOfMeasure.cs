using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Catalog;

public sealed class UnitOfMeasure : AuditableEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public UnitDimension Dimension { get; private set; }
    public string Symbol { get; private set; } = string.Empty;
    public int Precision { get; private set; }
    public bool IsActive { get; private set; } = true;

    private UnitOfMeasure()
    {
    }

    public UnitOfMeasure(
        string code,
        string name,
        UnitDimension dimension,
        string symbol,
        int precision = 2,
        bool isActive = true)
    {
        Update(code, name, dimension, symbol, precision);
        IsActive = isActive;
    }

    public void Update(
        string code,
        string name,
        UnitDimension dimension,
        string symbol,
        int precision = 2)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainRuleException("El código de la unidad es requerido.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainRuleException("El nombre de la unidad es requerido.");
        if (precision < 0 || precision > 6)
            throw new DomainRuleException("La precisión decimal debe estar entre 0 y 6.");

        Code = code.Trim().ToLowerInvariant();
        Name = NameNormalizer.Normalize(name);
        Dimension = dimension;
        Symbol = string.IsNullOrWhiteSpace(symbol) ? code.Trim() : symbol.Trim();
        Precision = precision;
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}
