using System.ComponentModel.DataAnnotations;

namespace SistemiPos.Web.Models.Customers;

public sealed class CustomerFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "El nombre es requerido.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "NIT")]
    public string? Nit { get; set; }

    [Display(Name = "Teléfono")]
    public string? Phone { get; set; }

    [Display(Name = "¿De dónde nos visita?")]
    public string? VisitOrigin { get; set; }

    [Display(Name = "Correo")]
    [EmailAddress(ErrorMessage = "El correo no es válido.")]
    public string? Email { get; set; }

    [Display(Name = "Dirección")]
    public string? Address { get; set; }

    [Range(0, 3650, ErrorMessage = "Los días de crédito deben estar entre 0 y 3650.")]
    [Display(Name = "Días de crédito")]
    public int CreditDays { get; set; } = 30;

    [Range(0, 3650, ErrorMessage = "Los días de gracia deben estar entre 0 y 3650.")]
    [Display(Name = "Días adicionales de gracia")]
    public int GraceDays { get; set; }

    [Display(Name = "Activo")]
    public bool IsActive { get; set; } = true;
}
