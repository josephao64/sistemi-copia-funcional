using SistemiPos.Domain.Common;

namespace SistemiPos.Domain.Security;

public sealed class RolePermission : Entity
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}
