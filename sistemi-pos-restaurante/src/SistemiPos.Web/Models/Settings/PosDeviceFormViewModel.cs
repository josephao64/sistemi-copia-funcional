using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SistemiPos.Web.Models.Settings;

public sealed class PosDeviceFormViewModel
{
    public Guid? Id { get; set; }
    [Required(ErrorMessage = "El nombre es requerido."), MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Required(ErrorMessage = "El banco o proveedor es requerido."), MaxLength(100)] public string Provider { get; set; } = string.Empty;
    [Required(ErrorMessage = "El Terminal ID es requerido."), MaxLength(100)] public string TerminalId { get; set; } = string.Empty;
    [Required(ErrorMessage = "Seleccione una cuenta bancaria.")] public Guid? BankAccountId { get; set; }
    [Required(ErrorMessage = "La ubicación o caja es requerida."), MaxLength(150)] public string Location { get; set; } = string.Empty;
    [MaxLength(500)] public string? Observation { get; set; }
    public bool IsActive { get; set; } = true;
    public IReadOnlyList<SelectListItem> BankAccounts { get; set; } = [];
}
