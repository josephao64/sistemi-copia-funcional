namespace SistemiPos.Application.Cash;

public sealed record CashCutDto(Guid SessionId, string RegisterCode, string Cashier, bool IsFinal,
    DateTimeOffset GeneratedAtUtc, decimal ExpectedCash, decimal? DeclaredCash, decimal? Difference,
    decimal CashSales, decimal TransferSales, decimal CardSales, decimal OtherSales = 0, string? CurrencySnapshotJson = null);
