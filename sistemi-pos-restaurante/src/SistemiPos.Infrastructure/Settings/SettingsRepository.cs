using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Settings;
using SistemiPos.Domain.Configuration;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Settings;

public sealed class SettingsRepository(AppDbContext dbContext) : ISettingsRepository
{
    private SystemSetting? _systemSetting;
    private bool _systemSettingLoaded;

    public async Task<SystemSetting?> GetSystemSettingAsync(CancellationToken cancellationToken = default)
    {
        if (_systemSettingLoaded) return _systemSetting;
        _systemSetting = await dbContext.SystemSettings.FirstOrDefaultAsync(cancellationToken);
        _systemSettingLoaded = true;
        return _systemSetting;
    }

    public async Task AddSystemSettingAsync(SystemSetting setting, CancellationToken cancellationToken = default)
    {
        await dbContext.SystemSettings.AddAsync(setting, cancellationToken);
        _systemSetting = setting;
        _systemSettingLoaded = true;
    }
    public Task<List<CurrencyDefinition>> GetCurrenciesAsync(CancellationToken cancellationToken = default) => dbContext.Currencies.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(cancellationToken);
    public Task<CurrencyDefinition?> GetCurrencyAsync(Guid id, CancellationToken cancellationToken = default) => dbContext.Currencies.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken);
    public Task AddCurrencyAsync(CurrencyDefinition currency, CancellationToken cancellationToken = default) => dbContext.Currencies.AddAsync(currency, cancellationToken).AsTask();

    public Task<List<BankAccount>> GetBankAccountsAsync(CancellationToken cancellationToken = default) =>
        dbContext.BankAccounts.AsNoTracking().ToListAsync(cancellationToken);

    public Task<BankAccount?> GetBankAccountAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.BankAccounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task AddBankAccountAsync(BankAccount bankAccount, CancellationToken cancellationToken = default) =>
        dbContext.BankAccounts.AddAsync(bankAccount, cancellationToken).AsTask();

    public Task<List<RoleDiscountSetting>> GetRoleDiscountSettingsAsync(CancellationToken cancellationToken = default) =>
        dbContext.RoleDiscountSettings.AsNoTracking().ToListAsync(cancellationToken);

    public Task<RoleDiscountSetting?> GetRoleDiscountSettingByRoleIdAsync(Guid roleId, CancellationToken cancellationToken = default) =>
        dbContext.RoleDiscountSettings.FirstOrDefaultAsync(x => x.RoleId == roleId, cancellationToken);

    public Task AddRoleDiscountSettingAsync(RoleDiscountSetting setting, CancellationToken cancellationToken = default) =>
        dbContext.RoleDiscountSettings.AddAsync(setting, cancellationToken).AsTask();

    public Task<List<UserDiscountSetting>> GetUserDiscountSettingsAsync(CancellationToken cancellationToken = default) =>
        dbContext.UserDiscountSettings.AsNoTracking().ToListAsync(cancellationToken);

    public Task<UserDiscountSetting?> GetUserDiscountSettingByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.UserDiscountSettings.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    public Task AddUserDiscountSettingAsync(UserDiscountSetting setting, CancellationToken cancellationToken = default) =>
        dbContext.UserDiscountSettings.AddAsync(setting, cancellationToken).AsTask();
}
