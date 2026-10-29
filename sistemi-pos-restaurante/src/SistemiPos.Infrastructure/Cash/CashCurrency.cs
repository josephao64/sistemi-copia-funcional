using Microsoft.EntityFrameworkCore;
using SistemiPos.Domain.Configuration;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Cash;

// Currency belongs to the company of the authorized branch; system currency is the legacy fallback.
public static class CashCurrency
{
    public static async Task<CurrencySnapshot> ResolveAsync(AppDbContext db, Guid? branchId, CancellationToken ct = default)
    {
        var system = await db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? SystemSetting.CreateDefault();
        if (!branchId.HasValue) return CurrencySnapshot.From(system);
        var company = await db.Branches.Where(x => x.Id == branchId).Select(x => x.CompanyId).SingleOrDefaultAsync(ct);
        var setting = await db.CompanySettings.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == company, ct);
        return setting is null ? CurrencySnapshot.From(system) : new(setting.CurrencyCode, setting.CurrencyName, setting.CurrencySymbol,
            setting.CurrencyDecimals, setting.DecimalSeparator, setting.ThousandsSeparator, setting.CurrencySymbolPosition.ToString());
    }
}
