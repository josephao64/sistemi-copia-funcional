using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Organizations;
using SistemiPos.Application.Security;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Security;

public sealed class LoginAuthorizationService(
    RoleManager<AppRole> roleManager,
    AppDbContext dbContext,
    IOrganizationRepository organizationRepository)
{
    public async Task<LoginAuthorizationContext> LoadAsync(
        AppUser user,
        IReadOnlyCollection<string> roleNames,
        CancellationToken cancellationToken = default)
    {
        var roles = roleNames
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role)
            .ToList();

        var isSuperAdmin = roles.Contains(SecurityRoleNames.SuperAdministrator, StringComparer.OrdinalIgnoreCase);
        var permissions = await LoadPermissionsAsync(roles, isSuperAdmin, cancellationToken);
        var branchIds = (await organizationRepository.GetAuthorizedBranchesForUserAsync(
                user.Id,
                isSuperAdmin,
                cancellationToken))
            .Select(branch => branch.Id)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        return new LoginAuthorizationContext(roles, permissions, branchIds);
    }

    private async Task<IReadOnlyList<string>> LoadPermissionsAsync(
        IReadOnlyCollection<string> roleNames,
        bool isSuperAdmin,
        CancellationToken cancellationToken)
    {
        if (isSuperAdmin)
        {
            return await dbContext.Permissions
                .AsNoTracking()
                .Where(permission => permission.IsActive)
                .Select(permission => permission.Code)
                .OrderBy(code => code)
                .ToListAsync(cancellationToken);
        }

        if (roleNames.Count == 0)
        {
            return [];
        }

        var roleIds = await roleManager.Roles
            .AsNoTracking()
            .Where(role => role.Name != null && roleNames.Contains(role.Name))
            .Select(role => role.Id)
            .ToListAsync(cancellationToken);

        if (roleIds.Count == 0)
        {
            return [];
        }

        return await dbContext.RolePermissions
            .AsNoTracking()
            .Where(rolePermission =>
                roleIds.Contains(rolePermission.RoleId) &&
                rolePermission.Permission.IsActive)
            .Select(rolePermission => rolePermission.Permission.Code)
            .Distinct()
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);
    }
}
