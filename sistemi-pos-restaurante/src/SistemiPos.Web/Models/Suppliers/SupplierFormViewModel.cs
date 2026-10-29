using System.ComponentModel.DataAnnotations;

namespace SistemiPos.Web.Models.Suppliers;

public sealed class SupplierFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "El nombre o razón social es requerido.")]
    [Display(Name = "Razón social")]
    public string LegalName { get; set; } = string.Empty;

    [Display(Name = "NIT")]
    public string Nit { get; set; } = string.Empty;

    [Display(Name = "Nombre comercial")]
    public string? TradeName { get; set; }

    [Display(Name = "Teléfono empresa")]
    public string? Phone { get; set; }

    [Display(Name = "Correo")]
    [EmailAddress(ErrorMessage = "El correo no es válido.")]
    public string? Email { get; set; }

    [Display(Name = "Dirección")]
    public string? Address { get; set; }

    [Display(Name = "Contacto principal")]
    public string? ContactName { get; set; }

    [Display(Name = "Cargo")]
    public string? ContactPosition { get; set; }

    [Display(Name = "Teléfono contacto")]
    public string? ContactPhone { get; set; }

    [Display(Name = "Condición habitual")]
    public SistemiPos.Domain.Purchases.PurchaseTermsType DefaultTerms { get; set; } = SistemiPos.Domain.Purchases.PurchaseTermsType.Cash;

    [Display(Name = "Días de crédito")]
    [Range(1, int.MaxValue, ErrorMessage = "Los días de crédito deben ser mayores a cero.")]
    public int? DefaultCreditDays { get; set; }

    [Display(Name = "Notas")]
    [StringLength(1000)]
    public string? Notes { get; set; }

    [Display(Name = "Proveedor activo")]
    public bool IsActive { get; set; } = true;
}
