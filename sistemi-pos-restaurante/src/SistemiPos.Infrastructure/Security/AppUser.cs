using Microsoft.AspNetCore.Identity;

namespace SistemiPos.Infrastructure.Security;

public sealed class AppUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastLoginAtUtc { get; set; }
}
