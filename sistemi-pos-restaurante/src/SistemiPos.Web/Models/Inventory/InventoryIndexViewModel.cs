using SistemiPos.Application.Inventory;
using SistemiPos.Application.Reports;

namespace SistemiPos.Web.Models.Inventory;

public enum InventoryListFilter
{
    All,
    LowStock,
    OutOfStock,
    WithoutMovement
}

public sealed class InventoryIndexViewModel
{
    public InventoryListFilter Filter { get; init; }
    public IReadOnlyList<InventoryStockDto> Stocks { get; init; } = [];
    public IReadOnlyDictionary<Guid, NoMovementProductRow> NoMovementDetails { get; init; }
        = new Dictionary<Guid, NoMovementProductRow>();
    public Guid? SelectedBranchId { get; init; }
    public Guid? SelectedWarehouseId { get; init; }
    public IReadOnlyList<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> AvailableBranches { get; init; } = [];
    public IReadOnlyList<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> AvailableWarehouses { get; init; } = [];
}
