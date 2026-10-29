using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Organizations;
using SistemiPos.Domain.Recipes;

namespace SistemiPos.Application.Recipes;

public sealed record RecipeCost(decimal UnitCost, string Source, bool Known = true);
public sealed record RecipeAvailabilityLine(RecipeComponentSnapshot Component, decimal Available,
    decimal Required, RecipeCost Cost);
public sealed record RecipeQuote(RecipeVersion Version, Guid WarehouseId, DateTimeOffset CostAtUtc,
    decimal Quantity, decimal TotalCost, decimal UnitCost, decimal AvailableOutput,
    string LimitingComponent, IReadOnlyList<RecipeAvailabilityLine> Lines);
public sealed record ProductionRequest(Guid OperationId, Guid RecipeId, Guid OriginWarehouseId,
    Guid DestinationWarehouseId, decimal PlannedQuantity, string LotNumber, DateOnly? ExpiryDate,
    DateTimeOffset ScheduledAtUtc);
public sealed record SaleRecipePlan(Guid VersionId, int VersionNumber, decimal UnitCost,
    IReadOnlyList<RecipeConsumption> Components);

public interface IRecipeOperationsRepository
{
    Task<List<RecipeVersion>> VersionsAsync(Guid recipeId, CancellationToken ct);
    Task<RecipeVersion?> VersionAsync(Guid id, CancellationToken ct);
    Task<RecipeVersion?> ActiveVersionAsync(Guid productId, DateTimeOffset at, CancellationToken ct);
    Task<List<ProductionOrder>> OrdersAsync(CancellationToken ct);
    Task<ProductionOrder?> OrderAsync(Guid id, CancellationToken ct);
    Task<ProductionOrder?> OperationAsync(Guid id, CancellationToken ct);
    Task AddOrderAsync(ProductionOrder order, CancellationToken ct);
    Task<Product?> ProductAsync(Guid id, CancellationToken ct);
    Task<InventoryStock> StockAsync(Guid productId, Guid warehouseId, CancellationToken ct);
    Task<decimal> AvailableAsync(Guid productId, Guid warehouseId, DateTimeOffset now, CancellationToken ct);
    Task<RecipeCost?> CostAsync(Guid productId, DateTimeOffset at, CancellationToken ct);
    Task AddMovementAsync(InventoryMovement movement, CancellationToken ct);
}

public interface IRecipeOperationsService
{
    Task<IReadOnlyList<Warehouse>> WarehousesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RecipeVersion>> VersionsAsync(Guid recipeId, CancellationToken ct = default);
    Task<RecipeQuote> QuoteAsync(Guid versionId, Guid warehouseId, decimal quantity, DateTimeOffset? at = null, CancellationToken ct = default);
    Task<IReadOnlyList<ProductionOrder>> OrdersAsync(CancellationToken ct = default);
    Task<ProductionOrder> OrderAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreateAsync(ProductionRequest request, CancellationToken ct = default);
    Task ConfirmAsync(Guid id, uint version, decimal actualQuantity, string? reason, CancellationToken ct = default);
    Task<IReadOnlyList<SistemiPos.Application.Catalog.ProductDto>> SaleCatalogAsync(IReadOnlyList<SistemiPos.Application.Catalog.ProductDto> products, Guid? branchId, CancellationToken ct = default);
    Task<SaleRecipePlan?> PlanSaleAsync(Guid productId, decimal quantity, Guid warehouseId, DateTimeOffset at, CancellationToken ct = default);
}
