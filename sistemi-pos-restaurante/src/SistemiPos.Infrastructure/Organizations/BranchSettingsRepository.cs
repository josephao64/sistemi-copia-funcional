using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Organizations;
using SistemiPos.Domain.Organizations;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Organizations;

public sealed class BranchSettingsRepository(AppDbContext dbContext) : IBranchSettingsRepository
{
    public Task<BranchSetting?> GetByBranchIdAsync(Guid branchId, CancellationToken cancellationToken = default) =>
        dbContext.BranchSettings
            .Include(x => x.Branch)
            .SingleOrDefaultAsync(x => x.BranchId == branchId, cancellationToken);

    public async Task AddAsync(BranchSetting setting, CancellationToken cancellationToken = default)
    {
        await dbContext.BranchSettings.AddAsync(setting, cancellationToken);
    }

    public Task UpdateAsync(BranchSetting setting, CancellationToken cancellationToken = default)
    {
        dbContext.BranchSettings.Update(setting);
        return Task.CompletedTask;
    }
}
