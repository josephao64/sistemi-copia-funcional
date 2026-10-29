using Microsoft.AspNetCore.Authorization;

namespace SistemiPos.Infrastructure.Security.Authorization;

public sealed class PermissionRequirement(string permissionCode) : IAuthorizationRequirement
{
    public string PermissionCode { get; } = permissionCode;
}
