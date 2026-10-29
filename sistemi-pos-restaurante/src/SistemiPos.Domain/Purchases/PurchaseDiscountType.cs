using System.ComponentModel.DataAnnotations;

namespace SistemiPos.Domain.Purchases;

public enum PurchaseDiscountType
{
    [Display(Name = "Sin descuento")] None = 0,
    [Display(Name = "Porcentaje")] Percentage = 1,
    [Display(Name = "Monto")] Amount = 2
}
