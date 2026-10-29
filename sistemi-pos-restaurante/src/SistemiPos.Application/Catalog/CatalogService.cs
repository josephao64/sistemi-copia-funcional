using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Common.Exceptions;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Inventory;

namespace SistemiPos.Application.Catalog;

public sealed class CatalogService(
    ICatalogRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUserAccessor? currentUserAccessor = null,
    IOperationContextAccessor? operationContext = null) : ICatalogService
{
    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categories = await repository.GetCategoriesAsync(cancellationToken);
        return categories
            .OrderBy(x => x.Name)
            .Select(MapCategory)
            .ToList();
    }

    public async Task<CategoryDto?> GetCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await repository.GetCategoryAsync(id, cancellationToken);
        return category is null ? null : MapCategory(category);
    }

    public async Task<Guid> CreateCategoryAsync(SaveCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedName = NameNormalizer.Normalize(request.Name);
        if (await repository.ExistsCategoryByNameAsync(normalizedName, null, cancellationToken))
        {
            throw new DomainRuleException("Ya existe una categoría con ese nombre.");
        }

        var category = new Category(normalizedName, request.Description);
        category.SetActive(request.IsActive);

        await repository.AddCategoryAsync(category, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return category.Id;
    }

    public async Task UpdateCategoryAsync(Guid id, SaveCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await repository.GetCategoryAsync(id, cancellationToken)
            ?? throw new NotFoundException("Categoría no encontrada.");

        var normalizedName = NameNormalizer.Normalize(request.Name);
        if (await repository.ExistsCategoryByNameAsync(normalizedName, id, cancellationToken))
        {
            throw new DomainRuleException("Ya existe una categoría con ese nombre.");
        }

        category.Rename(normalizedName, request.Description);
        category.SetActive(request.IsActive);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductDto>> SearchProductsAsync(string? query, CancellationToken cancellationToken = default) =>
        await SearchProductsAsync(query, operationContext?.CurrentBranchId, cancellationToken);

    public async Task<IReadOnlyList<ProductDto>> SearchProductsAsync(string? query, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var products = await repository.SearchProductsAsync(query, cancellationToken);
        if (products.Count == 0) return [];
        var quantities = await repository.GetStockQuantitiesAsync(products.Select(x => x.Id).ToArray(), cancellationToken);
        var results = new List<ProductDto>(products.Count);

        foreach (var product in products.OrderBy(x => x.Name))
        {
            results.Add(MapProduct(product, quantities.GetValueOrDefault(product.Id), branchId));
        }

        return results;
    }

    public async Task<IReadOnlyList<string>> GetProductBrandsAsync(CancellationToken cancellationToken = default) =>
        await repository.GetProductBrandsAsync(cancellationToken);

    public async Task<string> CreateBrandAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainRuleException("El nombre de la marca es requerido.");

        var normalizedName = NameNormalizer.Normalize(name);
        if (await repository.ExistsBrandByNameAsync(normalizedName, cancellationToken))
            throw new DomainRuleException("Ya existe una marca con ese nombre.");

        var brand = new Brand(normalizedName);
        await repository.AddBrandAsync(brand, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return brand.Name;
    }

    public async Task<ProductDto?> GetProductAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await repository.GetProductAsync(id, cancellationToken);
        if (product is null)
        {
            return null;
        }

        var stock = await repository.GetInventoryStockAsync(product.Id, cancellationToken);
        return MapProduct(product, stock?.CurrentStock ?? 0m, operationContext?.CurrentBranchId);
    }

    public async Task<ProductDto?> GetProductByBranchAsync(Guid id, Guid branchId, CancellationToken cancellationToken = default)
    {
        var product = await repository.GetProductAsync(id, cancellationToken);
        if (product is null)
        {
            return null;
        }

        var stock = await repository.GetInventoryStockAsync(product.Id, cancellationToken);
        return MapProduct(product, stock?.CurrentStock ?? 0m, branchId);
    }

    public async Task<IReadOnlyList<ProductDto>> GetProductVariantsAsync(Guid parentProductId, CancellationToken cancellationToken = default)
    {
        var products = await repository.GetProductVariantsAsync(parentProductId, cancellationToken);
        if (products.Count == 0) return [];
        var quantities = await repository.GetStockQuantitiesAsync(products.Select(x => x.Id).ToArray(), cancellationToken);
        var results = new List<ProductDto>(products.Count);
        foreach (var product in products.OrderBy(x => x.VariantValue))
        {
            results.Add(MapProduct(product, quantities.GetValueOrDefault(product.Id), operationContext?.CurrentBranchId));
        }
        return results;
    }

    public async Task<IReadOnlyList<VariantTypeDto>> GetVariantTypesAsync(CancellationToken cancellationToken = default) =>
        (await repository.GetVariantTypesAsync(cancellationToken))
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new VariantTypeDto(x.Id, x.Name, x.Options.Where(y => y.IsActive).OrderBy(y => y.Value)
                .Select(y => new VariantOptionDto(y.Id, y.Value)).ToList()))
            .ToList();

    public async Task<VariantTypeDto> CreateVariantTypeAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainRuleException("El nombre del atributo es requerido.");

        var normalizedName = NameNormalizer.Normalize(name);
        if (await repository.GetVariantTypeByNameAsync(normalizedName, cancellationToken) is not null)
            throw new DomainRuleException("Ya existe un atributo con ese nombre.");

        var variantType = new VariantType(normalizedName);
        await repository.AddVariantTypeAsync(variantType, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new VariantTypeDto(variantType.Id, variantType.Name, []);
    }

    public async Task<VariantOptionDto> CreateVariantOptionAsync(string attributeName, string value,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
            throw new DomainRuleException("Seleccione un atributo antes de crear el valor.");
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainRuleException("El nombre del valor es requerido.");

        var normalizedAttributeName = NameNormalizer.Normalize(attributeName);
        var type = await repository.GetVariantTypeByNameAsync(normalizedAttributeName, cancellationToken)
            ?? throw new DomainRuleException("Seleccione un atributo existente antes de crear el valor.");
        var normalizedValue = NameNormalizer.Normalize(value);
        if (await repository.GetVariantOptionByValueAsync(normalizedValue, cancellationToken) is not null)
            throw new DomainRuleException("Ya existe un valor con ese nombre.");

        var option = type.GetOrAddOption(normalizedValue);
        await repository.AddVariantOptionAsync(option, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new VariantOptionDto(option.Id, option.Value);
    }

    public async Task<string> GenerateProductCodeAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        var category = await repository.GetCategoryAsync(categoryId, cancellationToken)
            ?? throw new NotFoundException("Categoría no encontrada.");
        var categories = await repository.GetCategoriesAsync(cancellationToken);
        var normalizedName = NormalizeCodeLetters(category.Name);
        if (normalizedName.Length < 3)
            normalizedName = normalizedName.PadRight(3, 'X');

        var products = await repository.SearchProductsAsync(null, cancellationToken);
        var usedByCategory = products
            .Where(x => x.ParentProductId is null)
            .Select(x => new { x.CategoryId, Prefix = ExtractGeneratedPrefix(x.InternalCode) })
            .Where(x => x.Prefix is not null)
            .ToList();
        var length = 3;
        string prefix;
        do
        {
            prefix = normalizedName[..Math.Min(length, normalizedName.Length)];
            length++;
        }
        while (usedByCategory.Any(x => x.CategoryId != category.Id && x.Prefix == prefix)
            && length <= normalizedName.Length + 1);

        if (usedByCategory.Any(x => x.CategoryId != category.Id && x.Prefix == prefix))
        {
            var categoryPosition = categories.OrderBy(x => x.Name).ToList().FindIndex(x => x.Id == category.Id) + 1;
            prefix = $"{prefix}{categoryPosition}";
        }

        var next = products.Where(x => x.CategoryId == category.Id && x.ParentProductId is null)
            .Select(x => ParseGeneratedSequence(x.InternalCode, prefix)).DefaultIfEmpty(0).Max() + 1;
        return $"{prefix}-{next:000000}";
    }

    public async Task<Guid> CreateProductAsync(SaveProductRequest request, CancellationToken cancellationToken = default)
    {
        var baseUnit = await ValidateProductMasterAsync(request, null, cancellationToken);
        request = request with { LastCost = 0m, PreviousCost = 0m, UnitOfMeasure = baseUnit.Name,
            SalePrice = request.ArticleType == ArticleType.Ingrediente ? 0m : request.SalePrice };

        var category = await ResolveCategoryAsync(request.CategoryId, cancellationToken);
        var brand = string.IsNullOrWhiteSpace(request.Brand) ? "Sin marca" : request.Brand;
        var internalCode = string.IsNullOrWhiteSpace(request.InternalCode)
            ? await GenerateProductCodeAsync(category.Id, cancellationToken)
            : request.InternalCode;
        var normalizedRequest = request with { CategoryId = category.Id, Brand = brand, InternalCode = internalCode };
        await EnsureProductDoesNotExistAsync(normalizedRequest, cancellationToken);
        await EnsureBrandCatalogAsync(brand, cancellationToken);
        await EnsureInternalCodeAvailableAsync(internalCode, null, cancellationToken);
        await EnsureBarcodeAvailableAsync(request.ManagesVariants ? null : request.Barcode, null, cancellationToken);
        var product = new Product(
            internalCode,
            request.ManagesVariants ? null : request.Barcode,
            request.Name,
            request.Description,
            category.Id,
            brand,
            request.UnitOfMeasure,
            request.LastCost,
            request.PreviousCost,
            request.SalePrice,
            request.MinimumStock,
            request.AppliesTax,
            request.NoMovementDaysOverride);

        product.SetSupplierInvoiceName(request.SupplierInvoiceName);
        product.SetSupplierDescription(request.SupplierDescription);
        product.SetImageUrl(request.ImageUrl);
        product.SetActive(request.IsActive);
        product.SetManagesVariants(request.ManagesVariants);
        product.ConfigureInventoryTracking(request.ManagesLots, request.ManagesExpiry, request.ExpiryAlertDays);
        product.ConfigureMultiPricing(request.MultiPriceEnabled, request.MultiPriceMode);
        product.SetArticleType(request.ArticleType);

        product.SetBaseUnit(baseUnit.Id, baseUnit.Name);

        if (request.PriceRules is not null)
        {
            product.SynchronizePriceRules(request.PriceRules.Select(MapPriceRuleValue));
        }
        if (!request.ManagesVariants)
            await SynchronizeProductAttributesAsync(product, request.Attributes ?? [], cancellationToken);
        await repository.AddProductAsync(product, cancellationToken);

        // Branch settings
        if (request.BranchSettings is { Count: > 0 })
        {
            foreach (var bs in request.BranchSettings)
            {
                await repository.AddProductBranchSettingAsync(
                    new ProductBranchSetting(product.Id, bs.BranchId, bs.IsEnabled, bs.IsVisible, bs.SalePrice, bs.MinimumStock),
                    cancellationToken);
            }
        }

        // Purchase presentations
        if (request.PurchasePresentations is { Count: > 0 })
        {
            foreach (var pp in request.PurchasePresentations)
            {
                await repository.AddPurchasePresentationAsync(
                    new ProductPurchasePresentation(product.Id, pp.Name, pp.BaseQuantityPerPackage, pp.PurchaseUnitId, pp.IsActive),
                    cancellationToken);
            }
        }

        // Unit conversions
        if (request.Conversions is { Count: > 0 })
        {
            foreach (var c in request.Conversions)
            {
                await repository.AddUnitConversionAsync(
                    new ProductUnitConversion(product.Id, c.FromUnitId, c.ToUnitId, c.Factor, c.IsActive),
                    cancellationToken);
            }
        }

        if (!request.ManagesVariants)
            await repository.AddInventoryStockAsync(new InventoryStock(product.Id), cancellationToken);
        else
            await CreateVariantsAsync(product, request.Combinations ?? [], 0, cancellationToken);

        var actor = currentUserAccessor?.UserName ?? "Sistema";
        await repository.AddCatalogChangeLogAsync(
            new CatalogChangeLog(product.Id, product.InternalCode, "Creation", null, product.Name, actor, DateTimeOffset.UtcNow, reason: "Creación inicial"),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return product.Id;
    }

    private async Task<UnitOfMeasure> ValidateProductMasterAsync(
        SaveProductRequest request, Product? existing, CancellationToken cancellationToken)
    {
        if (request.NoMovementDaysOverride is null or < 1 or > 3650)
            throw new DomainRuleException("Define los días sin movimiento del producto entre 1 y 3650.");
        if (!ArticleTypes.Current.Contains(request.ArticleType) && existing?.ArticleType != request.ArticleType)
            throw new DomainRuleException("Selecciona Insumo, Preparado, Reventa, Subreceta o Empaque.");
        if (request.ManagesExpiry && !request.ManagesLots)
            throw new DomainRuleException("El control de caducidad requiere control por lote.");
        if (existing is not null && existing.ArticleType != request.ArticleType &&
            await repository.HasRecipeRelationsAsync(existing.Id, cancellationToken))
            throw new DomainRuleException("No se puede cambiar el tipo de un producto utilizado en recetas.");
        if (existing is not null && existing.ManagesLots != request.ManagesLots &&
            await repository.HasProductUsageAsync(existing.Id, cancellationToken))
            throw new DomainRuleException("No se puede cambiar el control por lote de un producto con historial o existencias.");
        await ValidateBranchSettingsAsync(request.BranchSettings ?? [], cancellationToken);
        var unit = request.BaseUnitId.HasValue
            ? await repository.GetUnitOfMeasureAsync(request.BaseUnitId.Value, cancellationToken) : null;
        if (unit is null || (!unit.IsActive && existing?.BaseUnitId != unit.Id))
            throw new DomainRuleException("Selecciona una unidad base válida y activa.");
        if (existing is not null && existing.BaseUnitId != unit.Id &&
            await repository.HasProductUsageAsync(existing.Id, cancellationToken))
            throw new DomainRuleException("No se puede cambiar la unidad base de un producto con historial o registros relacionados.");
        if (existing is not null && existing.ManagesExpiry != request.ManagesExpiry &&
            await repository.HasProductUsageAsync(existing.Id, cancellationToken))
            throw new DomainRuleException("No se puede cambiar el manejo de caducidad de un producto con historial sin conciliar sus lotes.");
        return unit;
    }

    public async Task DeleteProductAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await repository.GetProductAsync(id, cancellationToken)
            ?? throw new NotFoundException("Producto no encontrado.");
        if (await repository.HasProductUsageAsync(id, cancellationToken) || product.ParentProductId.HasValue ||
            product.PurchasePresentations.Count > 0 || product.UnitConversions.Count > 0)
            throw new DomainRuleException("El producto tiene historial o registros relacionados. Solo puede desactivarse.");
        await repository.RemoveUnusedProductAsync(product, cancellationToken);
        await repository.AddCatalogChangeLogAsync(new CatalogChangeLog(product.Id, product.InternalCode,
            "Deletion", product.Name, null, currentUserAccessor?.UserName ?? "Sistema", DateTimeOffset.UtcNow), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureProductDoesNotExistAsync(SaveProductRequest request, CancellationToken cancellationToken)
    {
        var requestedAttributes = NormalizeAttributeKey(request.Attributes ?? []);
        var candidates = await repository.GetProductsByNameAsync(NameNormalizer.Normalize(request.Name), cancellationToken);
        var duplicate = candidates.FirstOrDefault(product =>
            product.CategoryId == request.CategoryId
            && string.Equals(product.Brand.Trim(), request.Brand.Trim(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(NormalizeAttributeKey(product.VariantOptions.Select(x =>
                new SaveProductAttributeRequest(x.VariantOption.VariantType.Name, x.VariantOption.Value))),
                requestedAttributes, StringComparison.Ordinal));

        if (duplicate is null) return;

        var stock = await repository.GetInventoryStockAsync(duplicate.Id, cancellationToken);
        var barcode = string.IsNullOrWhiteSpace(duplicate.Barcode) ? "Sin código de barras" : duplicate.Barcode;
        throw new DomainRuleException(
            $"Este producto ya existe. Código: {duplicate.InternalCode}. Producto: {duplicate.DisplayName}. " +
            $"Marca: {duplicate.Brand}. Barras: {barcode}. Precio: Q {duplicate.SalePrice:N2}. " +
            $"Existencia: {(stock?.CurrentStock ?? 0m):N0}.");
    }

    private static string NormalizeAttributeKey(IEnumerable<SaveProductAttributeRequest> attributes) =>
        string.Join("|", attributes
            .Where(x => !string.IsNullOrWhiteSpace(x.TypeName) || !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => $"{x.TypeName.Trim().ToUpperInvariant()}={x.Value.Trim().ToUpperInvariant()}")
            .OrderBy(x => x, StringComparer.Ordinal));

    private async Task EnsureBrandCatalogAsync(string name, CancellationToken cancellationToken)
    {
        var normalizedName = NameNormalizer.Normalize(name);
        if (string.IsNullOrWhiteSpace(normalizedName) || await repository.ExistsBrandByNameAsync(normalizedName, cancellationToken))
            return;

        await repository.AddBrandAsync(new Brand(normalizedName), cancellationToken);
    }

    public async Task UpdateProductAsync(Guid id, SaveProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await repository.GetProductAsync(id, cancellationToken)
            ?? throw new NotFoundException("Producto no encontrado.");

        if (product.ParentProductId is not null)
            throw new DomainRuleException("Las variantes se administran desde el producto principal.");

        var baseUnit = await ValidateProductMasterAsync(request, product, cancellationToken);
        request = request with { LastCost = product.LastCost, PreviousCost = product.PreviousCost,
            SalePrice = request.ArticleType == ArticleType.Ingrediente ? product.SalePrice : request.SalePrice };


        var actor = currentUserAccessor?.UserName ?? "Sistema";
        var now = DateTimeOffset.UtcNow;

        if (product.SalePrice != request.SalePrice)
        {
            await repository.AddCatalogChangeLogAsync(
                new CatalogChangeLog(product.Id, product.InternalCode, "SalePrice", product.SalePrice.ToString("N2"), request.SalePrice.ToString("N2"), actor, now),
                cancellationToken);
        }

        if (product.ArticleType != request.ArticleType)
        {
            await repository.AddCatalogChangeLogAsync(
                new CatalogChangeLog(product.Id, product.InternalCode, "ArticleType", product.ArticleType.ToString(), request.ArticleType.ToString(), actor, now),
                cancellationToken);
        }

        if (product.BaseUnitId != request.BaseUnitId)
        {
            await repository.AddCatalogChangeLogAsync(
                new CatalogChangeLog(product.Id, product.InternalCode, "BaseUnitId", product.BaseUnitId?.ToString(), request.BaseUnitId?.ToString(), actor, now),
                cancellationToken);
        }

        if (product.ManagesLots != request.ManagesLots)
        {
            await repository.AddCatalogChangeLogAsync(
                new CatalogChangeLog(product.Id, product.InternalCode, "ManagesLots", product.ManagesLots.ToString(), request.ManagesLots.ToString(), actor, now),
                cancellationToken);
        }

        if (product.IsActive != request.IsActive)
        {
            await repository.AddCatalogChangeLogAsync(
                new CatalogChangeLog(product.Id, product.InternalCode, "IsActive", product.IsActive.ToString(), request.IsActive.ToString(), actor, now),
                cancellationToken);
        }

        await EnsureBarcodeAvailableAsync(product.ManagesVariants ? null : request.Barcode, id, cancellationToken);
        product.UpdateCommercialData(
            product.ManagesVariants ? null : request.Barcode,
            request.Name,
            request.Description,
            request.LastCost,
            request.PreviousCost,
            request.SalePrice,
            request.MinimumStock,
            request.AppliesTax,
            request.NoMovementDaysOverride);

        product.SetSupplierInvoiceName(request.SupplierInvoiceName);
        product.SetSupplierDescription(request.SupplierDescription);
        product.SetImageUrl(request.ImageUrl);
        product.SetActive(request.IsActive);
        product.ConfigureInventoryTracking(request.ManagesLots, request.ManagesExpiry, request.ExpiryAlertDays);
        product.ConfigureMultiPricing(request.MultiPriceEnabled, request.MultiPriceMode);
        product.SetArticleType(request.ArticleType);

        product.SetBaseUnit(baseUnit.Id, baseUnit.Name);

        if (request.PriceRules is not null)
        {
            product.SynchronizePriceRules(request.PriceRules.Select(MapPriceRuleValue));
        }
        if (!product.ManagesVariants && request.Attributes is not null)
            await SynchronizeProductAttributesAsync(product, request.Attributes, cancellationToken);
        if (product.ManagesVariants)
            await SynchronizeVariantsAsync(product, request.Combinations ?? [], cancellationToken);

        // Branch settings update
        if (request.BranchSettings is not null)
        {
            await StageProductBranchSettingsAsync(product, request.BranchSettings, cancellationToken);
        }

        // Purchase presentations update
        if (request.PurchasePresentations is not null)
        {
            var currentPresentations = await repository.GetPurchasePresentationsAsync(product.Id, cancellationToken);
            foreach (var reqPres in request.PurchasePresentations)
            {
                if (reqPres.Id.HasValue && currentPresentations.FirstOrDefault(x => x.Id == reqPres.Id.Value) is { } existingPres)
                {
                    existingPres.Update(reqPres.Name, reqPres.BaseQuantityPerPackage, reqPres.PurchaseUnitId);
                    existingPres.SetActive(reqPres.IsActive);
                }
                else
                {
                    await repository.AddPurchasePresentationAsync(
                        new ProductPurchasePresentation(product.Id, reqPres.Name, reqPres.BaseQuantityPerPackage, reqPres.PurchaseUnitId, reqPres.IsActive),
                        cancellationToken);
                }
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UnitOfMeasureDto>> GetUnitsOfMeasureAsync(CancellationToken cancellationToken = default)
    {
        var units = await repository.GetUnitsOfMeasureAsync(cancellationToken);
        return units.Select(x => new UnitOfMeasureDto(x.Id, x.Code, x.Name, x.Dimension, x.Symbol, x.Precision, x.IsActive)).ToList();
    }

    public async Task<UnitOfMeasureDto?> GetUnitOfMeasureAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var unit = await repository.GetUnitOfMeasureAsync(id, cancellationToken);
        return unit is null ? null : new UnitOfMeasureDto(unit.Id, unit.Code, unit.Name, unit.Dimension, unit.Symbol, unit.Precision, unit.IsActive);
    }

    public async Task<Guid> CreateUnitOfMeasureAsync(SaveUnitOfMeasureRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedCode = request.Code.Trim().ToLowerInvariant();
        if (await repository.GetUnitOfMeasureByCodeAsync(normalizedCode, cancellationToken) is not null)
            throw new DomainRuleException($"Ya existe una unidad de medida con el código '{request.Code}'.");

        var unit = new UnitOfMeasure(normalizedCode, request.Name, request.Dimension, request.Symbol, request.Precision, request.IsActive);
        await repository.AddUnitOfMeasureAsync(unit, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return unit.Id;
    }

    public async Task<IReadOnlyList<ProductUnitConversionDto>> GetUnitConversionsAsync(Guid? productId, CancellationToken cancellationToken = default)
    {
        var conversions = await repository.GetUnitConversionsAsync(productId, cancellationToken);
        return conversions.Select(x => new ProductUnitConversionDto(
            x.Id,
            x.ProductId,
            x.FromUnitId,
            x.FromUnit?.Code ?? string.Empty,
            x.FromUnit?.Name ?? string.Empty,
            x.ToUnitId,
            x.ToUnit?.Code ?? string.Empty,
            x.ToUnit?.Name ?? string.Empty,
            x.Factor,
            x.IsActive)).ToList();
    }

    public async Task<Guid> CreateUnitConversionAsync(SaveUnitConversionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Factor <= 0)
            throw new DomainRuleException("El factor de conversión debe ser mayor a cero.");

        var fromUnit = await repository.GetUnitOfMeasureAsync(request.FromUnitId, cancellationToken)
            ?? throw new DomainRuleException("La unidad origen no existe.");
        var toUnit = await repository.GetUnitOfMeasureAsync(request.ToUnitId, cancellationToken)
            ?? throw new DomainRuleException("La unidad destino no existe.");

        if (!request.ProductId.HasValue && fromUnit.Dimension != toUnit.Dimension)
            throw new DomainRuleException("Las conversiones universales solo pueden realizarse entre unidades de la misma dimensión.");

        var conversion = new ProductUnitConversion(request.ProductId, request.FromUnitId, request.ToUnitId, request.Factor, request.IsActive);
        await repository.AddUnitConversionAsync(conversion, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return conversion.Id;
    }

    public async Task<IReadOnlyList<ProductPurchasePresentationDto>> GetPurchasePresentationsAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var presentations = await repository.GetPurchasePresentationsAsync(productId, cancellationToken);
        return presentations.Select(x => new ProductPurchasePresentationDto(
            x.Id,
            x.ProductId,
            x.Name,
            x.PurchaseUnitId,
            x.PurchaseUnit?.Name,
            x.BaseQuantityPerPackage,
            x.IsActive)).ToList();
    }

    public async Task<Guid> CreatePurchasePresentationAsync(SavePurchasePresentationRequest request, Guid productId, CancellationToken cancellationToken = default)
    {
        var presentation = new ProductPurchasePresentation(productId, request.Name, request.BaseQuantityPerPackage, request.PurchaseUnitId, request.IsActive);
        await repository.AddPurchasePresentationAsync(presentation, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return presentation.Id;
    }

    public async Task<IReadOnlyList<ProductBranchSettingDto>> GetProductBranchSettingsAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var product = await repository.GetProductAsync(productId, cancellationToken)
            ?? throw new NotFoundException("Producto no encontrado.");

        var settings = await repository.GetProductBranchSettingsAsync(productId, cancellationToken);
        return settings.Select(x => new ProductBranchSettingDto(
            x.Id,
            x.ProductId,
            x.BranchId,
            x.Branch?.Name ?? string.Empty,
            x.IsEnabled,
            x.IsVisible,
            x.SalePrice,
            x.SalePrice ?? product.SalePrice,
            x.MinimumStock,
            x.MinimumStock ?? product.MinimumStock)).ToList();
    }

    public async Task SaveProductBranchSettingsAsync(Guid productId, IReadOnlyList<SaveProductBranchSettingRequest> settings, CancellationToken cancellationToken = default)
    {
        var product = await repository.GetProductAsync(productId, cancellationToken)
            ?? throw new NotFoundException("Producto no encontrado.");

        await ValidateBranchSettingsAsync(settings, cancellationToken);
        await StageProductBranchSettingsAsync(product, settings, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateBranchSettingsAsync(IReadOnlyList<SaveProductBranchSettingRequest> settings, CancellationToken cancellationToken)
    {
        if (settings.Any(x => x.BranchId == Guid.Empty || x.SalePrice < 0 || x.MinimumStock < 0) ||
            settings.Select(x => x.BranchId).Distinct().Count() != settings.Count)
            throw new DomainRuleException("Revisa las sucursales y sus valores de precio y stock mínimo.");
        if (operationContext is not null && settings.Count > 0)
        {
            var authorized = (await operationContext.GetAuthorizedBranchesAsync(cancellationToken)).Select(x => x.Id).ToHashSet();
            if (settings.Any(x => !authorized.Contains(x.BranchId)))
                throw new DomainRuleException("No tienes autorización para configurar una de las sucursales indicadas.");
        }
    }

    private async Task StageProductBranchSettingsAsync(Product product, IReadOnlyList<SaveProductBranchSettingRequest> settings, CancellationToken cancellationToken)
    {
        var productId = product.Id;

        var actor = currentUserAccessor?.UserName ?? "Sistema";
        var now = DateTimeOffset.UtcNow;

        foreach (var req in settings)
        {
            var existing = await repository.GetProductBranchSettingAsync(productId, req.BranchId, cancellationToken);
            if (existing is not null)
            {
                if (existing.SalePrice != req.SalePrice)
                {
                    await repository.AddCatalogChangeLogAsync(
                        new CatalogChangeLog(productId, product.InternalCode, "BranchSalePrice", existing.SalePrice?.ToString("N2"), req.SalePrice?.ToString("N2"), actor, now, branchId: req.BranchId),
                        cancellationToken);
                }
                if (existing.MinimumStock != req.MinimumStock)
                {
                    await repository.AddCatalogChangeLogAsync(
                        new CatalogChangeLog(productId, product.InternalCode, "BranchMinimumStock", existing.MinimumStock?.ToString("N2"), req.MinimumStock?.ToString("N2"), actor, now, branchId: req.BranchId),
                        cancellationToken);
                }
                existing.Update(req.IsEnabled, req.IsVisible, req.SalePrice, req.MinimumStock);
            }
            else
            {
                var newSetting = new ProductBranchSetting(productId, req.BranchId, req.IsEnabled, req.IsVisible, req.SalePrice, req.MinimumStock);
                await repository.AddProductBranchSettingAsync(newSetting, cancellationToken);
                if (req.SalePrice.HasValue)
                {
                    await repository.AddCatalogChangeLogAsync(
                        new CatalogChangeLog(productId, product.InternalCode, "BranchSalePrice", null, req.SalePrice.Value.ToString("N2"), actor, now, branchId: req.BranchId),
                        cancellationToken);
                }
                if (req.MinimumStock.HasValue)
                {
                    await repository.AddCatalogChangeLogAsync(
                        new CatalogChangeLog(productId, product.InternalCode, "BranchMinimumStock", null, req.MinimumStock.Value.ToString("N2"), actor, now, branchId: req.BranchId),
                        cancellationToken);
                }
            }
        }
    }

    public async Task<IReadOnlyList<CatalogChangeLogDto>> GetCatalogChangeLogsAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var logs = await repository.GetCatalogChangeLogsAsync(productId, cancellationToken);
        return logs.Select(x => new CatalogChangeLogDto(
            x.Id,
            x.ProductId,
            x.ProductInternalCode,
            x.BranchId,
            null,
            x.Field,
            x.OldValue,
            x.NewValue,
            x.Actor,
            x.TimestampUtc,
            x.Reason)).ToList();
    }

    private async Task SynchronizeVariantsAsync(Product parent, IReadOnlyList<SaveProductCombinationRequest> combinations,
        CancellationToken cancellationToken)
    {
        if (combinations.Count == 0)
            throw new DomainRuleException("Agrega al menos una variante al producto.");
        EnsureUniqueVariantCombinations(combinations);
        var existing = await repository.GetProductVariantsAsync(parent.Id, cancellationToken);
        var retained = combinations.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToHashSet();
        foreach (var variant in existing.Where(x => !retained.Contains(x.Id))) variant.SetActive(false);

        foreach (var value in combinations.Where(x => x.Id.HasValue))
        {
            var variant = existing.FirstOrDefault(x => x.Id == value.Id)
                ?? throw new DomainRuleException("Una variante indicada no pertenece a este producto.");
            await EnsureBarcodeAvailableAsync(value.Barcode, variant.Id, cancellationToken);
            variant.UpdateCommercialData(value.Barcode, variant.Name, variant.Description, variant.LastCost,
                variant.PreviousCost, value.SalePrice, value.MinimumStock, variant.AppliesTax, variant.NoMovementDaysOverride);
            variant.SetSupplierInvoiceName(parent.SupplierInvoiceName);
            variant.SetSupplierDescription(parent.SupplierDescription);
            variant.SetActive(value.IsActive && parent.IsActive);
            variant.ConfigureInventoryTracking(parent.ManagesLots, parent.ManagesExpiry, parent.ExpiryAlertDays);
            variant.SetImageUrl(value.ImageUrl);
            variant.SetArticleType(parent.ArticleType);
            variant.SetBaseUnit(parent.BaseUnitId, parent.UnitOfMeasure);
        }

        var lastSequence = existing.Select(x => ParseVariantSequence(x.InternalCode, parent.InternalCode)).DefaultIfEmpty(0).Max();
        await CreateVariantsAsync(parent, combinations.Where(x => !x.Id.HasValue).ToList(), lastSequence, cancellationToken);
    }

    private async Task CreateVariantsAsync(Product parent, IReadOnlyList<SaveProductCombinationRequest> combinations,
        int startingSequence, CancellationToken cancellationToken)
    {
        if (combinations.Count == 0 && startingSequence == 0)
            throw new DomainRuleException("Agrega al menos una variante al producto.");
        EnsureUniqueVariantCombinations(combinations);
        var sequence = startingSequence;
        foreach (var value in combinations)
        {
            var attributes = ParseCombinationKey(value.Key);
            var label = string.Join(" / ", attributes.Select(x => Product.FormatVariantLabel(x.TypeName, x.Value)));
            var code = $"{parent.InternalCode}-{++sequence:00}";
            await EnsureInternalCodeAvailableAsync(code, null, cancellationToken);
            await EnsureBarcodeAvailableAsync(value.Barcode, null, cancellationToken);
            var variant = new Product(code, value.Barcode, parent.Name, parent.Description, parent.CategoryId, parent.Brand,
                parent.UnitOfMeasure, 0m, 0m, value.SalePrice, value.MinimumStock,
                parent.AppliesTax, parent.NoMovementDaysOverride);
            variant.SetSupplierInvoiceName(parent.SupplierInvoiceName);
            variant.SetSupplierDescription(parent.SupplierDescription);
            variant.SetVariant(parent.Id, label);
            variant.SetActive(value.IsActive && parent.IsActive);
            variant.ConfigureInventoryTracking(parent.ManagesLots, parent.ManagesExpiry, parent.ExpiryAlertDays);
            variant.SetImageUrl(value.ImageUrl);
            variant.SetArticleType(parent.ArticleType);
            variant.SetBaseUnit(parent.BaseUnitId, parent.UnitOfMeasure);
            await SynchronizeProductAttributesAsync(variant, attributes, cancellationToken);
            await repository.AddProductAsync(variant, cancellationToken);
            await repository.AddInventoryStockAsync(new InventoryStock(variant.Id), cancellationToken);
        }
    }

    private static IReadOnlyList<SaveProductAttributeRequest> ParseCombinationKey(string key)
    {
        var attributes = key.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2)
            .Select(parts => new SaveProductAttributeRequest(parts[0], parts[1])).ToList();
        if (attributes.Count == 0 || attributes.Any(x => string.IsNullOrWhiteSpace(x.TypeName) || string.IsNullOrWhiteSpace(x.Value)))
            throw new DomainRuleException("La combinación de la variante no es válida.");
        if (attributes.GroupBy(x => x.TypeName.Trim(), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw new DomainRuleException("Una variante no puede repetir el mismo atributo.");
        return attributes;
    }

    private static void EnsureUniqueVariantCombinations(IReadOnlyList<SaveProductCombinationRequest> combinations)
    {
        var normalizedKeys = combinations.Select(combination => string.Join("|", ParseCombinationKey(combination.Key)
            .Select(attribute => $"{attribute.TypeName.Trim().ToUpperInvariant()}={attribute.Value.Trim().ToUpperInvariant()}")
            .OrderBy(attribute => attribute, StringComparer.Ordinal))).ToList();
        if (normalizedKeys.Distinct(StringComparer.Ordinal).Count() != normalizedKeys.Count)
            throw new DomainRuleException("No puedes agregar la misma variante más de una vez.");
    }

    private async Task<Category> ResolveCategoryAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        if (categoryId != Guid.Empty && await repository.GetCategoryAsync(categoryId, cancellationToken) is { } selected)
            return selected;
        var fallback = (await repository.GetCategoriesAsync(cancellationToken)).FirstOrDefault(x =>
            x.Name.Equals("Sin categoría", StringComparison.OrdinalIgnoreCase));
        if (fallback is not null) return fallback;
        fallback = new Category("Sin categoría", "Productos sin una categoría específica.");
        await repository.AddCategoryAsync(fallback, cancellationToken);
        return fallback;
    }

    private async Task EnsureInternalCodeAvailableAsync(string code, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new DomainRuleException("El código del producto es obligatorio.");
        if (await repository.ExistsInternalCodeAsync(code, excludeId, cancellationToken))
            throw new DomainRuleException("Ya existe un producto con ese código.");
    }

    private static string NormalizeCodeLetters(string value)
    {
        var normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        return new string(normalized.Where(x => char.IsLetter(x) &&
            System.Globalization.CharUnicodeInfo.GetUnicodeCategory(x) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(char.ToUpperInvariant).ToArray());
    }

    private static string? ExtractGeneratedPrefix(string code)
    {
        var separator = code.LastIndexOf('-');
        return separator > 0 && code[(separator + 1)..].Length == 6 && code[(separator + 1)..].All(char.IsDigit)
            ? code[..separator] : null;
    }

    private static int ParseGeneratedSequence(string code, string prefix) =>
        code.StartsWith(prefix + "-", StringComparison.Ordinal) && int.TryParse(code[(prefix.Length + 1)..], out var value) ? value : 0;

    private static int ParseVariantSequence(string code, string parentCode) =>
        code.StartsWith(parentCode + "-", StringComparison.Ordinal) && int.TryParse(code[(parentCode.Length + 1)..], out var value) ? value : 0;

    private async Task SynchronizeProductAttributesAsync(Product product,
        IReadOnlyList<SaveProductAttributeRequest> attributes, CancellationToken cancellationToken)
    {
        var values = attributes
            .Where(x => !string.IsNullOrWhiteSpace(x.TypeName) || !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => new SaveProductAttributeRequest(x.TypeName.Trim(), x.Value.Trim()))
            .ToList();

        if (values.Any(x => string.IsNullOrWhiteSpace(x.TypeName) || string.IsNullOrWhiteSpace(x.Value)))
            throw new DomainRuleException("Cada atributo debe tener un nombre y un valor.");
        if (values.GroupBy(x => x.TypeName, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            throw new DomainRuleException("Un producto solo puede tener un valor por cada atributo.");

        var resolved = new List<VariantOption>();
        foreach (var attribute in values)
        {
            var type = await repository.GetVariantTypeByNameAsync(attribute.TypeName, cancellationToken);
            if (type is null)
                throw new DomainRuleException("Seleccione un atributo existente o créelo antes de guardar el producto.");

            var globalOption = await repository.GetVariantOptionByValueAsync(attribute.Value, cancellationToken);
            if (globalOption is null)
                throw new DomainRuleException("Seleccione un valor existente o créelo antes de guardar el producto.");

            var existingOption = type.Options.FirstOrDefault(x =>
                string.Equals(x.Value, attribute.Value, StringComparison.OrdinalIgnoreCase));
            if (existingOption is null)
            {
                existingOption = type.GetOrAddOption(globalOption.Value);
                await repository.AddVariantOptionAsync(existingOption, cancellationToken);
            }

            resolved.Add(existingOption);
        }

        var createdAssociations = product.AssignVariantOptions(resolved.Select((option, index) => (option.Id, index)));
        await repository.AddProductVariantOptionsAsync(createdAssociations, cancellationToken);
    }

    private async Task EnsureBarcodeAvailableAsync(string? barcode, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(barcode)
            && await repository.ExistsBarcodeAsync(barcode, excludeId, cancellationToken))
        {
            throw new DomainRuleException("Ya existe un producto con ese código de barras.");
        }
    }

    private static CategoryDto MapCategory(Category category) =>
        new(category.Id, category.Name, category.Description, category.IsActive);

    private static ProductDto MapProduct(Product product, decimal currentStock, Guid? branchId = null)
    {
        var effectiveSalePrice = product.SalePrice;
        var effectiveMinimumStock = product.MinimumStock;
        var effectiveIsActive = product.IsActive;
        var effectiveIsVisible = true;

        if (branchId.HasValue && branchId.Value != Guid.Empty)
        {
            var branchSetting = product.BranchSettings.FirstOrDefault(x => x.BranchId == branchId.Value);
            if (branchSetting is not null)
            {
                if (branchSetting.SalePrice.HasValue)
                    effectiveSalePrice = branchSetting.SalePrice.Value;
                effectiveMinimumStock = branchSetting.MinimumStock ?? product.MinimumStock;
                effectiveIsActive = branchSetting.IsEnabled && product.IsActive;
                effectiveIsVisible = branchSetting.IsVisible;
            }
        }

        var branchSettingsDto = product.BranchSettings.Select(x => new ProductBranchSettingDto(
            x.Id,
            x.ProductId,
            x.BranchId,
            x.Branch?.Name ?? string.Empty,
            x.IsEnabled,
            x.IsVisible,
            x.SalePrice,
            x.SalePrice ?? product.SalePrice,
            x.MinimumStock,
            x.MinimumStock ?? product.MinimumStock)).ToList();

        var presentationsDto = product.PurchasePresentations.Select(x => new ProductPurchasePresentationDto(
            x.Id,
            x.ProductId,
            x.Name,
            x.PurchaseUnitId,
            x.PurchaseUnit?.Name,
            x.BaseQuantityPerPackage,
            x.IsActive)).ToList();

        var conversionsDto = product.UnitConversions.Select(x => new ProductUnitConversionDto(
            x.Id,
            x.ProductId,
            x.FromUnitId,
            x.FromUnit?.Code ?? string.Empty,
            x.FromUnit?.Name ?? string.Empty,
            x.ToUnitId,
            x.ToUnit?.Code ?? string.Empty,
            x.ToUnit?.Name ?? string.Empty,
            x.Factor,
            x.IsActive)).ToList();

        return new(
            product.Id,
            product.InternalCode,
            product.Barcode,
            product.Name,
            product.Description,
            product.CategoryId,
            product.Category?.Name ?? string.Empty,
            product.Brand,
            product.UnitOfMeasure,
            product.LastCost,
            product.PreviousCost,
            effectiveSalePrice,
            effectiveMinimumStock,
            product.AppliesTax,
            currentStock,
            effectiveIsActive,
            product.NoMovementDaysOverride,
            product.MultiPriceEnabled,
            product.MultiPriceMode,
            product.PriceRules
                .OrderBy(x => x.Quantity)
                .Select(x => new ProductPriceRuleDto(x.Id, x.Quantity, x.Price, x.IsActive))
                .ToList(),
            product.ParentProductId,
            product.VariantValue,
            product.DisplayName,
            product.ManagesVariants,
            product.VariantOptions.OrderBy(x => x.SortOrder)
                .Select(x => new ProductVariantOptionDto(x.VariantOptionId, x.VariantOption.VariantTypeId,
                    x.VariantOption.VariantType.Name, x.VariantOption.Value, x.SortOrder)).ToList(),
            product.SupplierInvoiceName,
            product.SupplierDescription,
            product.ManagesExpiry,
            product.ExpiryAlertDays,
            product.ImageUrl,
            product.ArticleType,
            product.BaseUnitId,
            product.BaseUnit?.Name,
            product.BaseUnit?.Code,
            branchSettingsDto,
            presentationsDto,
            conversionsDto,
            product.ManagesLots,
            effectiveIsVisible);
    }

    private static ProductPriceRuleValue MapPriceRuleValue(SaveProductPriceRuleRequest rule) =>
        new(rule.Id, rule.Quantity, rule.Price, rule.IsActive);
}
