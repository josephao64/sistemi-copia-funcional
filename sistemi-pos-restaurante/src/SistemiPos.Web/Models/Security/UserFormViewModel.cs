using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemiPos.Domain.Organizations;

namespace SistemiPos.Web.Models.Security;

public sealed class UserFormViewModel
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "El usuario es requerido.")]
    [Display(Name = "Usuario")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es requerido.")]
    [Display(Name = "Nombre completo")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es requerido.")]
    [EmailAddress(ErrorMessage = "El correo no es válido.")]
    [Display(Name = "Correo")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Contraseña")]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [Display(Name = "Confirmar contraseña")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    public string? ConfirmPassword { get; set; }

    [Display(Name = "Rol")]
    public string RoleName { get; set; } = string.Empty;

    [Display(Name = "Roles asignados")]
    public List<string> SelectedRoles { get; set; } = [];

    [Display(Name = "Activo")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Nivel de alcance")]
    public UserScopeLevel ScopeLevel { get; set; } = UserScopeLevel.Branch;

    [Display(Name = "Sucursales autorizadas")]
    public List<Guid> SelectedBranchIds { get; set; } = [];

    [Display(Name = "Sucursal predeterminada")]
    public Guid? DefaultBranchId { get; set; }

    [Display(Name = "Empresa asignada")]
    public Guid? SelectedCompanyId { get; set; }

    [Display(Name = "Grupo empresarial asignado")]
    public Guid? SelectedBusinessGroupId { get; set; }

    public IReadOnlyList<SelectListItem> AvailableRoles { get; set; } = [];
    public IReadOnlyList<SelectListItem> AvailableBranches { get; set; } = [];
    public IReadOnlyList<SelectListItem> AvailableCompanies { get; set; } = [];
    public IReadOnlyList<SelectListItem> AvailableBusinessGroups { get; set; } = [];
}
