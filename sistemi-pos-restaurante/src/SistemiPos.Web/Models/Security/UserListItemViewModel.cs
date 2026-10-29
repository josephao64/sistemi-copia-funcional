namespace SistemiPos.Web.Models.Security;

public sealed class UserListItemViewModel
{
    public required string Id { get; init; }
    public required string UserName { get; init; }
    public required string FullName { get; init; }
    public required string Email { get; init; }
    public required string Roles { get; init; }
    public string AssignedBranches { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTimeOffset? LastLoginAtUtc { get; init; }
}
