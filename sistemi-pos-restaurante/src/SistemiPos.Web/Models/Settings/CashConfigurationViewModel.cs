using SistemiPos.Domain.Configuration;

namespace SistemiPos.Web.Models.Settings;

public sealed class CashConfigurationViewModel
{
    public IReadOnlyList<CashDenomination> Denominations { get; init; } = [];
    public IReadOnlyList<PosDevice> PosDevices { get; init; } = [];
    public IReadOnlyList<BankAccount> BankAccounts { get; init; } = [];
    public IReadOnlyDictionary<Guid, decimal> PosDeviceTotals { get; init; } = new Dictionary<Guid, decimal>();
    public string CurrencySymbol { get; init; } = "Q";
    public IReadOnlyList<PaymentMethodDefinition> PaymentMethods { get; init; } = [];
}
