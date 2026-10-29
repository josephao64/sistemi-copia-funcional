using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemiPos.Application.Organizations;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Common;
using SistemiPos.Domain.Organizations;
using SistemiPos.Infrastructure.Security;
using SistemiPos.Web.Models.Security;

namespace SistemiPos.Web.Controllers;

[Authorize(Roles = SecurityRoleNames.SuperAdministrator)]
[Authorize(Policy = PermissionCodes.UsersView)]
public sealed class UsersController(
    UserManager<AppUser> userManager,
    RoleManager<AppRole> roleManager,
    ISecurityAuditService auditService,
    IOrganizationService organizationService,
    UserAccountService userAccountService) : Controller
{
    public async Task<IActionResult> Index()
    {
        var users = userManager.Users.ToList();
        var viewModel = new List<UserListItemViewModel>();

        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            var scopes = await organizationService.GetUserScopesAsync(user.Id);
            string scopeSummary;
            if (roles.Contains(SecurityRoleNames.SuperAdministrator, StringComparer.OrdinalIgnoreCase) ||
                scopes.Any(x => x.ScopeLevel == UserScopeLevel.Global))
            {
                scopeSummary = "Global (Todas las sucursales)";
            }
            else if (scopes.Any(x => x.ScopeLevel == UserScopeLevel.BusinessGroup))
            {
                scopeSummary = $"Grupo: {scopes.First().BusinessGroupName}";
            }
            else if (scopes.Any(x => x.ScopeLevel == UserScopeLevel.Company))
            {
                scopeSummary = $"Empresa: {scopes.First().CompanyName}";
            }
            else if (scopes.Count > 0)
            {
                var branchNames = scopes
                    .Where(x => !string.IsNullOrEmpty(x.BranchName))
                    .Select(x => x.BranchName + (x.IsDefault ? " (Pred.)" : string.Empty));
                scopeSummary = string.Join(", ", branchNames);
            }
            else
            {
                scopeSummary = "Sin asignación";
            }

            viewModel.Add(new UserListItemViewModel
            {
                Id = user.Id.ToString(),
                UserName = user.UserName ?? string.Empty,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Roles = string.Join(", ", roles),
                IsActive = user.IsActive,
                LastLoginAtUtc = user.LastLoginAtUtc
            });
        }

        return View(viewModel.OrderBy(x => x.UserName).ToList());
    }

    [Authorize(Policy = PermissionCodes.UsersManage)]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        return View("Form", await BuildFormAsync(new UserFormViewModel()));
    }

    [Authorize(Policy = PermissionCodes.UsersManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Password))
        {
            ModelState.AddModelError(nameof(model.Password), "La contraseña es requerida.");
        }

        if (string.IsNullOrWhiteSpace(model.ConfirmPassword))
        {
            ModelState.AddModelError(nameof(model.ConfirmPassword), "Debe confirmar la contraseña.");
        }

        var rolesToAssign = await ValidateRolesAsync(model);

        if (!User.IsInRole(SecurityRoleNames.SuperAdministrator) &&
            rolesToAssign.Contains(SecurityRoleNames.SuperAdministrator, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View("Form", await BuildFormAsync(model));
        }

        var user = new AppUser
        {
            UserName = model.UserName,
            Email = model.Email,
            FullName = NameNormalizer.Normalize(model.FullName),
            IsActive = model.IsActive,
            EmailConfirmed = true,
            LockoutEnabled = true
        };

        var result = await userAccountService.SaveAsync(user, rolesToAssign, isNew: true, password: model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View("Form", await BuildFormAsync(model));
        }

        await organizationService.SaveUserScopesAsync(new SaveUserScopesDto(
            user.Id,
            model.ScopeLevel,
            model.SelectedBranchIds,
            model.DefaultBranchId,
            model.SelectedBusinessGroupId,
            model.SelectedCompanyId));

        var actorUserName = User.Identity?.Name ?? "Sistema";
        await auditService.LogUserManagementAsync(
            actorUserName,
            SecurityAuditActions.CreateUser,
            user.Id.ToString(),
            user.UserName ?? string.Empty,
            $"Usuario creado exitosamente con rol: {string.Join(", ", rolesToAssign)}.");

        TempData["SuccessMessage"] = $"El usuario {user.UserName} ha sido creado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = PermissionCodes.UsersManage)]
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (await IsProtectedUserAsync(user) && !User.IsInRole(SecurityRoleNames.SuperAdministrator))
        {
            return Forbid();
        }

        var roles = await userManager.GetRolesAsync(user);
        var scopes = await organizationService.GetUserScopesAsync(user.Id);
        var model = new UserFormViewModel
        {
            Id = user.Id.ToString(),
            UserName = user.UserName ?? string.Empty,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            RoleName = roles.Count == 1 ? roles[0] : string.Empty,
            IsActive = user.IsActive,
            ScopeLevel = scopes.FirstOrDefault()?.ScopeLevel ?? UserScopeLevel.Branch,
            SelectedBranchIds = scopes.Where(x => x.BranchId.HasValue).Select(x => x.BranchId!.Value).ToList(),
            DefaultBranchId = scopes.FirstOrDefault(x => x.IsDefault && x.BranchId.HasValue)?.BranchId,
            SelectedCompanyId = scopes.FirstOrDefault(x => x.CompanyId.HasValue)?.CompanyId,
            SelectedBusinessGroupId = scopes.FirstOrDefault(x => x.BusinessGroupId.HasValue)?.BusinessGroupId
        };

        return View("Form", await BuildFormAsync(model));
    }

    [Authorize(Policy = PermissionCodes.UsersManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserFormViewModel model)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Id))
        {
            return View("Form", await BuildFormAsync(model));
        }

        var user = await userManager.FindByIdAsync(model.Id);
        if (user is null)
        {
            return NotFound();
        }

        var rolesToAssign = await ValidateRolesAsync(model);
        if (!ModelState.IsValid)
        {
            return View("Form", await BuildFormAsync(model));
        }

        if ((await IsProtectedUserAsync(user) ||
             rolesToAssign.Contains(SecurityRoleNames.SuperAdministrator, StringComparer.OrdinalIgnoreCase)) &&
            !User.IsInRole(SecurityRoleNames.SuperAdministrator))
        {
            return Forbid();
        }

        if (!await ValidateNewPasswordAsync(user, model.Password))
        {
            return View("Form", await BuildFormAsync(model));
        }

        var targetIsSuperAdministrator = await IsProtectedUserAsync(user);
        var removesSuperAdministrator = targetIsSuperAdministrator &&
            (!model.IsActive || !rolesToAssign.Contains(SecurityRoleNames.SuperAdministrator, StringComparer.OrdinalIgnoreCase));
        if (removesSuperAdministrator)
        {
            var activeSuperAdministrators = (await userManager.GetUsersInRoleAsync(SecurityRoleNames.SuperAdministrator))
                .Count(x => x.IsActive && x.Id != user.Id);
            if (activeSuperAdministrators == 0)
            {
                ModelState.AddModelError(string.Empty, "Debe permanecer al menos un Super Administrador activo.");
                return View("Form", await BuildFormAsync(model));
            }
        }

        user.UserName = model.UserName;
        user.Email = model.Email;
        user.FullName = NameNormalizer.Normalize(model.FullName);
        user.IsActive = model.IsActive;
        if (!string.IsNullOrWhiteSpace(model.Password))
        {
            user.PasswordHash = userManager.PasswordHasher.HashPassword(user, model.Password);
            user.SecurityStamp = Guid.NewGuid().ToString();
        }

        var updateResult = await userAccountService.SaveAsync(user, rolesToAssign, isNew: false);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View("Form", await BuildFormAsync(model));
        }

        await organizationService.SaveUserScopesAsync(new SaveUserScopesDto(
            user.Id,
            model.ScopeLevel,
            model.SelectedBranchIds,
            model.DefaultBranchId,
            model.SelectedBusinessGroupId,
            model.SelectedCompanyId));

        var actorUserName = User.Identity?.Name ?? "Sistema";
        await auditService.LogUserManagementAsync(
            actorUserName,
            SecurityAuditActions.UpdateUser,
            user.Id.ToString(),
            user.UserName ?? string.Empty,
            $"Usuario modificado. Activo: {user.IsActive}. Rol: {string.Join(", ", rolesToAssign)}.");

        TempData["SuccessMessage"] = $"El usuario {user.UserName} ha sido actualizado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = PermissionCodes.UsersManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (await IsProtectedUserAsync(user) && user.IsActive)
        {
            var activeSuperAdministrators = (await userManager.GetUsersInRoleAsync(SecurityRoleNames.SuperAdministrator))
                .Count(x => x.IsActive && x.Id != user.Id);
            if (activeSuperAdministrators == 0)
            {
                TempData["ErrorMessage"] = "No se puede desactivar al único Super Administrador activo.";
                return RedirectToAction(nameof(Index));
            }
        }

        user.IsActive = !user.IsActive;
        await userManager.UpdateAsync(user);

        var actorUserName = User.Identity?.Name ?? "Sistema";
        var actionLabel = user.IsActive ? SecurityAuditActions.ActivateUser : SecurityAuditActions.DeactivateUser;
        await auditService.LogUserManagementAsync(
            actorUserName,
            actionLabel,
            user.Id.ToString(),
            user.UserName ?? string.Empty,
            $"Estado cambiado a: {(user.IsActive ? "Activo" : "Inactivo")}.");

        TempData["SuccessMessage"] = $"El usuario {user.UserName} ahora está {(user.IsActive ? "activo" : "inactivo")}.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = PermissionCodes.UsersResetPassword)]
    [HttpGet]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var viewModel = new UserResetPasswordViewModel
        {
            Id = user.Id.ToString(),
            UserName = user.UserName ?? string.Empty,
            FullName = user.FullName
        };

        return View(viewModel);
    }

    [Authorize(Policy = PermissionCodes.UsersResetPassword)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(UserResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByIdAsync(model.Id);
        if (user is null)
        {
            return NotFound();
        }

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, resetToken, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await userManager.UpdateSecurityStampAsync(user);

        var actorUserName = User.Identity?.Name ?? "Sistema";
        await auditService.LogUserManagementAsync(
            actorUserName,
            SecurityAuditActions.ResetPassword,
            user.Id.ToString(),
            user.UserName ?? string.Empty,
            "Contraseña restablecida administrativamente.");

        TempData["SuccessMessage"] = $"La contraseña del usuario {user.UserName} fue restablecida exitosamente.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IReadOnlyList<string>> ValidateRolesAsync(UserFormViewModel model)
    {
        var roles = UserRoleSelection.Resolve(model.SelectedRoles, model.RoleName);
        var result = await userAccountService.ValidateRolesAsync(roles);
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(nameof(model.RoleName), error.Description);
        }
        return roles;
    }

    private async Task<UserFormViewModel> BuildFormAsync(UserFormViewModel model)
    {
        var roles = roleManager.Roles.AsQueryable();
        if (!User.IsInRole(SecurityRoleNames.SuperAdministrator))
        {
            roles = roles.Where(x => x.Name != SecurityRoleNames.SuperAdministrator);
        }

        model.AvailableRoles = roles
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name!, x.Name))
            .ToList();

        var branches = await organizationService.GetBranchesAsync(onlyActive: true);
        model.AvailableBranches = branches
            .Select(x => new SelectListItem($"{x.Name} ({x.CompanyTradeName})", x.Id.ToString()))
            .ToList();

        var companies = await organizationService.GetCompaniesAsync(onlyActive: true);
        model.AvailableCompanies = companies
            .Select(x => new SelectListItem(x.TradeName, x.Id.ToString()))
            .ToList();

        var groups = await organizationService.GetBusinessGroupsAsync(onlyActive: true);
        model.AvailableBusinessGroups = groups
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToList();

        return model;
    }

    private async Task<bool> IsProtectedUserAsync(AppUser user) =>
        await userManager.IsInRoleAsync(user, SecurityRoleNames.SuperAdministrator);

    private async Task<bool> ValidateNewPasswordAsync(AppUser user, string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return true;
        }

        foreach (var validator in userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(userManager, user, password);
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(nameof(UserFormViewModel.Password), error.Description);
            }
        }

        return ModelState.IsValid;
    }
}
