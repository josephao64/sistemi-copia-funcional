using System.Security.Claims;

namespace SistemiPos.Application.Security;

public sealed record LoginAuthorizationContext(
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<Guid> AuthorizedBranchIds)
{
    public bool CanAccessSystem =>
        Roles.Count == 1 &&
        Permissions.Count > 0 &&
        AuthorizedBranchIds.Count > 0;

    public IEnumerable<Claim> ToSessionClaims()
    {
        foreach (var permission in Permissions.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            yield return new Claim(SecurityClaimTypes.Permission, permission);
        }

        foreach (var branchId in AuthorizedBranchIds.Distinct())
        {
            yield return new Claim(SecurityClaimTypes.AuthorizedBranch, branchId.ToString());
        }
    }
}
