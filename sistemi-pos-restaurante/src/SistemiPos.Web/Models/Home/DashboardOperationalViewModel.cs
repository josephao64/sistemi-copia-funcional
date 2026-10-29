using SistemiPos.Domain.Configuration;

namespace SistemiPos.Web.Models.Home;

public sealed class DashboardOperationalViewModel
{
    public DateTimeOffset UpdatedAt { get; init; }
    public int OpenCashSessionsCount { get; init; }
    public int OverdueReceivablesCount { get; init; }
    public decimal OverdueReceivablesAmount { get; init; }
    public int LowStockCount { get; init; }
    public int OutOfStockCount { get; init; }
    public int ExpiringLotsCount { get; init; }
    public int PendingReceiptsCount { get; init; }
    public string CurrencySymbol { get; init; } = "Q";
    public int CurrencyDecimals { get; init; } = 2;
    public CurrencySymbolPosition CurrencySymbolPosition { get; init; } = CurrencySymbolPosition.Before;
}
