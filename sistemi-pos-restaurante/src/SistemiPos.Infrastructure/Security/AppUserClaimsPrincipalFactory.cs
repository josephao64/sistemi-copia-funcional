using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace SistemiPos.Infrastructure.Security;

public sealed class AppUserClaimsPrincipalFactory(
    UserManager<AppUser> userManager,
    RoleManager<AppRole> roleManager,
    IOptions<IdentityOptions> options,
    LoginAuthorizationService loginAuthorizationService)
    : UserClaimsPrincipalFactory<AppUser, AppRole>(userManager, roleManager, options)
{
    protected override async Task<System.Security.Claims.ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        var roles = await UserManager.GetRolesAsync(user);
        var loginContext = await loginAuthorizationService.LoadAsync(user, roles.ToArray());

        foreach (var claim in loginContext.ToSessionClaims())
        {
            identity.AddClaim(claim);
        }

        return identity;
    }
}
