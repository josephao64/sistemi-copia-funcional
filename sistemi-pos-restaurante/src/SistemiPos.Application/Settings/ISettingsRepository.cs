using SistemiPos.Domain.Configuration;

namespace SistemiPos.Application.Settings;

public interface ISettingsRepository
{
    Task<SystemSetting?> GetSystemSettingAsync(CancellationToken cancellationToken = default);
    Task AddSystemSettingAsync(SystemSetting setting, CancellationToken cancellationToken = default);
    Task<List<CurrencyDefinition>> GetCurrenciesAsync(CancellationToken cancellationToken = default);
    Task<CurrencyDefinition?> GetCurrencyAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddCurrencyAsync(CurrencyDefinition currency, CancellationToken cancellationToken = default);
    Task<List<BankAccount>> GetBankAccountsAsync(CancellationToken cancellationToken = default);
    Task<BankAccount?> GetBankAccountAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddBankAccountAsync(BankAccount bankAccount, CancellationToken cancellationToken = default);
    Task<List<RoleDiscountSetting>> GetRoleDiscountSettingsAsync(CancellationToken cancellationToken = default);
    Task<RoleDiscountSetting?> GetRoleDiscountSettingByRoleIdAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task AddRoleDiscountSettingAsync(RoleDiscountSetting setting, CancellationToken cancellationToken = default);
    Task<List<UserDiscountSetting>> GetUserDiscountSettingsAsync(CancellationToken cancellationToken = default);
    Task<UserDiscountSetting?> GetUserDiscountSettingByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddUserDiscountSettingAsync(UserDiscountSetting setting, CancellationToken cancellationToken = default);
}
