namespace SistemiPos.Web.Models.Offline;

public sealed record OfflineBootstrapResponse(
    IReadOnlyList<OfflineProductDto> Products,
    IReadOnlyList<OfflineCustomerDto> Customers,
    IReadOnlyList<OfflineCashSessionDto> CashSessions,
    IReadOnlyList<OfflineBankAccountDto> BankAccounts,
    OfflineSettingDto Settings,
    IReadOnlyList<OfflinePosDeviceDto>? PosDevices = null,
    SistemiPos.Application.Sales.OfflinePosTicket? OfflineContext = null,
    string? AntiforgeryToken = null);

public sealed record OfflinePosDeviceDto(Guid Id, string Name);

public sealed record OfflineProductDto(
    Guid Id,
    string InternalCode,
    string? Barcode,
    string Name,
    decimal SalePrice,
    decimal LastSyncedStock,
    bool MultiPriceEnabled = false,
    string MultiPriceMode = "MinimumQuantity",
    IReadOnlyList<OfflineProductPriceRuleDto>? PriceRules = null);

public sealed record OfflineProductPriceRuleDto(
    decimal Quantity,
    decimal Price,
    bool IsActive);

public sealed record OfflineCustomerDto(
    Guid Id,
    string Name,
    string? Nit);

public sealed record OfflineCashSessionDto(
    Guid Id,
    string RegisterCode,
    string CashierUserName);

public sealed record OfflineBankAccountDto(
    Guid Id,
    string Bank,
    string AccountName,
    string AccountNumber);

public sealed record OfflineSettingDto(
    string BusinessName,
    string? BusinessNit,
    string? BusinessAddress,
    string? BusinessPhone,
    string CurrencyCode,
    string CurrencyName,
    string CurrencySymbol,
    int CurrencyDecimals,
    string DecimalSeparator,
    string ThousandsSeparator,
    string CurrencySymbolPosition,
    decimal TaxPercent,
    bool ShowTaxOnReceipt,
    string? ReceiptFooter,
    bool CashPaymentsEnabled,
    bool TransferPaymentsEnabled,
    bool PosPaymentsEnabled,
    DateTimeOffset SyncedAtUtc);
