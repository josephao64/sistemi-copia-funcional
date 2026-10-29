using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Security;
using SistemiPos.Infrastructure.Persistence;
using SistemiPos.Infrastructure.Security;

namespace SistemiPos.Web.Services;

public interface INavigationPermissionService
{
    Task<IReadOnlySet<string>> GetPermissionCodesAsync(CancellationToken cancellationToken = default);
}

public sealed class NavigationPermissionService(
    IHttpContextAccessor httpContextAccessor,
    UserManager<AppUser> userManager,
    AppDbContext dbContext) : INavigationPermissionService
{
    private const string CacheKey = "navigation-permissions";

    public async Task<IReadOnlySet<string>> GetPermissionCodesAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext?.Items[CacheKey] is IReadOnlySet<string> cached)
        {
            return cached;
        }

        var userName = httpContext?.User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var user = await userManager.FindByNameAsync(userName);
        if (user is null || !user.IsActive)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var roles = await userManager.GetRolesAsync(user);
        HashSet<string> permissions;
        if (roles.Contains(SecurityRoleNames.SuperAdministrator, StringComparer.OrdinalIgnoreCase))
        {
            permissions = PermissionCodes.All.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            var roleIds = await dbContext.Roles.AsNoTracking()
                .Where(x => roles.Contains(x.Name!))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            permissions = await dbContext.RolePermissions.AsNoTracking()
                .Where(x => roleIds.Contains(x.RoleId) && x.Permission.IsActive)
                .Select(x => x.Permission.Code)
                .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);
        }

        if (await dbContext.PettyCashFunds.AsNoTracking().AnyAsync(x => x.IsActive && x.ResponsibleUserId == user.Id, cancellationToken))
            permissions.Add(PermissionCodes.PettyCashViewBalance);

        httpContext!.Items[CacheKey] = permissions;
        return permissions;
    }
}
