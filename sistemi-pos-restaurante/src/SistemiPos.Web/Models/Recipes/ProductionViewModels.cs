using System.ComponentModel.DataAnnotations;
using SistemiPos.Application.Recipes;
using SistemiPos.Domain.Organizations;
using SistemiPos.Domain.Recipes;
namespace SistemiPos.Web.Models.Recipes;
public sealed class ProductionFormViewModel
{
    public Guid OperationId { get; set; } = Guid.NewGuid();
    public Guid RecipeId { get; set; }
    public Guid OriginWarehouseId { get; set; }
    public Guid DestinationWarehouseId { get; set; }
    [Range(typeof(decimal), "0.000001", "999999999999.999999")]
    public decimal PlannedQuantity { get; set; } = 1;
    [Required, StringLength(100)] public string LotNumber { get; set; } = "";
    public DateOnly? ExpiryDate { get; set; }
    public DateTime ScheduledAt { get; set; } = DateTime.UtcNow.AddHours(-6);
    public IReadOnlyList<RecipeDto> Recipes { get; set; } = [];
    public IReadOnlyList<Warehouse> Warehouses { get; set; } = [];
}
public sealed record RecipeHistoryViewModel(Guid RecipeId, IReadOnlyList<RecipeVersion> Versions,
    IReadOnlyList<Warehouse> Warehouses, Guid? VersionId, Guid? WarehouseId, decimal Quantity, DateTime? CostDate,
    RecipeQuote? Quote, string? Error);
