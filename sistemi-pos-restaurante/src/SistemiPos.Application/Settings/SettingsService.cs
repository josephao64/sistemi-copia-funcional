using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Common.Exceptions;
using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Application.Settings;

public sealed class SettingsService(ISettingsRepository repository, IUnitOfWork unitOfWork) : ISettingsService
{
    public async Task<SystemSettingDto> GetSystemSettingAsync(CancellationToken cancellationToken = default)
    {
        var setting = await EnsureSystemSettingAsync(cancellationToken);
        return Map(setting);
    }

    public async Task UpdateSystemSettingAsync(UpdateSystemSettingRequest request, CancellationToken cancellationToken = default)
    {
        var setting = await EnsureSystemSettingAsync(cancellationToken);
        var currency = request.CurrencyId.HasValue ? await repository.GetCurrencyAsync(request.CurrencyId.Value, cancellationToken) ?? throw new DomainRuleException("La moneda seleccionada no existe o está inactiva.") : null;
        setting.Update(
            request.BusinessName,
            request.BusinessNit,
            request.BusinessAddress,
            request.BusinessPhone,
            request.BusinessLogoUrl,
            request.CurrencyCode,
            request.CurrencyName,
            request.CurrencySymbol,
            request.CurrencyDecimals,
            request.DecimalSeparator,
            request.ThousandsSeparator,
            request.CurrencySymbolPosition,
            request.TaxPercent,
            request.ShowTaxOnReceipt,
            request.ReceiptFooter,
            true,
            request.TransferPaymentsEnabled,
            request.PosPaymentsEnabled,
            request.DiscountsEnabled,
            request.DefaultMaxDiscountPercent,
            request.NitLookupProvider,
            request.ProductsWithoutMovementDays,
            request.BusinessEmail,
            request.BusinessLegalName,
            request.EstablishmentName,
            request.EstablishmentCode,
            request.FelEnabled,
            request.FelEnvironment,
            request.DefaultFelDocumentType,
            request.FelTaxPhrases,
            request.InfileApiUserProtected,
            request.InfileApiKeyProtected,
            request.InfileSignatureUserProtected,
            request.InfileSignatureKeyProtected,
            request.CreditPaymentsEnabled);
        if (currency is not null) setting.SelectCurrency(currency);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CurrencyDefinition>> GetCurrenciesAsync(CancellationToken cancellationToken = default) => await repository.GetCurrenciesAsync(cancellationToken);
    public async Task<Guid> CreateCurrencyAsync(string code, string name, string symbol, int decimals, string decimalSeparator, string thousandsSeparator, CurrencySymbolPosition symbolPosition, CancellationToken cancellationToken = default)
    {
        var currency = new CurrencyDefinition(code, name, symbol, decimals, decimalSeparator, thousandsSeparator, symbolPosition);
        await repository.AddCurrencyAsync(currency, cancellationToken); await unitOfWork.SaveChangesAsync(cancellationToken); return currency.Id;
    }

    public async Task<IReadOnlyList<BankAccountDto>> GetBankAccountsAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await repository.GetBankAccountsAsync(cancellationToken);
        return accounts.OrderBy(x => x.Bank).ThenBy(x => x.AccountName).Select(Map).ToList();
    }

