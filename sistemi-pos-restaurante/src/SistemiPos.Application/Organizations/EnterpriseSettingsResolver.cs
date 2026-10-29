using SistemiPos.Application.Settings;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Organizations;

namespace SistemiPos.Application.Organizations;

public sealed class EnterpriseSettingsResolver(
    IOrganizationRepository organizationRepository,
    ICompanySettingsRepository companySettingsRepository,
    IBranchSettingsRepository branchSettingsRepository,
    ISettingsRepository? systemSettingsRepository = null) : IEnterpriseSettingsResolver
{
    public async Task<EffectiveEnterpriseSettingsDto> ResolveEffectiveSettingsAsync(
        Guid companyId,
        Guid? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var company = await organizationRepository.GetCompanyByIdAsync(companyId, cancellationToken);
        if (company is null)
        {
            throw new DomainRuleException("La empresa especificada no existe.");
        }

        Branch? branch = null;
        if (branchId.HasValue && branchId.Value != Guid.Empty)
        {
            branch = await organizationRepository.GetBranchByIdAsync(branchId.Value, cancellationToken);
            if (branch is null)
            {
                throw new DomainRuleException("La sucursal especificada no existe.");
            }

            if (branch.CompanyId != companyId)
            {
                throw new DomainRuleException("La sucursal especificada no pertenece a la empresa.");
            }
        }

        var companySetting = await companySettingsRepository.GetByCompanyIdAsync(companyId, cancellationToken);
        SystemSetting? systemFallback = null;
        if (companySetting is null && systemSettingsRepository is not null)
        {
            systemFallback = await systemSettingsRepository.GetSystemSettingAsync(cancellationToken);
        }

        var baseOrigin = companySetting is not null ? SettingOrigin.Company : SettingOrigin.System;

        var businessName = companySetting?.BusinessName ?? systemFallback?.BusinessName ?? company.TradeName;
        var businessLegalName = companySetting?.BusinessLegalName ?? systemFallback?.BusinessLegalName ?? company.LegalName;
        var businessNit = companySetting?.BusinessNit ?? systemFallback?.BusinessNit ?? company.Nit;
        var businessAddress = companySetting?.BusinessAddress ?? systemFallback?.BusinessAddress ?? company.Address;
        var businessPhone = companySetting?.BusinessPhone ?? systemFallback?.BusinessPhone ?? company.Phone;
        var businessEmail = companySetting?.BusinessEmail ?? systemFallback?.BusinessEmail ?? company.Email;
        var businessLogoUrl = companySetting?.BusinessLogoUrl ?? systemFallback?.BusinessLogoUrl;

        var currencyCode = companySetting?.CurrencyCode ?? systemFallback?.CurrencyCode ?? "GTQ";
        var currencyId = companySetting?.CurrencyId ?? systemFallback?.CurrencyId;
        var currencyName = companySetting?.CurrencyName ?? systemFallback?.CurrencyName ?? "Quetzal";
        var currencySymbol = companySetting?.CurrencySymbol ?? systemFallback?.CurrencySymbol ?? "Q";
        var currencyDecimals = companySetting?.CurrencyDecimals ?? systemFallback?.CurrencyDecimals ?? 2;
        var decimalSeparator = companySetting?.DecimalSeparator ?? systemFallback?.DecimalSeparator ?? ".";
        var thousandsSeparator = companySetting?.ThousandsSeparator ?? systemFallback?.ThousandsSeparator ?? ",";
        var currencySymbolPosition = companySetting?.CurrencySymbolPosition ?? systemFallback?.CurrencySymbolPosition ?? CurrencySymbolPosition.Before;

        var taxPercent = companySetting?.TaxPercent ?? systemFallback?.TaxPercent ?? 12m;
        var showTaxOnReceipt = companySetting?.ShowTaxOnReceipt ?? systemFallback?.ShowTaxOnReceipt ?? true;
        var receiptFooter = companySetting?.ReceiptFooter ?? systemFallback?.ReceiptFooter ?? "¡Gracias por su compra!";

        var cashPayments = companySetting?.CashPaymentsEnabled ?? systemFallback?.CashPaymentsEnabled ?? true;
        var transferPayments = companySetting?.TransferPaymentsEnabled ?? systemFallback?.TransferPaymentsEnabled ?? true;
        var posPayments = companySetting?.PosPaymentsEnabled ?? systemFallback?.PosPaymentsEnabled ?? true;
        var creditPayments = companySetting?.CreditPaymentsEnabled ?? systemFallback?.CreditPaymentsEnabled ?? true;

        var discountsEnabled = companySetting?.DiscountsEnabled ?? systemFallback?.DiscountsEnabled ?? false;
        var defaultMaxDiscountPercent = companySetting?.DefaultMaxDiscountPercent ?? systemFallback?.DefaultMaxDiscountPercent ?? 0m;
        var nitLookupProvider = companySetting?.NitLookupProvider ?? systemFallback?.NitLookupProvider ?? NitLookupProviderType.None;

        var felEnabled = companySetting?.FelEnabled ?? systemFallback?.FelEnabled ?? false;
        var felEnvironment = companySetting?.FelEnvironment ?? systemFallback?.FelEnvironment ?? FelEnvironmentType.Testing;
        var felProvider = companySetting?.FelProvider ?? systemFallback?.FelProvider ?? "INFILE";
        var defaultFelDocumentType = companySetting?.DefaultFelDocumentType ?? systemFallback?.DefaultFelDocumentType ?? "FACT";
        var felTaxPhrases = companySetting?.FelTaxPhrases ?? systemFallback?.FelTaxPhrases;
        var hasInfileCredentials = !string.IsNullOrWhiteSpace(companySetting?.InfileApiKeyProtected ?? systemFallback?.InfileApiKeyProtected);

        BranchSetting? branchSetting = null;
        if (branch is not null)
        {
            branchSetting = await branchSettingsRepository.GetByBranchIdAsync(branch.Id, cancellationToken);
        }

        EffectiveValue<string?> effEstablishmentCode;
        if (!string.IsNullOrWhiteSpace(branchSetting?.EstablishmentCode))
        {
            effEstablishmentCode = new(branchSetting.EstablishmentCode, SettingOrigin.Branch);
        }
        else if (!string.IsNullOrWhiteSpace(branch?.EstablishmentCode))
        {
            effEstablishmentCode = new(branch.EstablishmentCode, SettingOrigin.Branch);
        }
        else if (systemFallback is not null && !string.IsNullOrWhiteSpace(systemFallback.EstablishmentCode))
        {
            effEstablishmentCode = new(systemFallback.EstablishmentCode, SettingOrigin.System);
        }
        else
        {
            effEstablishmentCode = new(null, baseOrigin);
        }

        EffectiveValue<string?> effEstablishmentName;
        if (!string.IsNullOrWhiteSpace(branchSetting?.EstablishmentName))
        {
            effEstablishmentName = new(branchSetting.EstablishmentName, SettingOrigin.Branch);
        }
        else if (branch is not null)
        {
            effEstablishmentName = new(branch.Name, SettingOrigin.Branch);
        }
        else
        {
            effEstablishmentName = new(businessName, baseOrigin);
        }

        EffectiveValue<string?> effReceiptFooter;
        if (branchSetting is not null && !string.IsNullOrWhiteSpace(branchSetting.ReceiptFooter))
        {
            effReceiptFooter = new(branchSetting.ReceiptFooter, SettingOrigin.Branch);
        }
        else
        {
            effReceiptFooter = new(receiptFooter, baseOrigin);
        }

        EffectiveValue<bool> effCash;
        EffectiveValue<bool> effTransfer;
        EffectiveValue<bool> effPos;
        EffectiveValue<bool> effCredit;

        if (branchSetting is not null && branchSetting.OverridePaymentMethods)
        {
            effCash = new(branchSetting.CashPaymentsEnabled, SettingOrigin.Branch);
            effTransfer = new(branchSetting.TransferPaymentsEnabled, SettingOrigin.Branch);
            effPos = new(branchSetting.PosPaymentsEnabled, SettingOrigin.Branch);
            effCredit = new(branchSetting.CreditPaymentsEnabled, SettingOrigin.Branch);
        }
        else
        {
            effCash = new(cashPayments, baseOrigin);
            effTransfer = new(transferPayments, baseOrigin);
            effPos = new(posPayments, baseOrigin);
            effCredit = new(creditPayments, baseOrigin);
        }

        EffectiveValue<string?> effAddress;
        if (branch is not null && !string.IsNullOrWhiteSpace(branch.Address))
        {
            effAddress = new(branch.Address, SettingOrigin.Branch);
        }
        else
        {
            effAddress = new(businessAddress, baseOrigin);
        }

        EffectiveValue<string?> effPhone;
        if (branch is not null && !string.IsNullOrWhiteSpace(branch.Phone))
        {
            effPhone = new(branch.Phone, SettingOrigin.Branch);
        }
        else
        {
            effPhone = new(businessPhone, baseOrigin);
        }

        return new EffectiveEnterpriseSettingsDto
        {
            CompanyId = companyId,
            BranchId = branch?.Id,
            BusinessName = new(businessName, baseOrigin),
            BusinessLegalName = new(businessLegalName, baseOrigin),
            BusinessNit = new(businessNit, baseOrigin),
            BusinessAddress = effAddress,
            BusinessPhone = effPhone,
            BusinessEmail = new(businessEmail, baseOrigin),
            BusinessLogoUrl = new(businessLogoUrl, baseOrigin),
            EstablishmentCode = effEstablishmentCode,
            EstablishmentName = effEstablishmentName,
            ReceiptFooter = effReceiptFooter,
            CurrencyCode = new(currencyCode, baseOrigin),
            CurrencyId = new(currencyId, baseOrigin),
            CurrencyName = new(currencyName, baseOrigin),
            CurrencySymbol = new(currencySymbol, baseOrigin),
            CurrencyDecimals = new(currencyDecimals, baseOrigin),
            DecimalSeparator = new(decimalSeparator, baseOrigin),
            ThousandsSeparator = new(thousandsSeparator, baseOrigin),
            CurrencySymbolPosition = new(currencySymbolPosition, baseOrigin),
            TaxPercent = new(taxPercent, baseOrigin),
            ShowTaxOnReceipt = new(showTaxOnReceipt, baseOrigin),
            CashPaymentsEnabled = effCash,
            TransferPaymentsEnabled = effTransfer,
            PosPaymentsEnabled = effPos,
            CreditPaymentsEnabled = effCredit,
            DiscountsEnabled = new(discountsEnabled, baseOrigin),
            DefaultMaxDiscountPercent = new(defaultMaxDiscountPercent, baseOrigin),
            NitLookupProvider = new(nitLookupProvider, baseOrigin),
            FelEnabled = new(felEnabled, baseOrigin),
            FelEnvironment = new(felEnvironment, baseOrigin),
            FelProvider = new(felProvider, baseOrigin),
            DefaultFelDocumentType = new(defaultFelDocumentType, baseOrigin),
            FelTaxPhrases = new(felTaxPhrases, baseOrigin),
            HasInfileCredentials = hasInfileCredentials
        };
    }

    public async Task<BranchSettingDto?> GetBranchSettingAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        var setting = await branchSettingsRepository.GetByBranchIdAsync(branchId, cancellationToken);
        if (setting is null) return null;

        return new BranchSettingDto(
            setting.BranchId,
            setting.EstablishmentCode,
            setting.EstablishmentName,
            setting.ReceiptFooter,
            setting.OverridePaymentMethods,
            setting.CashPaymentsEnabled,
            setting.TransferPaymentsEnabled,
            setting.PosPaymentsEnabled,
            setting.CreditPaymentsEnabled);
    }

    public async Task SaveBranchSettingAsync(Guid companyId, Guid branchId, SaveBranchSettingRequest request, CancellationToken cancellationToken = default)
    {
        var branch = await organizationRepository.GetBranchByIdAsync(branchId, cancellationToken);
        if (branch is null)
        {
            throw new DomainRuleException("La sucursal especificada no existe.");
        }

        if (branch.CompanyId != companyId)
        {
            throw new DomainRuleException("La sucursal especificada no pertenece a la empresa.");
        }

        var setting = await branchSettingsRepository.GetByBranchIdAsync(branchId, cancellationToken);
        if (setting is null)
        {
            setting = new BranchSetting(
                branchId,
                request.EstablishmentCode,
                request.EstablishmentName,
                request.ReceiptFooter,
                request.OverridePaymentMethods,
                request.CashPaymentsEnabled,
                request.TransferPaymentsEnabled,
                request.PosPaymentsEnabled,
                request.CreditPaymentsEnabled);
            await branchSettingsRepository.AddAsync(setting, cancellationToken);
        }
        else
        {
            setting.Update(
                request.EstablishmentCode,
                request.EstablishmentName,
                request.ReceiptFooter,
                request.OverridePaymentMethods,
                request.CashPaymentsEnabled,
                request.TransferPaymentsEnabled,
                request.PosPaymentsEnabled,
                request.CreditPaymentsEnabled);
            await branchSettingsRepository.UpdateAsync(setting, cancellationToken);
        }
    }
}
