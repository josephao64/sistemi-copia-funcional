using System.ComponentModel.DataAnnotations;

namespace SistemiPos.Web.Models.Cash;

public sealed class CloseCashSessionViewModel
{
    public Guid Id { get; set; }
    public string? AuthorizerUserName { get; set; }
    public string? AuthorizerPassword { get; set; }

    [Display(Name = "Efectivo contado")]
    [Range(typeof(decimal), "0", "999999999", ErrorMessage = "El efectivo contado no puede ser negativo.")]
    public decimal CountedCash { get; set; }
    [StringLength(500, ErrorMessage = "La observación no puede superar 500 caracteres.")]
    [Display(Name = "Comentario")]
    public string? Observation { get; set; }
}
