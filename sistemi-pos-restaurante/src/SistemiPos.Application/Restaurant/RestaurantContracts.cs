using SistemiPos.Domain.Restaurant;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Organizations;
using SistemiPos.Domain.Recipes;

namespace SistemiPos.Application.Restaurant;

public sealed record OrderItemInput(Guid ProductId, decimal Quantity, List<Guid> OptionIds, List<Guid> ExcludedProducts);
public sealed record ComboPick(Guid GroupId, Guid ProductId, List<Guid>? OptionIds = null, List<Guid>? ExcludedProducts = null);
public sealed record ComboInput(Guid ComboId, List<ComboPick> Picks);
public sealed record OrderInput(Guid WarehouseId, Guid ChannelId, RestaurantServiceType ServiceType, Guid? TableId,
    string? ExternalReference, string CustomerName, string Phone, string Address, Guid? ZoneId, string Notes,
    List<OrderItemInput> Items, List<ComboInput> Combos);
public sealed record DraftCommand(Guid OperationId, Guid? OrderId, int Revision, OrderInput Order);
public sealed record OrderAction(Guid OperationId, int Revision, string Action, Guid? LineId = null,
    decimal Quantity = 0, string? Reason = null, Guid? TargetId = null, string? Route = null, bool ReturnIntact = false);
public sealed record AddOrderLines(Guid OperationId, int Revision, List<OrderItemInput> Items, List<ComboInput> Combos);
public sealed record RestaurantMenuProduct(Guid Id, string Name, string Code, string Category, string? ImageUrl,
    decimal Price, decimal Available, bool Favorite, RestaurantMenuItem Configuration);
public sealed record RestaurantWorkspace(Guid BranchId, string BranchName, int ConfigurationRevision,
    RestaurantDefinition Configuration, IReadOnlyList<RestaurantMenuProduct> Menu, IReadOnlyList<Warehouse> Warehouses,
    IReadOnlyList<TableTurn> Turns, IReadOnlyList<RestaurantOrder> Orders, Guid? SelectedWarehouseId,
    IReadOnlyList<RestaurantComponentName> Components);
public sealed record RestaurantComponentName(Guid Id, string Name, decimal Quantity = 0, string? Unit = null, bool IsRequired = false);
public sealed record RestaurantMenuCandidate(Guid Id, string Name, string Code, string CatalogCategory, string Type,
    bool HasRecipe, bool CanExcludeComponents, IReadOnlyList<RestaurantComponentName> RecipeComponents,
    decimal BasePrice = 0, string? ImageUrl = null, string? RecipeInstructions = null);
public sealed record OrderCheckoutSnapshot(Guid OrderId, int Revision, Guid BranchId, Guid WarehouseId, decimal Total,
    decimal DeliveryFee, IReadOnlyList<OrderCheckoutLine> Lines);
public sealed record OrderCheckoutLine(Guid LineId, OrderLineSnapshot Snapshot, decimal ActiveQuantity,
    bool InventoryConsumed, IReadOnlyList<Guid> ConsumptionMovementIds);
public sealed record CopyMenuCommand(Guid SourceBranchId);

public interface IRestaurantService
{
    Task<RestaurantWorkspace> WorkspaceAsync(CancellationToken ct = default, Guid? warehouseId = null, bool includeMenu = true);
    Task<IReadOnlyList<Product>> ProductsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RestaurantMenuCandidate>> MenuCandidatesAsync(CancellationToken ct = default);
    Task SaveSetupAsync(RestaurantDefinition definition, int revision, CancellationToken ct = default);
    Task<RestaurantDefinition> CopyBranchMenuAsync(Guid sourceBranchId, CancellationToken ct = default);
    Task<RestaurantOrder> GetAsync(Guid id, CancellationToken ct = default);
    Task<Guid> SaveDraftAsync(DraftCommand command, CancellationToken ct = default);
    Task ActAsync(Guid id, OrderAction command, CancellationToken ct = default);
    Task AddLinesAsync(Guid id, AddOrderLines command, CancellationToken ct = default);
    Task<OrderCheckoutSnapshot> CheckoutAsync(Guid id, CancellationToken ct = default);
}

public interface IRestaurantRepository
{
    Task<T> TransactionAsync<T>(Func<Task<T>> action, CancellationToken ct);
    Task<RestaurantSetup?> SetupAsync(Guid branch, CancellationToken ct);
    Task<List<RestaurantOrder>> OrdersAsync(Guid branch, CancellationToken ct);
    Task<RestaurantOrder?> OrderAsync(Guid id, CancellationToken ct);
    Task<List<Product>> ProductsAsync(CancellationToken ct);
    Task<List<Warehouse>> WarehousesAsync(Guid branch, CancellationToken ct);
    Task<List<TableTurn>> TurnsAsync(Guid branch, CancellationToken ct);
    Task<List<OrderReservation>> ReservationsAsync(Guid warehouse, Guid? order, CancellationToken ct);
    Task<InventoryStock?> StockAsync(Guid product, Guid warehouse, CancellationToken ct);
    Task<List<InventoryExpiryLot>> LotsAsync(Guid product, Guid warehouse, CancellationToken ct);
    Task<RecipeVersion?> RecipeAsync(Guid product, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, RecipeSnapshot>> ActiveRecipeSnapshotsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct);
    Task<RestaurantCommand?> CommandAsync(Guid operation, CancellationToken ct);
    Task<RestaurantOrder?> ExternalAsync(Guid branch, Guid channel, string reference, CancellationToken ct);
    Task<string> NumberAsync(CancellationToken ct);
    Task<decimal> TaxAsync(CancellationToken ct);
    void Add(object entity);
    Task SaveAsync(CancellationToken ct);
}
