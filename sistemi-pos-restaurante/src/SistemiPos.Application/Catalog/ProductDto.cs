using SistemiPos.Domain.Catalog;

namespace SistemiPos.Application.Catalog;

public sealed record ProductDto(
    Guid Id,
    string InternalCode,
    string? Barcode,
    string Name,
    string? Description,
    Guid CategoryId,
    string CategoryName,
    string Brand,
    string UnitOfMeasure,
    decimal LastCost,
    decimal PreviousCost,
    decimal SalePrice,
    decimal MinimumStock,
    bool AppliesTax,
    decimal CurrentStock,
    bool IsActive,
    int? NoMovementDaysOverride = null,
    bool MultiPriceEnabled = false,
    MultiPriceMode MultiPriceMode = MultiPriceMode.MinimumQuantity,
    IReadOnlyList<ProductPriceRuleDto>? PriceRules = null,
    Guid? ParentProductId = null,
    string? VariantValue = null,
    string? DisplayName = null,
    bool ManagesVariants = false,
    IReadOnlyList<ProductVariantOptionDto>? VariantOptions = null,
    string? SupplierInvoiceName = null,
    string? SupplierDescription = null,
    bool ManagesExpiry = false,
    int? ExpiryAlertDays = null,
    string? ImageUrl = null,
    ArticleType ArticleType = ArticleType.Reventa,
    Guid? BaseUnitId = null,
    string? BaseUnitName = null,
    string? BaseUnitCode = null,
    IReadOnlyList<ProductBranchSettingDto>? BranchSettings = null,
    IReadOnlyList<ProductPurchasePresentationDto>? PurchasePresentations = null,
    IReadOnlyList<ProductUnitConversionDto>? UnitConversions = null,
    bool ManagesLots = true,
    bool IsVisible = true);

public sealed record ProductVariantOptionDto(Guid OptionId, Guid TypeId, string TypeName, string Value, int SortOrder);
public sealed record VariantTypeDto(Guid Id, string Name, IReadOnlyList<VariantOptionDto> Options);
public sealed record VariantOptionDto(Guid Id, string Value);
