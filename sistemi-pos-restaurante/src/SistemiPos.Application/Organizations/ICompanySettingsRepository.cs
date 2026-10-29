using SistemiPos.Domain.Organizations;

namespace SistemiPos.Application.Organizations;

public interface ICompanySettingsRepository
{
    Task<CompanySetting?> GetByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task AddAsync(CompanySetting setting, CancellationToken cancellationToken = default);
    Task UpdateAsync(CompanySetting setting, CancellationToken cancellationToken = default);
}
