using System.ComponentModel.DataAnnotations;

namespace SistemiPos.Web.Models.Security;

public sealed class RoleFormViewModel
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "El nombre es requerido.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción es requerida.")]
    [Display(Name = "Descripción")]
    public string Description { get; set; } = string.Empty;

    public List<Guid> SelectedPermissionIds { get; set; } = [];
    public IReadOnlyList<PermissionCheckboxViewModel> AvailablePermissions { get; set; } = [];
    public bool IsProtected { get; set; }
}
