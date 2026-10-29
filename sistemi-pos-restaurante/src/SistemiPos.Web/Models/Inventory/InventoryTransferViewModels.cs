using Microsoft.AspNetCore.Mvc.Rendering;
using SistemiPos.Application.Inventory;
using SistemiPos.Domain.Inventory;

namespace SistemiPos.Web.Models.Inventory;

public sealed class InventoryTransferIndexViewModel
{
    public IReadOnlyList<InventoryTransferDto> Transfers { get; init; } = [];
    public Guid? OriginWarehouseId { get; init; }
    public Guid? DestinationWarehouseId { get; init; }
    public InventoryTransferStatus? Status { get; init; }
    public IReadOnlyList<SelectListItem> Warehouses { get; init; } = [];
}

public sealed class CreateInventoryTransferViewModel
{
    public Guid? OriginWarehouseId { get; set; }
    public Guid? DestinationWarehouseId { get; set; }
    public string? Observations { get; set; }
    public List<CreateTransferItemInputModel> Items { get; set; } = [];
    public IReadOnlyList<SelectListItem> Warehouses { get; set; } = [];
}

public sealed class CreateTransferItemInputModel
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCostOrigin { get; set; }
    public decimal? AssignedUnitCost { get; set; }
}

public sealed class InventoryTransferDetailViewModel
{
    public InventoryTransferDto Transfer { get; init; } = null!;
    public bool CanDispatch { get; init; }
    public bool CanReceive { get; init; }
    public bool CanCancel { get; init; }
}
