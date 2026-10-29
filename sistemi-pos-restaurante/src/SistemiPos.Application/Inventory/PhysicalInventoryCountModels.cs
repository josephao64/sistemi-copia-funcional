using SistemiPos.Domain.Inventory;

namespace SistemiPos.Application.Inventory;

public sealed record PhysicalInventoryCategoryOption(Guid Id, string Name);

public sealed record CreatePhysicalInventoryCountRequest(
    string Name,
    PhysicalInventoryCountScope Scope,
    IReadOnlyCollection<Guid> CategoryIds,
    Guid? WarehouseId = null);

public sealed record PhysicalInventoryCountListItemDto(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAtUtc,
    string CreatedBy,
    PhysicalInventoryCountScope Scope,
    IReadOnlyList<string> Categories,
    int TotalProducts,
    int CountedProducts,
    int PendingProducts,
    PhysicalInventoryCountStatus Status);

public sealed record PhysicalInventoryCountSummaryDto(
    int TotalProducts,
    int CountedProducts,
    int PendingProducts,
    int CorrectProducts,
    int ShortageProducts,
    int SurplusProducts,
    int RecountRequiredProducts);

public sealed record PhysicalInventoryCountItemDto(
    Guid Id,
    Guid ProductId,
    string InternalCode,
    string ProductName,
    string CategoryName,
    decimal CurrentOperationalStock,
    decimal? ReferenceStock,
    decimal? CountedStock,
    decimal? Difference,
    string? Comment,
    DateTimeOffset? CountedAtUtc,
    string? CountedBy,
    PhysicalInventoryCountItemStatus Status,
    decimal? OperationalStockBeforeAdjustment,
    decimal? AppliedAdjustment,
    decimal? OperationalStockAfterAdjustment);

public sealed record PhysicalInventoryCountDetailDto(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAtUtc,
    string CreatedBy,
    PhysicalInventoryCountScope Scope,
    IReadOnlyList<string> Categories,
    PhysicalInventoryCountStatus Status,
    DateTimeOffset? FinalizedAtUtc,
    string? FinalizedBy,
    PhysicalInventoryCountSummaryDto Summary,
    IReadOnlyList<PhysicalInventoryCountItemDto> Items);

public sealed record BeginPhysicalInventoryCountItemResult(
    Guid ItemId,
    decimal ReferenceStock,
    PhysicalInventoryCountItemStatus Status,
    PhysicalInventoryCountSummaryDto Summary);

public sealed record SavePhysicalInventoryCountItemResult(
    bool Saved,
    string Message,
    PhysicalInventoryCountItemDto Item,
    PhysicalInventoryCountSummaryDto Summary);

public sealed record SavePhysicalInventoryCountItemRequest(int CountedStock, string? Comment);

public sealed record PhysicalInventoryCountExportDto(
    PhysicalInventoryCountDetailDto Count,
    decimal TotalPositiveDifference,
    decimal TotalNegativeDifference);
