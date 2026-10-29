using SistemiPos.Domain.Catalog;

namespace SistemiPos.Application.Catalog;

public sealed record SaveProductRequest(
    string InternalCode,
    string? Barcode,
    string Name,
    string? Description,
    Guid CategoryId,
    string Brand,
    string UnitOfMeasure,
    decimal LastCost,
    decimal PreviousCost,
    decimal SalePrice,
    decimal MinimumStock,
    bool AppliesTax,
    bool IsActive,
    int? NoMovementDaysOverride = null,
    bool MultiPriceEnabled = false,
    MultiPriceMode MultiPriceMode = MultiPriceMode.MinimumQuantity,
    IReadOnlyList<SaveProductPriceRuleRequest>? PriceRules = null,
    Guid? ParentProductId = null,
    string? VariantValue = null,
    bool ManagesVariants = false,
    IReadOnlyList<SaveVariantGroupRequest>? VariantGroups = null,
    IReadOnlyList<SaveProductCombinationRequest>? Combinations = null,
    IReadOnlyList<SaveProductAttributeRequest>? Attributes = null,
    string? SupplierInvoiceName = null,
    string? SupplierDescription = null,
    bool ManagesExpiry = false,
    int? ExpiryAlertDays = null,
    string? ImageUrl = null,
    ArticleType ArticleType = ArticleType.Reventa,
    Guid? BaseUnitId = null,
    IReadOnlyList<SaveProductBranchSettingRequest>? BranchSettings = null,
    IReadOnlyList<SavePurchasePresentationRequest>? PurchasePresentations = null,
    IReadOnlyList<SaveUnitConversionRequest>? Conversions = null,
    bool ManagesLots = true);

public sealed record SaveProductAttributeRequest(string TypeName, string Value);

public sealed record SaveVariantGroupRequest(string TypeName, IReadOnlyList<string> Values);

public sealed record SaveProductCombinationRequest(
    Guid? Id,
    string Key,
    string? Barcode,
    decimal LastCost,
    decimal SalePrice,
    decimal MinimumStock,
    bool IsActive,
    string? ImageUrl = null);
