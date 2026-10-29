using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Security;
using SistemiPos.Infrastructure.Persistence;
using SistemiPos.Web.Models.Security;

namespace SistemiPos.Web.Controllers;

[Authorize(Roles = SecurityRoleNames.SuperAdministrator)]
[Authorize(Policy = PermissionCodes.PermissionsView)]
public sealed class PermissionsController(AppDbContext dbContext) : Controller
{
    public async Task<IActionResult> Index()
    {
        var roles = await dbContext.Roles
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new RoleListItemViewModel
            {
                Id = x.Id.ToString(),
                Name = x.Name ?? string.Empty,
                Description = x.Description,
                PermissionCount = x.RolePermissions.Count,
                UserCount = dbContext.UserRoles.Count(userRole => userRole.RoleId == x.Id),
                IsProtected = x.Name == SecurityRoleNames.SuperAdministrator
            })
            .ToListAsync();

        return View(roles);
    }
}
