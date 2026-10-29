using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemiPos.Domain.Purchases;

namespace SistemiPos.Web.Models.Purchases;

public sealed class PurchaseFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "El proveedor es requerido.")]
    [Display(Name = "Proveedor")]
    public Guid SupplierId { get; set; }

    [Display(Name = "Fecha")]
    public DateTime PurchaseDateLocal { get; set; } = DateTime.Now;
    [Display(Name = "Condición")] public PurchaseTermsType Terms { get; set; } = PurchaseTermsType.Cash;
    [Display(Name = "Fecha de vencimiento")] public DateTime? DueDateLocal { get; set; }

    public List<PurchaseItemFormViewModel> Items { get; set; } = [new()];
    public IReadOnlyList<SelectListItem> Suppliers { get; set; } = [];
    public IReadOnlyList<SelectListItem> Products { get; set; } = [];
}

public sealed class PurchaseItemFormViewModel
{
    [Required(ErrorMessage = "El producto es requerido.")]
    [Display(Name = "Producto")]
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "La cantidad debe ser mayor a cero.")]
    [Display(Name = "Cantidad")]
    public decimal Quantity { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "El costo unitario no puede ser negativo.")]
    [Display(Name = "Costo unitario")]
    public decimal UnitCost { get; set; }
}
