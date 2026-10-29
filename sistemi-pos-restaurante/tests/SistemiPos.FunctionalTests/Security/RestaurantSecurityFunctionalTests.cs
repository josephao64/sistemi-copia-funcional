using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using SistemiPos.Application.Security;
using SistemiPos.Web.Controllers;
using SistemiPos.Web.Models.Account;
using SistemiPos.Web.Models.Security;

namespace SistemiPos.FunctionalTests.Security;

public sealed class RestaurantSecurityFunctionalTests
{
    [Fact]
    public void LoginViewModel_Supports_Session_Expired_Indicator()
    {
        var model = new LoginViewModel
        {
            SessionExpiredMessage = LoginSecurityDefaults.SessionExpiredMessage
        };

        Assert.Equal(LoginSecurityDefaults.SessionExpiredMessage, model.SessionExpiredMessage);
    }

    [Fact]
    public void AccountController_Login_Accepts_Session_Expired_Reason()
    {
        var loginGet = typeof(AccountController).GetMethods()
            .Where(method => method.Name == nameof(AccountController.Login))
            .Select(method => method.GetParameters())
            .FirstOrDefault(parameters => parameters.Any(parameter => parameter.Name == "reason"));

        Assert.NotNull(loginGet);
        Assert.Contains(loginGet, parameter => parameter.Name == "reason");
    }

    [Fact]
    public void ResetPasswordViewModel_Validates_Correctly()
    {
        var model = new UserResetPasswordViewModel
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "mesero",
            FullName = "Mesero Principal",
            NewPassword = "NuevaPassword123",
            ConfirmNewPassword = "NuevaPassword123"
        };

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Fact]
    public void ResetPasswordViewModel_Detects_Mismatched_Passwords()
    {
        var model = new UserResetPasswordViewModel
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "mesero",
            FullName = "Mesero Principal",
            NewPassword = "PasswordA123",
            ConfirmNewPassword = "PasswordB456"
        };

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UserResetPasswordViewModel.ConfirmNewPassword)));
    }

    [Fact]
    public void SecurityAuditController_Requires_SecurityAuditView_Policy()
    {
        var attributes = typeof(SecurityAuditController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .ToList();

        Assert.Contains(attributes, a => a.Policy == PermissionCodes.SecurityAuditView);
    }

    [Fact]
    public void UsersController_ResetPassword_Actions_Require_ResetPassword_Policy()
    {
        var resetPasswordActions = typeof(UsersController).GetMethods()
            .Where(m => m.Name == nameof(UsersController.ResetPassword))
            .ToList();

        Assert.NotEmpty(resetPasswordActions);
        Assert.All(resetPasswordActions, action =>
        {
            var attributes = action.GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<AuthorizeAttribute>();
            Assert.Contains(attributes, a => a.Policy == PermissionCodes.UsersResetPassword);
        });
    }

    [Fact]
    public void UserFormViewModel_Supports_Multiple_Roles()
    {
        var model = new UserFormViewModel
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "mesero.cajero",
            FullName = "Operador Mixto",
            Email = "mixto@restaurante.local",
            RoleName = SecurityRoleNames.Waiter,
            SelectedRoles = [SecurityRoleNames.Waiter, SecurityRoleNames.Cashier],
            IsActive = true
        };

        Assert.Equal(2, model.SelectedRoles.Count);
        Assert.Contains(SecurityRoleNames.Waiter, model.SelectedRoles);
        Assert.Contains(SecurityRoleNames.Cashier, model.SelectedRoles);
    }
}

