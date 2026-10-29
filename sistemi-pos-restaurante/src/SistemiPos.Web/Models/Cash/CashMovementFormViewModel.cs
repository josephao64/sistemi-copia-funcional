using System.ComponentModel.DataAnnotations;
using SistemiPos.Domain.Cash;

namespace SistemiPos.Web.Models.Cash;

public sealed class CashMovementFormViewModel
{
    public Guid CashSessionId { get; set; }

    [Display(Name = "Entrada o salida")]
    public CashMovementType Type { get; set; }

    [Display(Name = "Monto")]
    [Range(typeof(decimal), "0.01", "999999999", ErrorMessage = "El monto debe ser mayor a cero.")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "El motivo es requerido.")]
    [Display(Name = "Motivo")]
    public string Reason { get; set; } = string.Empty;
    public string? AuthorizerUserName { get; set; }
    [DataType(DataType.Password)] public string? AuthorizerPassword { get; set; }
}
