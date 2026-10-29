using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Configuration;

public sealed class CurrencyDefinition : AuditableEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Symbol { get; private set; } = string.Empty;
    public int Decimals { get; private set; }
    public string DecimalSeparator { get; private set; } = ".";
    public string ThousandsSeparator { get; private set; } = ",";
    public CurrencySymbolPosition SymbolPosition { get; private set; }
    public bool IsActive { get; private set; } = true;
    private CurrencyDefinition() { }
    public CurrencyDefinition(string code, string name, string symbol, int decimals, string decimalSeparator, string thousandsSeparator, CurrencySymbolPosition symbolPosition)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(symbol)) throw new DomainRuleException("Código, nombre y símbolo de moneda son requeridos.");
        if (decimals is < 0 or > 6 || string.IsNullOrEmpty(decimalSeparator) || string.IsNullOrEmpty(thousandsSeparator) || decimalSeparator == thousandsSeparator) throw new DomainRuleException("El formato de moneda no es válido.");
        Code = code.Trim().ToUpperInvariant(); Name = NameNormalizer.Normalize(name); Symbol = symbol.Trim(); Decimals = decimals; DecimalSeparator = decimalSeparator; ThousandsSeparator = thousandsSeparator; SymbolPosition = symbolPosition;
    }
}
