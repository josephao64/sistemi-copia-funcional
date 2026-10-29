using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Orders;
using SistemiPos.Domain.Organizations;

namespace SistemiPos.Application.Orders;

public sealed record SaveTerminalRequest(Guid WarehouseId, string Code, string RegisterCode);
public sealed record OrderLineRequest(Guid ProductId, decimal Quantity);
public sealed record CreateOrderRequest(Guid TerminalId, Guid CashSessionId, IReadOnlyList<OrderLineRequest> Lines);
public sealed record OrderActionRequest(OrderEventType Type, Guid? LineId, decimal Quantity,
    string OperationId, string? Reason = null, IReadOnlyList<OrderPackagingRequest>? ExtraPackaging = null);
public sealed record OrderPackagingRequest(Guid ProductId, decimal Quantity);
public sealed record ReviseOrderLineRequest(Guid ProductId, decimal Quantity, string OperationId, string Reason);
public sealed record TerminalDto(Guid Id, Guid BranchId, Guid WarehouseId, string Code, string RegisterCode);
public sealed record OrderLineDto(Guid Id, Guid ProductId, string ProductName, decimal Quantity,
    decimal UnitPrice, decimal GrossAmount, decimal TaxPercent, bool IsTaxExempt, int? RecipeRevision,
    bool RequiresPreparation, decimal Pending, decimal Preparing, decimal Prepared, decimal Dispatched,
    decimal Cancelled, decimal CancelledAfterPreparation, decimal Sold = 0, decimal Sellable = 0);
public sealed record OrderEventDto(Guid Id, OrderEventType Type, Guid? LineId, decimal Quantity,
    string OperationId, DateTimeOffset OccurredAtUtc, string UserName, string? Reason);
public sealed record OrderDto(Guid Id, Guid TerminalId, Guid BranchId, Guid WarehouseId, Guid CashSessionId,
    bool IsConfirmed, int Revision, IReadOnlyList<OrderLineDto> Lines, IReadOnlyList<OrderEventDto> Events);

public interface IOrderService
{
    Task<IReadOnlyList<TerminalDto>> GetTerminalsAsync(CancellationToken ct = default);
    Task<Guid> CreateTerminalAsync(SaveTerminalRequest request, CancellationToken ct = default);
    Task<TerminalDto> GetTerminalForEditAsync(Guid id, CancellationToken ct = default);
    Task UpdateTerminalAsync(Guid id, SaveTerminalRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<OrderDto>> GetOrdersAsync(CancellationToken ct = default);
    Task<OrderDto> GetOrderAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct = default);
    Task ApplyAsync(Guid id, OrderActionRequest request, CancellationToken ct = default);
    Task ReviseLineAsync(Guid id, Guid lineId, ReviseOrderLineRequest request, CancellationToken ct = default);
}

public interface IOrderRepository
{
    Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct);
    Task<List<PosTerminal>> GetTerminalsAsync(Guid branchId, CancellationToken ct);
    Task<PosTerminal?> GetTerminalAsync(Guid id, CancellationToken ct);
    Task<Warehouse?> GetWarehouseAsync(Guid id, CancellationToken ct);
    Task AddTerminalAsync(PosTerminal terminal, CancellationToken ct);
    Task<bool> TerminalCodeExistsAsync(Guid branchId, string code, Guid? excludingId, CancellationToken ct) => throw new NotSupportedException();
    Task<bool> HasTerminalOperationsAsync(Guid terminalId, Guid branchId, string currentRegister, string targetRegister, CancellationToken ct) => throw new NotSupportedException();
    Task<List<RestaurantOrder>> GetOrdersAsync(Guid branchId, CancellationToken ct);
    Task<RestaurantOrder?> GetOrderAsync(Guid id, CancellationToken ct);
    Task AddOrderAsync(RestaurantOrder order, CancellationToken ct);
    Task TrackNewDetailsAsync(RestaurantOrder order, CancellationToken ct);
    Task<Product?> GetProductAsync(Guid id, CancellationToken ct);
    Task<InventoryStock?> GetStockAsync(Guid productId, Guid warehouseId, CancellationToken ct);
    Task<Dictionary<Guid, decimal>> GetStocksAsync(Guid warehouseId, CancellationToken ct) => throw new NotSupportedException();
    Task AddMovementAsync(InventoryMovement movement, CancellationToken ct);
}
