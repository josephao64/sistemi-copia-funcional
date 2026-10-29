namespace SistemiPos.Web.Models.Security;

public sealed class RoleListItemViewModel
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public int PermissionCount { get; init; }
    public int UserCount { get; init; }
    public bool IsProtected { get; init; }
}
