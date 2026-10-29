using SistemiPos.Application.Organizations;

namespace SistemiPos.Application.Abstractions;

public interface IOperationContextAccessor
{
    Guid? CurrentBranchId { get; }
    Guid? CurrentCompanyId { get; }
    Guid? CurrentBusinessGroupId { get; }
    Guid? CurrentWarehouseId { get; }
    string? CurrentBranchName { get; }
    string? CurrentCompanyName { get; }
    Task<IReadOnlyList<BranchDto>> GetAuthorizedBranchesAsync(CancellationToken cancellationToken = default);
    Task SetCurrentBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
}
