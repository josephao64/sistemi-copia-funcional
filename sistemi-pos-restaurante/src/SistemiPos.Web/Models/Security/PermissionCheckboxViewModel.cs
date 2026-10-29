namespace SistemiPos.Web.Models.Security;

public sealed class PermissionCheckboxViewModel
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Module { get; init; }
    public required string Action { get; init; }
    public bool Selected { get; set; }
}
