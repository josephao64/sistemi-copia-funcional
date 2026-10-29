using SistemiPos.Domain.Configuration;

namespace SistemiPos.Web.Models.Cash;

public sealed record CashDenominationCountViewModel(
    string Prefix,
    string CurrencySymbol,
    IReadOnlyList<CashDenomination> Denominations,
    IReadOnlyDictionary<Guid, int>? Quantities = null);
