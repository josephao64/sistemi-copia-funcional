using System.ComponentModel.DataAnnotations;

namespace SistemiPos.Web.Models.Cash;

public sealed class OpenCashSessionViewModel
{
    [StringLength(50)] public string? RegisterCode { get; set; }
    [Display(Name = "POS bancarios asignados")]
    public List<Guid> PosDeviceIds { get; set; } = [];

    [Display(Name = "Usaré POS en esta caja")]
    public bool UsePosDevices { get; set; }

    [Display(Name = "Monto inicial")]
    [Range(typeof(decimal), "0", "999999999", ErrorMessage = "El monto inicial no puede ser negativo.")]
    public decimal OpeningAmount { get; set; }

    [StringLength(500, ErrorMessage = "La observación no puede superar 500 caracteres.")]
    [Display(Name = "Observación")]
    public string? Observation { get; set; }

    public bool ReturnToSales { get; set; }
}
