using System.ComponentModel.DataAnnotations;

namespace SistemiPos.Web.Models.Security;

public sealed class UserResetPasswordViewModel
{
    public required string Id { get; init; }
    public required string UserName { get; init; }
    public required string FullName { get; init; }

    [Required(ErrorMessage = "La nueva contraseña es requerida.")]
    [StringLength(100, ErrorMessage = "La {0} debe tener al menos {2} caracteres.", MinimumLength = 8)]
    [DataType(DataType.Password)]
    [Display(Name = "Nueva contraseña")]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirmar nueva contraseña")]
    [Compare(nameof(NewPassword), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

