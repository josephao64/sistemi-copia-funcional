using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Catalog;

public sealed class Product : AuditableEntity
{
    private readonly List<ProductPriceRule> _priceRules = [];
    private readonly List<ProductVariantOption> _variantOptions = [];
    private readonly List<ProductBranchSetting> _branchSettings = [];
    private readonly List<ProductPurchasePresentation> _purchasePresentations = [];
    private readonly List<ProductUnitConversion> _unitConversions = [];

    public string InternalCode { get; private set; } = string.Empty;
    public string? Barcode { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? SupplierInvoiceName { get; private set; }
    public string? SupplierDescription { get; private set; }
    public Guid? ParentProductId { get; private set; }
    public Product? ParentProduct { get; set; }
    public string? VariantValue { get; private set; }
    public string DisplayName
    {
        get
        {
            var attributes = string.Join(" / ", _variantOptions
                .OrderBy(x => x.SortOrder)
                .Select(x => FormatVariantLabel(x.VariantOption?.VariantType?.Name, x.VariantOption?.Value))
                .Where(x => !string.IsNullOrWhiteSpace(x)));
            var suffix = string.IsNullOrWhiteSpace(attributes) ? VariantValue : attributes;
            return string.IsNullOrWhiteSpace(suffix) ? Name : $"{Name} {suffix}";
        }
    }

    public static string FormatVariantLabel(string? attribute, string? value) =>
        string.IsNullOrWhiteSpace(attribute) || string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : $"{NameNormalizer.Normalize(attribute)} - {NameNormalizer.Normalize(value)}";
    public bool ManagesVariants { get; private set; }
    public bool ManagesExpiry { get; private set; }
    public bool ManagesLots { get; private set; } = true;
    public int? ExpiryAlertDays { get; private set; }
    public string? Description { get; private set; }
    public string? ImageUrl { get; private set; }
    public Guid CategoryId { get; private set; }
    public Category Category { get; set; } = null!;
    public string Brand { get; private set; } = string.Empty;
    public string UnitOfMeasure { get; private set; } = string.Empty;
    public decimal LastCost { get; private set; }
    public decimal PreviousCost { get; private set; }
    public decimal SalePrice { get; private set; }
    public decimal MinimumStock { get; private set; }
    public bool AppliesTax { get; private set; } = true;
    public bool IsActive { get; private set; } = true;
    public int? NoMovementDaysOverride { get; private set; }
    public bool MultiPriceEnabled { get; private set; }
    public MultiPriceMode MultiPriceMode { get; private set; } = MultiPriceMode.MinimumQuantity;
    public ArticleType ArticleType { get; private set; } = ArticleType.Reventa;
    public Guid? BaseUnitId { get; private set; }
    public UnitOfMeasure? BaseUnit { get; set; }
    public IReadOnlyCollection<ProductPriceRule> PriceRules => _priceRules;
    public IReadOnlyCollection<ProductVariantOption> VariantOptions => _variantOptions;
    public IReadOnlyCollection<ProductBranchSetting> BranchSettings => _branchSettings;
    public IReadOnlyCollection<ProductPurchasePresentation> PurchasePresentations => _purchasePresentations;
    public IReadOnlyCollection<ProductUnitConversion> UnitConversions => _unitConversions;

    private Product()
    {
    }

    public Product(
        string internalCode,
        string? barcode,
        string name,
        string? description,
        Guid categoryId,
        string brand,
        string unitOfMeasure,
        decimal lastCost,
        decimal previousCost,
        decimal salePrice,
        decimal minimumStock,
        bool appliesTax,
        int? noMovementDaysOverride = null)
    {
        Update(internalCode, barcode, name, description, categoryId, brand, unitOfMeasure, lastCost, previousCost, salePrice, minimumStock, appliesTax, noMovementDaysOverride);
    }

    public void Update(
        string internalCode,
        string? barcode,
        string name,
        string? description,
        Guid categoryId,
        string brand,
        string unitOfMeasure,
        decimal lastCost,
        decimal previousCost,
        decimal salePrice,
        decimal minimumStock,
        bool appliesTax,
        int? noMovementDaysOverride = null)
    {
        if (string.IsNullOrWhiteSpace(internalCode))
        {
            throw new DomainRuleException("El código interno es requerido.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("El nombre del producto es requerido.");
        }

        if (categoryId == Guid.Empty)
        {
            throw new DomainRuleException("La categoría es requerida.");
        }

        if (string.IsNullOrWhiteSpace(brand))
        {
            throw new DomainRuleException("La marca es requerida.");
        }

        if (string.IsNullOrWhiteSpace(unitOfMeasure))
        {
            throw new DomainRuleException("La unidad de medida es requerida.");
        }

        if (lastCost < 0 || previousCost < 0 || salePrice < 0 || minimumStock < 0)
        {
            throw new DomainRuleException("Los valores monetarios y de stock mínimo no pueden ser negativos.");
        }

        if (noMovementDaysOverride is <= 0)
        {
            throw new DomainRuleException("Los días sin movimiento del producto deben ser mayores a cero.");
        }

        InternalCode = internalCode.Trim();
        Barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();
        Name = NameNormalizer.Normalize(name);
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        CategoryId = categoryId;
        Brand = NameNormalizer.Normalize(brand);
        UnitOfMeasure = NameNormalizer.Normalize(unitOfMeasure);
        LastCost = decimal.Round(lastCost, 6);
        PreviousCost = decimal.Round(previousCost, 6);
        SalePrice = decimal.Round(salePrice, 2);
        MinimumStock = decimal.Round(minimumStock, 2);
        AppliesTax = appliesTax;
        NoMovementDaysOverride = noMovementDaysOverride;
    }

    public void UpdateCommercialData(
        string? barcode,
        string name,
        string? description,
        decimal lastCost,
        decimal previousCost,
        decimal salePrice,
        decimal minimumStock,
        bool appliesTax,
        int? noMovementDaysOverride = null) =>
        Update(InternalCode, barcode, name, description, CategoryId, Brand, UnitOfMeasure,
            lastCost, previousCost, salePrice, minimumStock, appliesTax, noMovementDaysOverride);

    public void SetSupplierInvoiceName(string? supplierInvoiceName) =>
        SupplierInvoiceName = NameNormalizer.NormalizeOptional(supplierInvoiceName);

    public void SetSupplierDescription(string? supplierDescription) =>
        SupplierDescription = string.IsNullOrWhiteSpace(supplierDescription) ? null : supplierDescription.Trim();

    public void SetImageUrl(string? imageUrl) =>
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }

    public void SetVariant(Guid? parentProductId, string? variantValue)
    {
        if (parentProductId is null && !string.IsNullOrWhiteSpace(variantValue))
        {
            throw new DomainRuleException("La variante debe pertenecer a un producto base.");
        }

        if (parentProductId is not null && string.IsNullOrWhiteSpace(variantValue))
        {
            throw new DomainRuleException("El valor de la variante es requerido.");
        }

        ParentProductId = parentProductId;
        VariantValue = string.IsNullOrWhiteSpace(variantValue) ? null : variantValue.Trim();
    }

    public void SetManagesVariants(bool managesVariants)
    {
        if (ParentProductId is not null && managesVariants)
            throw new DomainRuleException("Una combinación no puede administrar otras variantes.");
        ManagesVariants = managesVariants;
    }

    public void ConfigureExpiry(bool managesExpiry, int? expiryAlertDays) =>
        ConfigureInventoryTracking(ManagesLots, managesExpiry, expiryAlertDays);

    public void ConfigureInventoryTracking(bool managesLots, bool managesExpiry, int? expiryAlertDays)
    {
        if (managesExpiry && !managesLots)
            throw new DomainRuleException("El control de caducidad requiere control por lote.");
        if (managesExpiry && expiryAlertDays is not > 0)
            throw new DomainRuleException("Los días de alerta de caducidad deben ser mayores a cero.");

        ManagesLots = managesLots;
        ManagesExpiry = managesExpiry;
        ExpiryAlertDays = managesExpiry ? expiryAlertDays : null;
    }

    public void ConfigureLotTracking(bool managesLots) =>
        ConfigureInventoryTracking(managesLots, ManagesExpiry, ExpiryAlertDays);

    public IReadOnlyList<ProductVariantOption> AssignVariantOptions(IEnumerable<(Guid OptionId, int SortOrder)> options)
    {
        var values = options.ToList();
        if (values.Any(x => x.OptionId == Guid.Empty) || values.Select(x => x.OptionId).Distinct().Count() != values.Count)
            throw new DomainRuleException("La combinación de variantes no es válida.");
        var retained = values.Select(x => x.OptionId).ToHashSet();
        _variantOptions.RemoveAll(x => !retained.Contains(x.VariantOptionId));

        var created = new List<ProductVariantOption>();
        foreach (var value in values)
        {
            var existing = _variantOptions.FirstOrDefault(x => x.VariantOptionId == value.OptionId);
            if (existing is not null)
            {
                existing.SetSortOrder(value.SortOrder);
                continue;
            }

            var item = new ProductVariantOption(Id, value.OptionId, value.SortOrder);
            _variantOptions.Add(item);
            created.Add(item);
        }
        return created;
    }

    public void ConfigureMultiPricing(bool enabled, MultiPriceMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new DomainRuleException("El tipo de multiprecio no es válido.");
        }

        MultiPriceEnabled = enabled;
        MultiPriceMode = mode;
    }

