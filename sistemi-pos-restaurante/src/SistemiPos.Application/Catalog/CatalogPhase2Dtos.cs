using SistemiPos.Domain.Catalog;

namespace SistemiPos.Application.Catalog;

public sealed record UnitOfMeasureDto(
    Guid Id,
    string Code,
    string Name,
    UnitDimension Dimension,
    string Symbol,
    int Precision,
    bool IsActive);

public sealed record SaveUnitOfMeasureRequest(
    string Code,
    string Name,
    UnitDimension Dimension,
    string Symbol,
    int Precision = 2,
    bool IsActive = true);

public sealed record ProductUnitConversionDto(
    Guid Id,
    Guid? ProductId,
    Guid FromUnitId,
    string FromUnitCode,
    string FromUnitName,
    Guid ToUnitId,
    string ToUnitCode,
    string ToUnitName,
    decimal Factor,
    bool IsActive);

public sealed record SaveUnitConversionRequest(
    Guid? ProductId,
    Guid FromUnitId,
    Guid ToUnitId,
    decimal Factor,
    bool IsActive = true);

public sealed record ProductPurchasePresentationDto(
    Guid Id,
    Guid ProductId,
    string Name,
    Guid? PurchaseUnitId,
    string? PurchaseUnitName,
    decimal BaseQuantityPerPackage,
    bool IsActive);

public sealed record SavePurchasePresentationRequest(
    Guid? Id,
    string Name,
    decimal BaseQuantityPerPackage,
    Guid? PurchaseUnitId = null,
    bool IsActive = true);

public sealed record ProductBranchSettingDto(
    Guid Id,
    Guid ProductId,
    Guid BranchId,
    string BranchName,
    bool IsEnabled,
    bool IsVisible,
    decimal? SalePrice,
    decimal EffectiveSalePrice,
    decimal? MinimumStock,
    decimal EffectiveMinimumStock);

public sealed record SaveProductBranchSettingRequest(
    Guid BranchId,
    bool IsEnabled,
    bool IsVisible,
    decimal? SalePrice,
    decimal? MinimumStock);

public sealed record CatalogChangeLogDto(
    Guid Id,
    Guid ProductId,
    string ProductInternalCode,
    Guid? BranchId,
    string? BranchName,
    string Field,
    string? OldValue,
    string? NewValue,
    string Actor,
    DateTimeOffset TimestampUtc,
    string? Reason);
