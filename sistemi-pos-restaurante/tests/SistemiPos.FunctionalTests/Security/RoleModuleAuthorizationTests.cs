using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SistemiPos.Application.Security;
using SistemiPos.FunctionalTests.Infrastructure;
using SistemiPos.Web.Controllers;
using System.Security.Claims;

namespace SistemiPos.FunctionalTests.Security;

public sealed class RoleModuleAuthorizationTests : IClassFixture<TestingWebApplicationFactory>
{
    private readonly TestingWebApplicationFactory _factory;

    public RoleModuleAuthorizationTests(TestingWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(typeof(SalesController))]
    public void Sales_Controllers_Require_Sales_Access_Policy(Type controllerType)
    {
        var attributes = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>();

        Assert.Contains(attributes, attribute => attribute.Policy == RoleAccessPolicies.SalesAccess);
    }

    [Fact]
    public void Sales_Create_Actions_Require_Authorized_Pos_Role_And_Manage_Permission()
    {
        var actions = typeof(SalesController).GetMethods()
            .Where(method => method.Name == nameof(SalesController.Create))
            .ToList();

        Assert.NotEmpty(actions);
        Assert.All(actions, action =>
        {
            var attributes = action.GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<AuthorizeAttribute>()
                .ToList();

            Assert.Contains(attributes, attribute => attribute.Roles == SecurityRoleNames.PointOfSaleAccess);
            Assert.Contains(attributes, attribute => attribute.Policy == PermissionCodes.SalesManage);
        });
    }

    [Theory]
    [InlineData(typeof(HomeController), PermissionCodes.DashboardView)]
    [InlineData(typeof(ProductsController), PermissionCodes.ProductsView)]
    [InlineData(typeof(CategoriesController), PermissionCodes.CategoriesView)]
    [InlineData(typeof(InventoryController), PermissionCodes.InventoryView)]
    [InlineData(typeof(PurchasesController), PermissionCodes.PurchasesView)]
    [InlineData(typeof(SuppliersController), PermissionCodes.SuppliersView)]
    [InlineData(typeof(CustomersController), PermissionCodes.CustomersView)]
    [InlineData(typeof(CashController), PermissionCodes.CashView)]
    [InlineData(typeof(ReportsController), PermissionCodes.ReportsView)]
    public void Operational_Module_Controllers_Authorize_By_Permission_For_Custom_Roles(Type controllerType, string permission)
    {
        var attributes = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .ToList();

        Assert.Contains(attributes, attribute => attribute.Policy == permission);
        Assert.DoesNotContain(attributes, attribute => !string.IsNullOrWhiteSpace(attribute.Roles));
    }

    [Theory]
    [InlineData(typeof(UsersController), SecurityRoleNames.SuperAdministrator)]
    [InlineData(typeof(RolesController), SecurityRoleNames.SuperAdministrator)]
    [InlineData(typeof(PermissionsController), SecurityRoleNames.SuperAdministrator)]
    [InlineData(typeof(SettingsController), SecurityRoleNames.SuperAdministrator)]
    public void Security_Module_Controllers_Remain_Restricted_To_SuperAdministrator(Type controllerType, string expectedRoles)
    {
        var attributes = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>();

        Assert.Contains(attributes, attribute => attribute.Roles == expectedRoles);
    }

    [Fact]
    public void Roles_Index_Redirects_To_The_Centralized_Permissions_Page()
    {
        var controller = new RolesController(null!, null!);

        var result = Assert.IsType<RedirectToActionResult>(controller.Index());

        Assert.Equal("Permissions", result.ControllerName);
        Assert.Equal("Index", result.ActionName);
    }

    [Fact]
    public async Task SalesAccess_Policy_Allows_Authenticated_User_And_Action_Permission_Decides_Access()
    {
        var result = await AuthorizeSalesAccessAsync(SecurityRoleNames.PointOfSale);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(SecurityRoleNames.SuperAdministrator)]
    [InlineData(SecurityRoleNames.SuperAdministrator, SecurityRoleNames.Administrator)]
    public async Task SalesAccess_Policy_Does_Not_Depend_On_Role_Name(params string[] roles)
    {
        var result = await AuthorizeSalesAccessAsync(roles);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(SecurityRoleNames.Administrator)]
    [InlineData(SecurityRoleNames.Manager)]
    [InlineData(SecurityRoleNames.PointOfSale, SecurityRoleNames.Administrator)]
    [InlineData("Rol personalizado")]
    public async Task SalesAccess_Policy_Allows_Custom_And_BuiltIn_Roles_Then_Permission_Policy_Applies(params string[] roles)
    {
        var result = await AuthorizeSalesAccessAsync(roles);

        Assert.True(result.Succeeded);
    }

    private async Task<AuthorizationResult> AuthorizeSalesAccessAsync(params string[] roles)
    {
        using var scope = _factory.Services.CreateScope();
        var authorizationService = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var identity = new ClaimsIdentity(
            roles.Select(role => new Claim(ClaimTypes.Role, role)),
            authenticationType: "Test");

        return await authorizationService.AuthorizeAsync(
            new ClaimsPrincipal(identity),
            resource: null,
            RoleAccessPolicies.SalesAccess);
    }
}
