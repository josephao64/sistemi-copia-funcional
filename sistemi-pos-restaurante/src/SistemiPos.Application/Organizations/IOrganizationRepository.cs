using SistemiPos.Domain.Organizations;

namespace SistemiPos.Application.Organizations;

public interface IOrganizationRepository
{
    // Business Group
    Task<List<BusinessGroup>> GetBusinessGroupsAsync(bool onlyActive = false, CancellationToken cancellationToken = default);
    Task<BusinessGroup?> GetBusinessGroupByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BusinessGroup?> GetBusinessGroupByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task AddBusinessGroupAsync(BusinessGroup group, CancellationToken cancellationToken = default);

    // Company
    Task<List<Company>> GetCompaniesAsync(Guid? businessGroupId = null, bool onlyActive = false, CancellationToken cancellationToken = default);
    Task<Company?> GetCompanyByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Company?> GetCompanyByCodeAsync(Guid businessGroupId, string code, CancellationToken cancellationToken = default);
    Task AddCompanyAsync(Company company, CancellationToken cancellationToken = default);

    // Branch
    Task<List<Branch>> GetBranchesAsync(Guid? companyId = null, bool onlyActive = false, CancellationToken cancellationToken = default);
    Task<Branch?> GetBranchByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Branch?> GetBranchByCodeAsync(Guid companyId, string code, CancellationToken cancellationToken = default);
    Task AddBranchAsync(Branch branch, CancellationToken cancellationToken = default);

    // Warehouse
    Task<List<Warehouse>> GetWarehousesAsync(Guid? branchId = null, bool onlyActive = false, CancellationToken cancellationToken = default);
    Task<Warehouse?> GetWarehouseByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Warehouse?> GetWarehouseByCodeAsync(Guid branchId, string code, CancellationToken cancellationToken = default);
    Task AddWarehouseAsync(Warehouse warehouse, CancellationToken cancellationToken = default);

    // User Scope
    Task<List<UserScopeAssignment>> GetUserScopesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<Branch>> GetAuthorizedBranchesForUserAsync(Guid userId, bool isSuperAdmin, CancellationToken cancellationToken = default);
    Task SaveUserScopesAsync(Guid userId, IEnumerable<UserScopeAssignment> assignments, CancellationToken cancellationToken = default);
}
