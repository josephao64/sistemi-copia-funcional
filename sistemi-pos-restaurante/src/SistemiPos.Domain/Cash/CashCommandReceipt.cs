namespace SistemiPos.Domain.Cash;

// Stored in the same transaction as the financial operation. No passwords or card data.
public sealed class CashCommandReceipt
{
    public string ScopeKey { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public Guid SessionId { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
}
