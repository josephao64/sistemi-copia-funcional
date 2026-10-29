using SistemiPos.Domain.Organizations;

namespace SistemiPos.Application.Organizations;

public interface IBranchSettingsRepository
{
    Task<BranchSetting?> GetByBranchIdAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task AddAsync(BranchSetting setting, CancellationToken cancellationToken = default);
    Task UpdateAsync(BranchSetting setting, CancellationToken cancellationToken = default);
}
