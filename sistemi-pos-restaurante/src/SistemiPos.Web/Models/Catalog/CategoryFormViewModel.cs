using System.ComponentModel.DataAnnotations;

namespace SistemiPos.Web.Models.Catalog;

public sealed class CategoryFormViewModel
{
    public Guid? Id { get; set; }
    public string? ReturnUrl { get; set; }

    [Required(ErrorMessage = "El nombre es requerido.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Descripción")]
    public string? Description { get; set; }

    [Display(Name = "Activa")]
    public bool IsActive { get; set; } = true;
}
