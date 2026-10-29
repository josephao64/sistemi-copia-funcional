namespace SistemiPos.Application.Organizations;

public interface IOrganizationService
{
    // Business Groups
    Task<IReadOnlyList<BusinessGroupDto>> GetBusinessGroupsAsync(bool onlyActive = false, CancellationToken cancellationToken = default);
    Task<BusinessGroupDto?> GetBusinessGroupByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BusinessGroupDto> CreateBusinessGroupAsync(CreateBusinessGroupDto dto, CancellationToken cancellationToken = default);
    Task<BusinessGroupDto> UpdateBusinessGroupAsync(UpdateBusinessGroupDto dto, CancellationToken cancellationToken = default);
    Task ToggleBusinessGroupActiveAsync(Guid id, CancellationToken cancellationToken = default);

    // Companies
    Task<IReadOnlyList<CompanyDto>> GetCompaniesAsync(Guid? businessGroupId = null, bool onlyActive = false, CancellationToken cancellationToken = default);
    Task<CompanyDto?> GetCompanyByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CompanyDto> CreateCompanyAsync(CreateCompanyDto dto, CancellationToken cancellationToken = default);
    Task<CompanyDto> UpdateCompanyAsync(UpdateCompanyDto dto, CancellationToken cancellationToken = default);
    Task ToggleCompanyActiveAsync(Guid id, CancellationToken cancellationToken = default);

    // Branches
    Task<IReadOnlyList<BranchDto>> GetBranchesAsync(Guid? companyId = null, bool onlyActive = false, CancellationToken cancellationToken = default);
    Task<BranchDto?> GetBranchByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BranchDto> CreateBranchAsync(CreateBranchDto dto, CancellationToken cancellationToken = default);
    Task<BranchDto> UpdateBranchAsync(UpdateBranchDto dto, CancellationToken cancellationToken = default);
    Task ToggleBranchActiveAsync(Guid id, CancellationToken cancellationToken = default);

    // Warehouses
    Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(Guid? branchId = null, bool onlyActive = false, CancellationToken cancellationToken = default);
    Task<WarehouseDto?> GetWarehouseByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WarehouseDto> CreateWarehouseAsync(CreateWarehouseDto dto, CancellationToken cancellationToken = default);
    Task<WarehouseDto> UpdateWarehouseAsync(UpdateWarehouseDto dto, CancellationToken cancellationToken = default);
    Task ToggleWarehouseActiveAsync(Guid id, CancellationToken cancellationToken = default);

    // User Scopes
    Task<IReadOnlyList<UserScopeDto>> GetUserScopesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SaveUserScopesAsync(SaveUserScopesDto dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BranchDto>> GetAuthorizedBranchesForUserAsync(Guid userId, bool isSuperAdmin, CancellationToken cancellationToken = default);
}
