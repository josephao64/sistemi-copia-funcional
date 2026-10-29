using System.ComponentModel.DataAnnotations;
using SistemiPos.Application.Catalog;
using SistemiPos.Application.Orders;
using SistemiPos.Application.Organizations;

namespace SistemiPos.Web.Models.Orders;

public sealed class TerminalListViewModel
{
    public string BranchName { get; init; } = string.Empty;
    public IReadOnlyList<TerminalDto> Terminals { get; init; } = [];
    public IReadOnlyList<WarehouseDto> Warehouses { get; init; } = [];
}

public sealed class NewOrderViewModel
{
    [Required] public Guid TerminalId { get; set; }
    public List<NewOrderLineViewModel> Lines { get; set; } = [new()];
    public IReadOnlyList<TerminalDto> Terminals { get; set; } = [];
    public IReadOnlyList<ProductDto> Products { get; set; } = [];
}
public sealed class NewOrderLineViewModel
{
    [Required] public Guid ProductId { get; set; }
    [Range(typeof(decimal), "0.01", "999999999999")] public decimal Quantity { get; set; } = 1;
}
public sealed class TerminalFormViewModel
{
    public Guid? Id { get; set; }
    [Required, StringLength(50)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(50)] public string RegisterCode { get; set; } = string.Empty;
    [Required] public Guid WarehouseId { get; set; }
    public IReadOnlyList<WarehouseDto> Warehouses { get; set; } = [];
}
