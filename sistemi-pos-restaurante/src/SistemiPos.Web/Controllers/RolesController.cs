using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Security;
using SistemiPos.Infrastructure.Persistence;
using SistemiPos.Infrastructure.Security;
using SistemiPos.Web.Models.Security;

namespace SistemiPos.Web.Controllers;

[Authorize(Roles = SecurityRoleNames.SuperAdministrator)]
[Authorize(Policy = PermissionCodes.RolesView)]
public sealed class RolesController(
    RoleManager<AppRole> roleManager,
    AppDbContext dbContext) : Controller
{
    public IActionResult Index() => RedirectToAction("Index", "Permissions");

    [Authorize(Policy = PermissionCodes.RolesManage)]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        return View("Form", await BuildFormAsync(new RoleFormViewModel()));
    }

    [Authorize(Policy = PermissionCodes.RolesManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RoleFormViewModel model)
    {
        RejectLegacyAdministratorName(model.Name);
        if (!ModelState.IsValid)
        {
            return View("Form", await BuildFormAsync(model));
        }

        var role = new AppRole
        {
            Name = model.Name,
            Description = model.Description
        };

        var result = await roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View("Form", await BuildFormAsync(model));
        }

        await SavePermissionsAsync(role.Id, model.SelectedPermissionIds);
        return RedirectToAction("Index", "Permissions");
    }

    [Authorize(Policy = PermissionCodes.RolesManage)]
    [HttpGet]
    public async Task<IActionResult> ConfigurePermissions(string id)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role is null)
        {
            return NotFound();
        }

        if (IsProtected(role) && !User.IsInRole(SecurityRoleNames.SuperAdministrator))
        {
            return Forbid();
        }

        return View("Permissions", await BuildPermissionsFormAsync(role));
    }

    [Authorize(Policy = PermissionCodes.RolesManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfigurePermissions(RolePermissionsViewModel model)
    {
        var role = await roleManager.FindByIdAsync(model.Id);
        if (role is null)
        {
            return NotFound();
        }

        if (IsProtected(role) && !User.IsInRole(SecurityRoleNames.SuperAdministrator))
        {
            return Forbid();
        }

        if (IsProtected(role))
        {
            model.SelectedPermissionIds = await dbContext.Permissions
                .Where(x => x.IsActive)
                .Select(x => x.Id)
                .ToListAsync();
        }

        await SavePermissionsAsync(role.Id, model.SelectedPermissionIds);
        TempData["Success"] = IsProtected(role)
            ? "El Super Administrador conserva todos los permisos."
            : $"Permisos de {role.Name} actualizados.";
        return RedirectToAction("Index", "Permissions");
    }

    [Authorize(Policy = PermissionCodes.RolesManage)]
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role is null)
        {
            return NotFound();
        }

        if (IsProtected(role) && !User.IsInRole(SecurityRoleNames.SuperAdministrator))
        {
            return Forbid();
        }

        var selectedIds = await dbContext.RolePermissions
            .Where(x => x.RoleId == role.Id)
            .Select(x => x.PermissionId)
            .ToListAsync();

        var model = new RoleFormViewModel
        {
            Id = role.Id.ToString(),
            Name = role.Name ?? string.Empty,
            Description = role.Description,
            SelectedPermissionIds = selectedIds,
            IsProtected = IsProtected(role)
        };

        return View("Form", await BuildFormAsync(model));
    }

    [Authorize(Policy = PermissionCodes.RolesManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(RoleFormViewModel model)
    {
        RejectLegacyAdministratorName(model.Name);
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Id))
        {
            return View("Form", await BuildFormAsync(model));
        }

        var role = await roleManager.FindByIdAsync(model.Id);
        if (role is null)
        {
            return NotFound();
        }

        if (IsProtected(role) && !User.IsInRole(SecurityRoleNames.SuperAdministrator))
        {
            return Forbid();
        }

        if (IsProtected(role))
        {
            model.Name = SecurityRoleNames.SuperAdministrator;
            model.Description = role.Description;
            model.SelectedPermissionIds = await dbContext.Permissions.Select(x => x.Id).ToListAsync();
        }
        else
        {
            role.Name = model.Name;
            role.Description = model.Description;
        }

        var result = await roleManager.UpdateAsync(role);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View("Form", await BuildFormAsync(model));
        }

        return RedirectToAction("Index", "Permissions");
    }

    private async Task<RoleFormViewModel> BuildFormAsync(RoleFormViewModel model)
    {
        var permissions = await dbContext.Permissions
            .AsNoTracking()
            .OrderBy(x => x.Module)
            .ThenBy(x => x.Action)
            .ToListAsync();

        model.AvailablePermissions = permissions
            .Select(x => new PermissionCheckboxViewModel
            {
                Id = x.Id,
                Code = x.Code,
                Module = x.Module,
                Action = x.Action,
                Selected = model.SelectedPermissionIds.Contains(x.Id)
            })
            .ToList();

        if (!string.IsNullOrWhiteSpace(model.Id))
        {
            var role = await roleManager.FindByIdAsync(model.Id);
            model.IsProtected = role is not null && IsProtected(role);
            if (model.IsProtected)
            {
                model.SelectedPermissionIds = model.AvailablePermissions.Select(x => x.Id).ToList();
                model.AvailablePermissions = model.AvailablePermissions
                    .Select(x => new PermissionCheckboxViewModel
                    {
                        Id = x.Id,
                        Code = x.Code,
                        Module = x.Module,
                        Action = x.Action,
                        Selected = true
                    })
                    .ToList();
            }
        }

        return model;
    }

    private async Task SavePermissionsAsync(Guid roleId, List<Guid> selectedPermissionIds)
    {
        var validPermissionIds = await dbContext.Permissions
            .Where(x => x.IsActive && selectedPermissionIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync();
        var current = await dbContext.RolePermissions.Where(x => x.RoleId == roleId).ToListAsync();
        dbContext.RolePermissions.RemoveRange(current);

        foreach (var permissionId in validPermissionIds.Distinct())
        {
            dbContext.RolePermissions.Add(new RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionId
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task<RolePermissionsViewModel> BuildPermissionsFormAsync(AppRole role)
    {
        var selectedIds = await dbContext.RolePermissions
            .Where(x => x.RoleId == role.Id)
            .Select(x => x.PermissionId)
            .ToListAsync();
        var isProtected = IsProtected(role);
        var permissions = await dbContext.Permissions
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Module)
            .ThenBy(x => x.Action)
            .ToListAsync();

        return new RolePermissionsViewModel
        {
            Id = role.Id.ToString(),
            RoleName = role.Name ?? string.Empty,
            IsProtected = isProtected,
            SelectedPermissionIds = isProtected ? permissions.Select(x => x.Id).ToList() : selectedIds,
            AvailablePermissions = permissions.Select(x => new PermissionCheckboxViewModel
            {
                Id = x.Id,
                Code = x.Code,
                Module = x.Module,
                Action = x.Action,
                Selected = isProtected || selectedIds.Contains(x.Id)
            }).ToList()
        };
    }

    private static bool IsProtected(AppRole role) =>
        string.Equals(role.Name, SecurityRoleNames.SuperAdministrator, StringComparison.OrdinalIgnoreCase);

    private void RejectLegacyAdministratorName(string roleName)
    {
        if (string.Equals(roleName?.Trim(), SecurityRoleNames.LegacyAdministrator, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(RoleFormViewModel.Name), "El nombre Administrator es heredado. Utiliza un nombre descriptivo diferente para el nuevo rol.");
        }
    }
}
