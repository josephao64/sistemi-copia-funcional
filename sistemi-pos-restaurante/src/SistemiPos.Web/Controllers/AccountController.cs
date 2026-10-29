using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SistemiPos.Application.Abstractions;
using SistemiPos.Infrastructure.Security;
using SistemiPos.Application.Security;
using SistemiPos.Web.Models.Account;

namespace SistemiPos.Web.Controllers;

public sealed class AccountController(
    SignInManager<AppUser> signInManager,
    UserManager<AppUser> userManager,
    IAuthorizationService authorizationService,
    ISecurityAuditService auditService,
    LoginAuthorizationService loginAuthorizationService,
    IDateTimeProvider dateTimeProvider) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null, string? reason = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return await RedirectToDefaultModuleAsync(User);
        }

        var model = new LoginViewModel { ReturnUrl = returnUrl };
        if (string.Equals(reason, LoginSecurityDefaults.SessionExpiredQueryValue, StringComparison.OrdinalIgnoreCase))
        {
            model.SessionExpiredMessage = LoginSecurityDefaults.SessionExpiredMessage;
        }

        return View(model);
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

        AppUser? user = model.UserName.Contains('@')
            ? await userManager.FindByEmailAsync(model.UserName)
            : await userManager.FindByNameAsync(model.UserName);

        if (user is null)
        {
            await auditService.LogUserNotFoundAsync(model.UserName, ipAddress, userAgent);
            ModelState.AddModelError(string.Empty, LoginSecurityDefaults.InvalidCredentialsMessage);
            return View(model);
        }

        if (!user.IsActive)
        {
            await auditService.LogLoginFailureAsync(user.UserName ?? model.UserName, "Cuenta desactivada", ipAddress, userAgent);
            ModelState.AddModelError(string.Empty, "Esta cuenta se encuentra desactivada.");
            return View(model);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            await auditService.LogAccountLockedOutAsync(user.UserName ?? model.UserName, ipAddress, userAgent);
            ModelState.AddModelError(string.Empty, LoginSecurityDefaults.TemporaryLockoutMessage);
            return View(model);
        }

        var result = await signInManager.CheckPasswordSignInAsync(
            user,
            model.Password,
            lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            await auditService.LogAccountLockedOutAsync(user.UserName!, ipAddress, userAgent);
            ModelState.AddModelError(string.Empty, LoginSecurityDefaults.TemporaryLockoutMessage);
            return View(model);
        }

        if (!result.Succeeded)
        {
            await auditService.LogLoginFailureAsync(user.UserName!, "Contraseña incorrecta", ipAddress, userAgent);
            ModelState.AddModelError(string.Empty, LoginSecurityDefaults.InvalidCredentialsMessage);
            return View(model);
        }

        var roles = await userManager.GetRolesAsync(user);
        var loginContext = await loginAuthorizationService.LoadAsync(user, roles.ToArray(), HttpContext.RequestAborted);
        if (!loginContext.CanAccessSystem)
        {
            await auditService.LogLoginFailureAsync(user.UserName!, "Usuario sin autorizacion operativa completa", ipAddress, userAgent);
            ModelState.AddModelError(string.Empty, LoginSecurityDefaults.UnauthorizedAccountMessage);
            return View(model);
        }

        await signInManager.SignInAsync(user, model.RememberMe);

        user.LastLoginAtUtc = dateTimeProvider.UtcNow;
        await userManager.UpdateAsync(user);
        await auditService.LogLoginSuccessAsync(user.UserName!, ipAddress, userAgent);

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        var principal = await signInManager.CreateUserPrincipalAsync(user);
        return await RedirectToDefaultModuleAsync(principal);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userName = User.Identity?.Name ?? "Desconocido";
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

        await signInManager.SignOutAsync();
        await auditService.LogLogoutAsync(userName, ipAddress, userAgent);

        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var userName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
        {
            return RedirectToAction(nameof(Login));
        }

        var user = await userManager.FindByNameAsync(userName);
        if (user is null)
        {
            return NotFound();
        }

        var roles = await userManager.GetRolesAsync(user);

        var viewModel = new ProfileViewModel
        {
            UserName = user.UserName ?? string.Empty,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Roles = string.Join(", ", roles),
            LastLoginAtUtc = user.LastLoginAtUtc
        };

        return View(viewModel);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        var userName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
        {
            return RedirectToAction(nameof(Login));
        }

        var user = await userManager.FindByNameAsync(userName);
        if (user is null)
        {
            return NotFound();
        }

        var roles = await userManager.GetRolesAsync(user);
        model.UserName = user.UserName ?? string.Empty;
        model.FullName = user.FullName;
        model.Email = user.Email ?? string.Empty;
        model.Roles = string.Join(", ", roles);
        model.LastLoginAtUtc = user.LastLoginAtUtc;

        if (string.IsNullOrWhiteSpace(model.CurrentPassword) && string.IsNullOrWhiteSpace(model.NewPassword))
        {
            return View(model);
        }

        if (string.IsNullOrWhiteSpace(model.CurrentPassword))
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "Debes ingresar tu contraseña actual.");
            return View(model);
        }

        if (string.IsNullOrWhiteSpace(model.NewPassword))
        {
            ModelState.AddModelError(nameof(model.NewPassword), "Debes ingresar la nueva contraseña.");
            return View(model);
        }

        var changeResult = await userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!changeResult.Succeeded)
        {
            foreach (var error in changeResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await signInManager.RefreshSignInAsync(user);
        await auditService.LogUserManagementAsync(
            userName,
            SecurityAuditActions.ChangeOwnPassword,
            user.Id.ToString(),
            user.UserName ?? string.Empty,
            "Cambio de contraseña exitoso por el propio usuario.");

        TempData["SuccessMessage"] = "Tu contraseña ha sido actualizada correctamente.";
        return RedirectToAction(nameof(Profile));
    }

    private async Task<IActionResult> RedirectToDefaultModuleAsync(System.Security.Claims.ClaimsPrincipal principal)
    {
        var isExclusivePointOfSaleUser =
            (principal.IsInRole(SecurityRoleNames.PointOfSale) || principal.IsInRole(SecurityRoleNames.Cashier)) &&
            !principal.IsInRole(SecurityRoleNames.SuperAdministrator) &&
            !principal.IsInRole(SecurityRoleNames.Administrator) &&
            !principal.IsInRole(SecurityRoleNames.Manager);

        if (isExclusivePointOfSaleUser)
        {
            return RedirectToRoute("pos");
        }

        var destinations = new (string Permission, string Action, string Controller)[]
        {
            (PermissionCodes.DashboardView, "Index", "Home"),
            (PermissionCodes.KitchenOrdersView, "Kitchen", "Restaurant"),
            (PermissionCodes.OrdersView, "Index", "Restaurant"),
            (PermissionCodes.ProductsView, "Index", "Products"),
            (PermissionCodes.RecipesView, "Index", "Recipes"),
            (PermissionCodes.CategoriesView, "Index", "Categories"),
            (PermissionCodes.InventoryView, "Index", "Inventory"),
            (PermissionCodes.PurchasesView, "Index", "Purchases"),
            (PermissionCodes.SuppliersView, "Index", "Suppliers"),
            (PermissionCodes.CustomersView, "Index", "Customers"),
            (PermissionCodes.CashView, "Index", "Cash"),
            (PermissionCodes.ReportsView, "Sales", "Reports")
        };

        foreach (var destination in destinations)
        {
            if ((await authorizationService.AuthorizeAsync(principal, destination.Permission)).Succeeded)
            {
                return RedirectToAction(destination.Action, destination.Controller);
            }
        }

        return RedirectToAction(nameof(AccessDenied));
    }
}
