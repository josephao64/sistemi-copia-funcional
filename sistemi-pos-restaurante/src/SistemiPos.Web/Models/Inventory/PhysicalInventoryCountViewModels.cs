using System.ComponentModel.DataAnnotations;
using SistemiPos.Application.Inventory;
using SistemiPos.Domain.Inventory;

namespace SistemiPos.Web.Models.Inventory;

public sealed class CreatePhysicalInventoryCountViewModel
{
    [Required(ErrorMessage = "El nombre de la toma es requerido.")]
    [StringLength(200, ErrorMessage = "El nombre no puede superar 200 caracteres.")]
    [Display(Name = "Nombre de la toma")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Alcance")]
    public PhysicalInventoryCountScope Scope { get; set; } = PhysicalInventoryCountScope.AllInventory;

    public List<Guid> CategoryIds { get; set; } = [];
}

public sealed class PhysicalInventoryCountIndexViewModel
{
    public CreatePhysicalInventoryCountViewModel Create { get; init; } = new();
    public IReadOnlyList<PhysicalInventoryCategoryOption> Categories { get; init; } = [];
    public IReadOnlyList<PhysicalInventoryCountListItemDto> Counts { get; init; } = [];
}

public enum PhysicalInventoryCountFilter
{
    All,
    Pending,
    Counted,
    Difference,
    Shortage,
    Surplus,
    RecountRequired
}

public sealed class PhysicalInventoryCountPageViewModel
{
    public PhysicalInventoryCountDetailDto Count { get; init; } = null!;
    public IReadOnlyList<PhysicalInventoryCountItemDto> VisibleItems { get; init; } = [];
    public string? Search { get; init; }
    public string? Category { get; init; }
    public PhysicalInventoryCountFilter Filter { get; init; }
}
