namespace SistemiPos.Application.Organizations;

public interface IEnterpriseSettingsResolver
{
    Task<EffectiveEnterpriseSettingsDto> ResolveEffectiveSettingsAsync(Guid companyId, Guid? branchId = null, CancellationToken cancellationToken = default);
    Task<BranchSettingDto?> GetBranchSettingAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task SaveBranchSettingAsync(Guid companyId, Guid branchId, SaveBranchSettingRequest request, CancellationToken cancellationToken = default);
}
