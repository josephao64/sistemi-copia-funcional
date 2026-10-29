using SistemiPos.Application.Inventory;

namespace SistemiPos.Web.Models.Catalog;

public sealed class ExpiringProductsViewModel
{
    public IReadOnlyList<InventoryExpiryLotDto> Lots { get; set; } = [];
    public Guid? ProductId { get; set; }
    public string? Category { get; set; }
    public string? Status { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}
