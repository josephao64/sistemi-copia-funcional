namespace SistemiPos.Application.Inventory;

public sealed record InventoryMovementPage(
    IReadOnlyList<InventoryMovementDto> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}
