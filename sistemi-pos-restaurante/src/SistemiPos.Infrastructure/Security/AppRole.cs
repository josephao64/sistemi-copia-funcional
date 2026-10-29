using Microsoft.AspNetCore.Identity;
using SistemiPos.Domain.Security;

namespace SistemiPos.Infrastructure.Security;

public sealed class AppRole : IdentityRole<Guid>
{
    public string Description { get; set; } = string.Empty;
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
