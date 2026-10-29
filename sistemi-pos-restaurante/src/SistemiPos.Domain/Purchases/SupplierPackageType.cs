using System.ComponentModel.DataAnnotations;

namespace SistemiPos.Domain.Purchases;

public enum SupplierPackageType
{
    [Display(Name = "Unidad")] Unit = 1,
    [Display(Name = "Caja")] Box = 2,
    [Display(Name = "Paquete")] Package = 3,
    [Display(Name = "Fardo")] Bundle = 4,
    [Display(Name = "Docena")] Dozen = 5,
    [Display(Name = "Otro")] Other = 6
}
