using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Configuration;

public sealed class SystemSetting : AuditableEntity
{
    public string BusinessName { get; private set; } = "MISCELÁNEA N&C";
    public string? BusinessLegalName { get; private set; }
    public string? BusinessNit { get; private set; } = "101085257";
    public string? BusinessAddress { get; private set; } = "CALLE TRANSITO ROJAS 7-72 zona 6, JALAPA, JALAPA";
    public string? BusinessPhone { get; private set; }
    public string? BusinessEmail { get; private set; }
    public string? BusinessLogoUrl { get; private set; } = "/images/business/miscelanea-nc-logo.jpg";
    public string CurrencyCode { get; private set; } = "GTQ";
    public Guid? CurrencyId { get; private set; }
    public string CurrencyName { get; private set; } = "Quetzal";
    public string CurrencySymbol { get; private set; } = "Q";
    public int CurrencyDecimals { get; private set; } = 2;
    public string DecimalSeparator { get; private set; } = ".";
    public string ThousandsSeparator { get; private set; } = ",";
    public CurrencySymbolPosition CurrencySymbolPosition { get; private set; } = CurrencySymbolPosition.Before;
    public decimal TaxPercent { get; private set; } = 12m;
    public bool ShowTaxOnReceipt { get; private set; } = true;
    public string? ReceiptFooter { get; private set; } = "¡Gracias por su compra!";
    public bool CashPaymentsEnabled { get; private set; } = true;
    public bool TransferPaymentsEnabled { get; private set; } = true;
    public bool PosPaymentsEnabled { get; private set; } = true;
    public bool CreditPaymentsEnabled { get; private set; } = true;
    public bool DiscountsEnabled { get; private set; }
    public decimal DefaultMaxDiscountPercent { get; private set; }
    public NitLookupProviderType NitLookupProvider { get; private set; } = NitLookupProviderType.None;
    public int ProductsWithoutMovementDays { get; private set; } = 30;
    public string? EstablishmentName { get; private set; }
    public string? EstablishmentCode { get; private set; }
    public bool FelEnabled { get; private set; }
    public FelEnvironmentType FelEnvironment { get; private set; } = FelEnvironmentType.Testing;
    public string FelProvider { get; private set; } = "INFILE";
    public string DefaultFelDocumentType { get; private set; } = "FACT";
    public string? FelTaxPhrases { get; private set; }
    public string? InfileApiUserProtected { get; private set; }
    public string? InfileApiKeyProtected { get; private set; }
    public string? InfileSignatureUserProtected { get; private set; }
    public string? InfileSignatureKeyProtected { get; private set; }

    private SystemSetting()
    {
    }

    public static SystemSetting CreateDefault() => new();
    public SystemSetting WithOperationalCurrency(CurrencySnapshot currency)
    {
        var result = (SystemSetting)MemberwiseClone();
        result.CurrencyCode = currency.Code; result.CurrencyName = currency.Name; result.CurrencySymbol = currency.Symbol;
        result.CurrencyDecimals = currency.Decimals; result.DecimalSeparator = currency.DecimalSeparator;
        result.ThousandsSeparator = currency.ThousandsSeparator;
        result.CurrencySymbolPosition = Enum.Parse<CurrencySymbolPosition>(currency.SymbolPosition);
        return result;
    }
    public void SelectCurrency(CurrencyDefinition currency) { CurrencyId = currency.Id; CurrencyCode = currency.Code; CurrencyName = currency.Name; CurrencySymbol = currency.Symbol; CurrencyDecimals = currency.Decimals; DecimalSeparator = currency.DecimalSeparator; ThousandsSeparator = currency.ThousandsSeparator; CurrencySymbolPosition = currency.SymbolPosition; }