    public async Task<BankAccountDto?> GetBankAccountAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await repository.GetBankAccountAsync(id, cancellationToken);
        return account is null ? null : Map(account);
    }

    public async Task<Guid> CreateBankAccountAsync(SaveBankAccountRequest request, CancellationToken cancellationToken = default)
    {
        var account = new BankAccount(request.Bank, request.AccountName, request.AccountNumber);
        account.Update(request.Bank, request.AccountName, request.AccountNumber, request.IsActive);
        await repository.AddBankAccountAsync(account, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return account.Id;
    }

    public async Task UpdateBankAccountAsync(Guid id, SaveBankAccountRequest request, CancellationToken cancellationToken = default)
    {
        var account = await repository.GetBankAccountAsync(id, cancellationToken)
            ?? throw new NotFoundException("Cuenta bancaria no encontrada.");
        account.Update(request.Bank, request.AccountName, request.AccountNumber, request.IsActive);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoleDiscountSettingDto>> GetRoleDiscountSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await repository.GetRoleDiscountSettingsAsync(cancellationToken);
        return settings.Select(x => new RoleDiscountSettingDto(x.Id, x.RoleId, x.MaxDiscountPercent)).ToList();
    }

    public async Task SaveRoleDiscountSettingAsync(SaveRoleDiscountSettingRequest request, CancellationToken cancellationToken = default)
    {
        var setting = await repository.GetRoleDiscountSettingByRoleIdAsync(request.RoleId, cancellationToken);
        if (setting is null)
        {
            setting = new RoleDiscountSetting(request.RoleId, request.MaxDiscountPercent);
            await repository.AddRoleDiscountSettingAsync(setting, cancellationToken);
        }
        else
        {
            setting.Update(request.RoleId, request.MaxDiscountPercent);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserDiscountSettingDto>> GetUserDiscountSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await repository.GetUserDiscountSettingsAsync(cancellationToken);
        return settings.Select(x => new UserDiscountSettingDto(x.Id, x.UserId, x.MaxDiscountPercent)).ToList();
    }

    public async Task SaveUserDiscountSettingAsync(SaveUserDiscountSettingRequest request, CancellationToken cancellationToken = default)
    {
        var setting = await repository.GetUserDiscountSettingByUserIdAsync(request.UserId, cancellationToken);
        if (setting is null)
        {
            setting = new UserDiscountSetting(request.UserId, request.MaxDiscountPercent);
            await repository.AddUserDiscountSettingAsync(setting, cancellationToken);
        }
        else
        {
            setting.Update(request.UserId, request.MaxDiscountPercent);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<SystemSetting> EnsureSystemSettingAsync(CancellationToken cancellationToken)
    {
        var setting = await repository.GetSystemSettingAsync(cancellationToken);
        if (setting is not null)
        {
            return setting;
        }

        setting = SystemSetting.CreateDefault();
        await repository.AddSystemSettingAsync(setting, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return setting;
    }

    private static SystemSettingDto Map(SystemSetting setting) =>
        new(
            setting.Id,
            setting.BusinessName,
            setting.BusinessNit,
            setting.BusinessAddress,
            setting.BusinessPhone,
            setting.BusinessLogoUrl,
            setting.CurrencyCode,
            setting.CurrencyName,
            setting.CurrencySymbol,
            setting.CurrencyDecimals,
            setting.DecimalSeparator,
            setting.ThousandsSeparator,
            setting.CurrencySymbolPosition,
            setting.TaxPercent,
            setting.ShowTaxOnReceipt,
            setting.ReceiptFooter,
            true,
            setting.TransferPaymentsEnabled,
            setting.PosPaymentsEnabled,
            setting.CreditPaymentsEnabled,
            setting.DiscountsEnabled,
            setting.DefaultMaxDiscountPercent,
            setting.NitLookupProvider,
            setting.ProductsWithoutMovementDays,
            setting.CurrencyId,
            setting.BusinessEmail,
            setting.BusinessLegalName,
            setting.EstablishmentName,
            setting.EstablishmentCode,
            setting.FelEnabled,
            setting.FelEnvironment,
            setting.FelProvider,
            setting.DefaultFelDocumentType,
            setting.FelTaxPhrases,
            setting.InfileApiUserProtected,
            setting.InfileApiKeyProtected,
            setting.InfileSignatureUserProtected,
            setting.InfileSignatureKeyProtected);

    private static BankAccountDto Map(BankAccount account) =>
        new(account.Id, account.Bank, account.AccountName, account.AccountNumber, account.IsActive);
}
