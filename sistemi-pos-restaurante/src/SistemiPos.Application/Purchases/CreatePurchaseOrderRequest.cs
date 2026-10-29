namespace SistemiPos.Application.Purchases;

public sealed record CreatePurchaseOrderRequest(
    Guid SupplierId,
    IReadOnlyList<PurchaseOrderItemRequest> Items,
    string? Observations = null,
    bool Submit = false,
    Guid? BranchId = null,
    Guid? WarehouseId = null);

public sealed record PurchaseOrderItemRequest(Guid ProductId, decimal PackageQuantity, Guid PresentationId = default);

public sealed record UpdatePurchaseOrderRequest(
    Guid SupplierId,
    IReadOnlyList<PurchaseOrderItemRequest> Items,
    string? Observations,
    bool Submit,
    Guid? BranchId = null,
    Guid? WarehouseId = null);
