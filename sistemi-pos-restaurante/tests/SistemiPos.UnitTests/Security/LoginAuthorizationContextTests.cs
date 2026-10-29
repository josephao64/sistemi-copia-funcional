using System.Security.Claims;
using SistemiPos.Application.Security;

namespace SistemiPos.UnitTests.Security;

public sealed class LoginAuthorizationContextTests
{
    [Fact]
    public void CanAccessSystem_Requires_One_Role_Permissions_And_Branch()
    {
        var valid = new LoginAuthorizationContext(
            [SecurityRoleNames.Cashier],
            [PermissionCodes.SalesManage],
            [Guid.NewGuid()]);

        Assert.True(valid.CanAccessSystem);
        Assert.False((valid with { Roles = [] }).CanAccessSystem);
        Assert.False((valid with { Roles = [SecurityRoleNames.Cashier, SecurityRoleNames.Waiter] }).CanAccessSystem);
        Assert.False((valid with { Permissions = [] }).CanAccessSystem);
        Assert.False((valid with { AuthorizedBranchIds = [] }).CanAccessSystem);
    }

    [Fact]
    public void ToSessionClaims_Loads_Permissions_And_Authorized_Branches()
    {
        var branchId = Guid.NewGuid();
        var context = new LoginAuthorizationContext(
            [SecurityRoleNames.Cashier],
            [PermissionCodes.SalesManage, PermissionCodes.SalesManage],
            [branchId, branchId]);

        var claims = context.ToSessionClaims().ToList();

        Assert.Single(claims, claim => claim.Type == SecurityClaimTypes.Permission);
        Assert.Single(claims, claim => claim.Type == SecurityClaimTypes.AuthorizedBranch);
        Assert.Contains(claims, claim =>
            claim.Type == SecurityClaimTypes.Permission &&
            claim.Value == PermissionCodes.SalesManage);
        Assert.Contains(claims, claim =>
            claim.Type == SecurityClaimTypes.AuthorizedBranch &&
            claim.Value == branchId.ToString());
        Assert.DoesNotContain(claims, claim => claim.Type == ClaimTypes.Hash);
    }
}
