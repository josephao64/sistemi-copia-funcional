using System.ComponentModel.DataAnnotations;

namespace SistemiPos.Web.Models.Settings;

public sealed class BankAccountFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "El banco es requerido.")]
    [Display(Name = "Banco")]
    public string Bank { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre de cuenta es requerido.")]
    [Display(Name = "Nombre o identificador")]
    public string AccountName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El número de cuenta es requerido.")]
    [Display(Name = "Número de cuenta")]
    public string AccountNumber { get; set; } = string.Empty;

    [Display(Name = "Activa")]
    public bool IsActive { get; set; } = true;
}
