using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Security;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Security.Authorization;

internal sealed class PermissionAuthorizationHandler(
    UserManager<AppUser> userManager,
    AppDbContext dbContext) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userName = context.User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
        {
            return;
        }

        var user = await userManager.FindByNameAsync(userName);
        if (user is null || !user.IsActive)
        {
            return;
        }

        var roleNames = await userManager.GetRolesAsync(user);
        if (roleNames.Contains(SecurityRoleNames.SuperAdministrator, StringComparer.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return;
        }
        var roleIds = await dbContext.Roles
            .AsNoTracking()
            .Where(x => roleNames.Contains(x.Name!))
            .Select(x => x.Id)
            .ToListAsync();

        var allowed = await dbContext.Set<RolePermission>()
            .AsNoTracking()
            .Include(x => x.Permission)
            .Where(x => roleIds.Contains(x.RoleId) && x.Permission.Code == requirement.PermissionCode && x.Permission.IsActive)
            .AnyAsync();

        if (allowed)
        {
            context.Succeed(requirement);
        }
    }
}
