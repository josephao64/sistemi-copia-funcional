using System.ComponentModel.DataAnnotations;

namespace SistemiPos.Web.Models.Account;

public sealed class ProfileViewModel
{
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Roles { get; set; } = string.Empty;
    public DateTimeOffset? LastLoginAtUtc { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Contraseña actual")]
    public string? CurrentPassword { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Nueva contraseña")]
    [StringLength(100, ErrorMessage = "La {0} debe tener al menos {2} caracteres.", MinimumLength = 8)]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Confirmar nueva contraseña")]
    [Compare(nameof(NewPassword), ErrorMessage = "Las contraseñas no coinciden.")]
    public string? ConfirmNewPassword { get; set; }
}

