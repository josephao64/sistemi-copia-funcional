using System.ComponentModel.DataAnnotations;

namespace SistemiPos.Web.Models.Security;

public sealed class RolePermissionsViewModel
{
    [Required(ErrorMessage = "El rol es obligatorio.")]
    public string Id { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;
    public bool IsProtected { get; set; }
    public List<Guid> SelectedPermissionIds { get; set; } = [];
    public IReadOnlyList<PermissionCheckboxViewModel> AvailablePermissions { get; set; } = [];
}
