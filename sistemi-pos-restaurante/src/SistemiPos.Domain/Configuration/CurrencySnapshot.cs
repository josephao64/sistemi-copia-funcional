namespace SistemiPos.Domain.Configuration;

public sealed record CurrencySnapshot(string Code, string Name, string Symbol, int Decimals,
    string DecimalSeparator, string ThousandsSeparator, string SymbolPosition)
{
    public static CurrencySnapshot From(SystemSetting setting) => new(setting.CurrencyCode, setting.CurrencyName,
        setting.CurrencySymbol, setting.CurrencyDecimals, setting.DecimalSeparator, setting.ThousandsSeparator,
        setting.CurrencySymbolPosition.ToString());
}
