using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Organizations;
using SistemiPos.Domain.Organizations;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Organizations;

public sealed class OrganizationRepository(AppDbContext dbContext) : IOrganizationRepository
{
    // Business Group
    public Task<List<BusinessGroup>> GetBusinessGroupsAsync(bool onlyActive = false, CancellationToken cancellationToken = default)
    {
        var query = dbContext.BusinessGroups.Include(x => x.Companies).AsNoTracking();
        if (onlyActive)
        {
            query = query.Where(x => x.IsActive);
        }
        return query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public Task<BusinessGroup?> GetBusinessGroupByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.BusinessGroups
            .Include(x => x.Companies)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<BusinessGroup?> GetBusinessGroupByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        dbContext.BusinessGroups
            .FirstOrDefaultAsync(x => x.Code == code.Trim().ToUpperInvariant(), cancellationToken);

    public async Task AddBusinessGroupAsync(BusinessGroup group, CancellationToken cancellationToken = default)
    {
        await dbContext.BusinessGroups.AddAsync(group, cancellationToken);
    }

    // Company
    public Task<List<Company>> GetCompaniesAsync(Guid? businessGroupId = null, bool onlyActive = false, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Companies
            .Include(x => x.BusinessGroup)
            .Include(x => x.Branches)
            .AsNoTracking();

        if (businessGroupId.HasValue)
        {
            query = query.Where(x => x.BusinessGroupId == businessGroupId.Value);
        }

        if (onlyActive)
        {
            query = query.Where(x => x.IsActive);
        }

        return query.OrderBy(x => x.TradeName).ToListAsync(cancellationToken);
    }

    public Task<Company?> GetCompanyByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Companies
            .Include(x => x.BusinessGroup)
            .Include(x => x.Branches)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Company?> GetCompanyByCodeAsync(Guid businessGroupId, string code, CancellationToken cancellationToken = default) =>
        dbContext.Companies
            .FirstOrDefaultAsync(x => x.BusinessGroupId == businessGroupId && x.Code == code.Trim().ToUpperInvariant(), cancellationToken);

    public async Task AddCompanyAsync(Company company, CancellationToken cancellationToken = default)
    {
        await dbContext.Companies.AddAsync(company, cancellationToken);
    }

    // Branch
    public Task<List<Branch>> GetBranchesAsync(Guid? companyId = null, bool onlyActive = false, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Branches
            .Include(x => x.Company)
            .Include(x => x.Warehouses)
            .AsNoTracking();

        if (companyId.HasValue)
        {
            query = query.Where(x => x.CompanyId == companyId.Value);
        }

        if (onlyActive)
        {
            query = query.Where(x => x.IsActive);
        }

        return query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public Task<Branch?> GetBranchByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Branches
            .Include(x => x.Company)
            .Include(x => x.Warehouses)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Branch?> GetBranchByCodeAsync(Guid companyId, string code, CancellationToken cancellationToken = default) =>
        dbContext.Branches
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Code == code.Trim().ToUpperInvariant(), cancellationToken);

    public async Task AddBranchAsync(Branch branch, CancellationToken cancellationToken = default)
    {
        await dbContext.Branches.AddAsync(branch, cancellationToken);
    }

    // Warehouse
    public Task<List<Warehouse>> GetWarehousesAsync(Guid? branchId = null, bool onlyActive = false, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Warehouses
            .Include(x => x.Branch)
                .ThenInclude(b => b.Company)
            .AsNoTracking();

        if (branchId.HasValue)
        {
            query = query.Where(x => x.BranchId == branchId.Value);
        }

        if (onlyActive)
        {
            query = query.Where(x => x.IsActive);
        }

        return query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public Task<Warehouse?> GetWarehouseByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Warehouses
            .Include(x => x.Branch)
                .ThenInclude(b => b.Company)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Warehouse?> GetWarehouseByCodeAsync(Guid branchId, string code, CancellationToken cancellationToken = default) =>
        dbContext.Warehouses
            .FirstOrDefaultAsync(x => x.BranchId == branchId && x.Code == code.Trim().ToUpperInvariant(), cancellationToken);

    public async Task AddWarehouseAsync(Warehouse warehouse, CancellationToken cancellationToken = default)
    {
        await dbContext.Warehouses.AddAsync(warehouse, cancellationToken);
    }

    // User Scope
    public Task<List<UserScopeAssignment>> GetUserScopesAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.UserScopeAssignments
            .Include(x => x.BusinessGroup)
            .Include(x => x.Company)
            .Include(x => x.Branch)
            .Where(x => x.UserId == userId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<List<Branch>> GetAuthorizedBranchesForUserAsync(Guid userId, bool isSuperAdmin, CancellationToken cancellationToken = default)
    {
        if (isSuperAdmin)
        {
            return await dbContext.Branches
                .Include(x => x.Company)
                .Include(x => x.Warehouses)
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        var scopes = await dbContext.UserScopeAssignments
            .Where(x => x.UserId == userId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (scopes.Count == 0)
        {
            return [];
        }

        if (scopes.Any(x => x.ScopeLevel == UserScopeLevel.Global))
        {
            return await dbContext.Branches
                .Include(x => x.Company)
                .Include(x => x.Warehouses)
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        var authorizedGroupIds = scopes.Where(x => x.ScopeLevel == UserScopeLevel.BusinessGroup && x.BusinessGroupId.HasValue)
            .Select(x => x.BusinessGroupId!.Value).ToHashSet();

        var authorizedCompanyIds = scopes.Where(x => x.ScopeLevel == UserScopeLevel.Company && x.CompanyId.HasValue)
            .Select(x => x.CompanyId!.Value).ToHashSet();

        var authorizedBranchIds = scopes.Where(x => x.ScopeLevel == UserScopeLevel.Branch && x.BranchId.HasValue)
            .Select(x => x.BranchId!.Value).ToHashSet();

        var branches = await dbContext.Branches
            .Include(x => x.Company)
            .Include(x => x.Warehouses)
            .Where(x => x.IsActive && (
                authorizedBranchIds.Contains(x.Id) ||
                authorizedCompanyIds.Contains(x.CompanyId) ||
                authorizedGroupIds.Contains(x.Company.BusinessGroupId)))
            .OrderBy(x => x.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return branches;
    }

    public async Task SaveUserScopesAsync(Guid userId, IEnumerable<UserScopeAssignment> assignments, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.UserScopeAssignments
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        dbContext.UserScopeAssignments.RemoveRange(existing);
        await dbContext.UserScopeAssignments.AddRangeAsync(assignments, cancellationToken);
    }
}
