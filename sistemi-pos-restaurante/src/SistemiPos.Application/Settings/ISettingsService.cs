using SistemiPos.Domain.Configuration;

namespace SistemiPos.Application.Settings;

public interface ISettingsService
{
    Task<SystemSettingDto> GetSystemSettingAsync(CancellationToken cancellationToken = default);
    Task UpdateSystemSettingAsync(UpdateSystemSettingRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CurrencyDefinition>> GetCurrenciesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CurrencyDefinition>>([]);
    Task<Guid> CreateCurrencyAsync(string code, string name, string symbol, int decimals, string decimalSeparator, string thousandsSeparator, CurrencySymbolPosition symbolPosition, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    Task<IReadOnlyList<BankAccountDto>> GetBankAccountsAsync(CancellationToken cancellationToken = default);
    Task<BankAccountDto?> GetBankAccountAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateBankAccountAsync(SaveBankAccountRequest request, CancellationToken cancellationToken = default);
    Task UpdateBankAccountAsync(Guid id, SaveBankAccountRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleDiscountSettingDto>> GetRoleDiscountSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveRoleDiscountSettingAsync(SaveRoleDiscountSettingRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserDiscountSettingDto>> GetUserDiscountSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveUserDiscountSettingAsync(SaveUserDiscountSettingRequest request, CancellationToken cancellationToken = default);
}