    public void Update(
        string businessName,
        string? businessNit,
        string? businessAddress,
        string? businessPhone,
        string? businessLogoUrl,
        string currencyCode,
        string currencyName,
        string currencySymbol,
        int currencyDecimals,
        string decimalSeparator,
        string thousandsSeparator,
        CurrencySymbolPosition currencySymbolPosition,
        decimal taxPercent,
        bool showTaxOnReceipt,
        string? receiptFooter,
        bool cashPaymentsEnabled,
        bool transferPaymentsEnabled,
        bool posPaymentsEnabled,
        bool discountsEnabled,
        decimal defaultMaxDiscountPercent,
        NitLookupProviderType nitLookupProvider = NitLookupProviderType.None,
        int productsWithoutMovementDays = 30,
        string? businessEmail = null,
        string? businessLegalName = null,
        string? establishmentName = null,
        string? establishmentCode = null,
        bool felEnabled = false,
        FelEnvironmentType felEnvironment = FelEnvironmentType.Testing,
        string defaultFelDocumentType = "FACT",
        string? felTaxPhrases = null,
        string? infileApiUserProtected = null,
        string? infileApiKeyProtected = null,
        string? infileSignatureUserProtected = null,
        string? infileSignatureKeyProtected = null,
        bool? creditPaymentsEnabled = null)
    {
        if (string.IsNullOrWhiteSpace(businessName))
        {
            throw new DomainRuleException("El nombre del negocio es requerido.");
        }

        if (string.IsNullOrWhiteSpace(currencyCode) || string.IsNullOrWhiteSpace(currencyName) || string.IsNullOrWhiteSpace(currencySymbol))
        {
            throw new DomainRuleException("La configuración de moneda es requerida.");
        }

        if (string.IsNullOrWhiteSpace(decimalSeparator) || string.IsNullOrWhiteSpace(thousandsSeparator))
        {
            throw new DomainRuleException("Los separadores de moneda son requeridos.");
        }

        if (decimalSeparator == thousandsSeparator)
        {
            throw new DomainRuleException("Los separadores decimal y de miles deben ser distintos.");
        }

        if (currencyDecimals < 0 || currencyDecimals > 6)
        {
            throw new DomainRuleException("La cantidad de decimales de moneda no es válida.");
        }

        if (taxPercent < 0 || defaultMaxDiscountPercent < 0)
        {
            throw new DomainRuleException("Los porcentajes no pueden ser negativos.");
        }

        if (productsWithoutMovementDays <= 0)
        {
            throw new DomainRuleException("Los días para considerar un producto sin movimiento deben ser mayores a cero.");
        }

        if (felEnabled && (string.IsNullOrWhiteSpace(businessLegalName)
            || string.IsNullOrWhiteSpace(businessNit)
            || string.IsNullOrWhiteSpace(establishmentName)
            || string.IsNullOrWhiteSpace(establishmentCode)))
        {
            throw new DomainRuleException("Para activar FEL complete la razón social, NIT, nombre y código del establecimiento.");
        }

        if (felEnabled && (string.IsNullOrWhiteSpace(infileApiUserProtected)
            || string.IsNullOrWhiteSpace(infileApiKeyProtected)
            || string.IsNullOrWhiteSpace(infileSignatureUserProtected)
            || string.IsNullOrWhiteSpace(infileSignatureKeyProtected)))
        {
            throw new DomainRuleException("Para activar FEL configure todas las credenciales de INFILE.");
        }

        BusinessName = NameNormalizer.Normalize(businessName);
        BusinessLegalName = string.IsNullOrWhiteSpace(businessLegalName) ? null : NameNormalizer.Normalize(businessLegalName);
        BusinessNit = string.IsNullOrWhiteSpace(businessNit) ? null : businessNit.Trim();
        BusinessAddress = string.IsNullOrWhiteSpace(businessAddress) ? null : businessAddress.Trim();
        BusinessPhone = string.IsNullOrWhiteSpace(businessPhone) ? null : businessPhone.Trim();
        BusinessEmail = string.IsNullOrWhiteSpace(businessEmail) ? null : businessEmail.Trim();
        BusinessLogoUrl = string.IsNullOrWhiteSpace(businessLogoUrl) ? null : businessLogoUrl.Trim();
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        CurrencyName = NameNormalizer.Normalize(currencyName);
        CurrencySymbol = currencySymbol.Trim();
        CurrencyDecimals = currencyDecimals;
        DecimalSeparator = decimalSeparator;
        ThousandsSeparator = thousandsSeparator;
        CurrencySymbolPosition = currencySymbolPosition;
        TaxPercent = decimal.Round(taxPercent, 2);
        ShowTaxOnReceipt = showTaxOnReceipt;
        ReceiptFooter = string.IsNullOrWhiteSpace(receiptFooter) ? null : receiptFooter.Trim();
        CashPaymentsEnabled = true;
        TransferPaymentsEnabled = transferPaymentsEnabled;
        PosPaymentsEnabled = posPaymentsEnabled;
        CreditPaymentsEnabled = creditPaymentsEnabled ?? CreditPaymentsEnabled;
        DiscountsEnabled = discountsEnabled;
        DefaultMaxDiscountPercent = decimal.Round(defaultMaxDiscountPercent, 2);
        NitLookupProvider = nitLookupProvider;
        ProductsWithoutMovementDays = productsWithoutMovementDays;
        EstablishmentName = string.IsNullOrWhiteSpace(establishmentName) ? null : NameNormalizer.Normalize(establishmentName);
        EstablishmentCode = string.IsNullOrWhiteSpace(establishmentCode) ? null : establishmentCode.Trim();
        FelEnabled = felEnabled;
        FelEnvironment = felEnvironment;
        FelProvider = "INFILE";
        DefaultFelDocumentType = string.IsNullOrWhiteSpace(defaultFelDocumentType) ? "FACT" : defaultFelDocumentType.Trim().ToUpperInvariant();
        FelTaxPhrases = string.IsNullOrWhiteSpace(felTaxPhrases) ? null : felTaxPhrases.Trim();
        InfileApiUserProtected = string.IsNullOrWhiteSpace(infileApiUserProtected) ? null : infileApiUserProtected;
        InfileApiKeyProtected = string.IsNullOrWhiteSpace(infileApiKeyProtected) ? null : infileApiKeyProtected;
        InfileSignatureUserProtected = string.IsNullOrWhiteSpace(infileSignatureUserProtected) ? null : infileSignatureUserProtected;
        InfileSignatureKeyProtected = string.IsNullOrWhiteSpace(infileSignatureKeyProtected) ? null : infileSignatureKeyProtected;
    }

    public void ConfigurePaymentMethods(bool transferPaymentsEnabled, bool posPaymentsEnabled, bool creditPaymentsEnabled)
    {
        CashPaymentsEnabled = true;
        TransferPaymentsEnabled = transferPaymentsEnabled;
        PosPaymentsEnabled = posPaymentsEnabled;
        CreditPaymentsEnabled = creditPaymentsEnabled;
    }
}