    public void SynchronizePriceRules(IEnumerable<ProductPriceRuleValue> rules)
    {
        var values = rules.ToList();
        if (values.GroupBy(x => decimal.Round(x.Quantity, 2)).Any(x => x.Count() > 1))
        {
            throw new DomainRuleException("No se permiten cantidades repetidas en las reglas de precio del producto.");
        }

        var retainedIds = new HashSet<Guid>();
        foreach (var value in values)
        {
            if (value.Id is Guid id && id != Guid.Empty)
            {
                var existing = _priceRules.FirstOrDefault(x => x.Id == id)
                    ?? throw new DomainRuleException("La regla de precio no pertenece al producto.");
                existing.Update(value.Quantity, value.Price, value.IsActive);
                retainedIds.Add(existing.Id);
                continue;
            }

            var rule = new ProductPriceRule(Id, value.Quantity, value.Price, value.IsActive);
            _priceRules.Add(rule);
            retainedIds.Add(rule.Id);
        }

        _priceRules.RemoveAll(x => !retainedIds.Contains(x.Id));
    }

    public void SetArticleType(ArticleType articleType)
    {
        if (!Enum.IsDefined(articleType))
            throw new DomainRuleException("El tipo de artículo seleccionado no es válido.");
        ArticleType = articleType;
    }

    public void SetBaseUnit(Guid? baseUnitId, string? unitOfMeasureName = null)
    {
        BaseUnitId = baseUnitId;
        if (!string.IsNullOrWhiteSpace(unitOfMeasureName))
        {
            UnitOfMeasure = unitOfMeasureName.Trim();
        }
    }
}

public sealed record ProductPriceRuleValue(Guid? Id, decimal Quantity, decimal Price, bool IsActive);
