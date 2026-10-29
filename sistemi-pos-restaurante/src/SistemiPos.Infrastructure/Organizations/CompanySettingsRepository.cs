using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Organizations;
using SistemiPos.Domain.Organizations;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Organizations;

public sealed class CompanySettingsRepository(AppDbContext dbContext) : ICompanySettingsRepository
{
    public Task<CompanySetting?> GetByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        dbContext.CompanySettings
            .Include(x => x.Company)
            .SingleOrDefaultAsync(x => x.CompanyId == companyId, cancellationToken);

    public async Task AddAsync(CompanySetting setting, CancellationToken cancellationToken = default)
    {
        await dbContext.CompanySettings.AddAsync(setting, cancellationToken);
    }

    public Task UpdateAsync(CompanySetting setting, CancellationToken cancellationToken = default)
    {
        dbContext.CompanySettings.Update(setting);
        return Task.CompletedTask;
    }
}
