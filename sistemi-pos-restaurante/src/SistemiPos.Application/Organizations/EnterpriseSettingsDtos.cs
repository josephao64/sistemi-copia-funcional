using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Organizations;

namespace SistemiPos.Application.Organizations;

public sealed record EffectiveValue<T>(T Value, SettingOrigin Origin)
{
    public static implicit operator T(EffectiveValue<T> effective) => effective.Value;
}

public sealed class EffectiveEnterpriseSettingsDto
{
    public Guid CompanyId { get; set; }
    public Guid? BranchId { get; set; }

    public EffectiveValue<string> BusinessName { get; set; } = null!;
    public EffectiveValue<string?> BusinessLegalName { get; set; } = null!;
    public EffectiveValue<string?> BusinessNit { get; set; } = null!;
    public EffectiveValue<string?> BusinessAddress { get; set; } = null!;
    public EffectiveValue<string?> BusinessPhone { get; set; } = null!;
    public EffectiveValue<string?> BusinessEmail { get; set; } = null!;
    public EffectiveValue<string?> BusinessLogoUrl { get; set; } = null!;

    public EffectiveValue<string?> EstablishmentCode { get; set; } = null!;
    public EffectiveValue<string?> EstablishmentName { get; set; } = null!;
    public EffectiveValue<string?> ReceiptFooter { get; set; } = null!;

    public EffectiveValue<string> CurrencyCode { get; set; } = null!;
    public EffectiveValue<Guid?> CurrencyId { get; set; } = null!;
    public EffectiveValue<string> CurrencyName { get; set; } = null!;
    public EffectiveValue<string> CurrencySymbol { get; set; } = null!;
    public EffectiveValue<int> CurrencyDecimals { get; set; } = null!;
    public EffectiveValue<string> DecimalSeparator { get; set; } = null!;
    public EffectiveValue<string> ThousandsSeparator { get; set; } = null!;
    public EffectiveValue<CurrencySymbolPosition> CurrencySymbolPosition { get; set; } = null!;

    public EffectiveValue<decimal> TaxPercent { get; set; } = null!;
    public EffectiveValue<bool> ShowTaxOnReceipt { get; set; } = null!;

    public EffectiveValue<bool> CashPaymentsEnabled { get; set; } = null!;
    public EffectiveValue<bool> TransferPaymentsEnabled { get; set; } = null!;
    public EffectiveValue<bool> PosPaymentsEnabled { get; set; } = null!;
    public EffectiveValue<bool> CreditPaymentsEnabled { get; set; } = null!;

    public EffectiveValue<bool> DiscountsEnabled { get; set; } = null!;
    public EffectiveValue<decimal> DefaultMaxDiscountPercent { get; set; } = null!;
    public EffectiveValue<NitLookupProviderType> NitLookupProvider { get; set; } = null!;

    public EffectiveValue<bool> FelEnabled { get; set; } = null!;
    public EffectiveValue<FelEnvironmentType> FelEnvironment { get; set; } = null!;
    public EffectiveValue<string> FelProvider { get; set; } = null!;
    public EffectiveValue<string> DefaultFelDocumentType { get; set; } = null!;
    public EffectiveValue<string?> FelTaxPhrases { get; set; } = null!;
    public bool HasInfileCredentials { get; set; }
}

public sealed record BranchSettingDto(
    Guid BranchId,
    string? EstablishmentCode,
    string? EstablishmentName,
    string? ReceiptFooter,
    bool OverridePaymentMethods,
    bool CashPaymentsEnabled,
    bool TransferPaymentsEnabled,
    bool PosPaymentsEnabled,
    bool CreditPaymentsEnabled);

public sealed record SaveBranchSettingRequest(
    string? EstablishmentCode,
    string? EstablishmentName,
    string? ReceiptFooter,
    bool OverridePaymentMethods,
    bool CashPaymentsEnabled,
    bool TransferPaymentsEnabled,
    bool PosPaymentsEnabled,
    bool CreditPaymentsEnabled);
