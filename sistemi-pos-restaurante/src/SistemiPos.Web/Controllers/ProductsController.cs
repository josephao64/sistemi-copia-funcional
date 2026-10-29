using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemiPos.Application.Catalog;
using SistemiPos.Application.Security;
using SistemiPos.Application.Settings;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Web.Models.Catalog;
using SistemiPos.Web.Services;

namespace SistemiPos.Web.Controllers;

[Authorize(Policy = PermissionCodes.ProductsView)]
public sealed class ProductsController(
    ICatalogService catalogService,
    ISettingsService settingsService,
    SistemiPos.Application.Inventory.IInventoryQueryService inventoryQueryService,
    ApplicationStoragePaths storagePaths,
    SistemiPos.Application.Abstractions.IOperationContextAccessor operationContext) : Controller
{
    public async Task<IActionResult> Index(string? query, ArticleType? articleType, CancellationToken cancellationToken)
    {
        ViewData["Query"] = query;
        ViewData["ArticleType"] = articleType;
        var products = await catalogService.SearchProductsAsync(query, cancellationToken);
        if (articleType.HasValue)
        {
            products = products.Where(x => x.ArticleType == articleType.Value).ToList();
        }
        return View(products
            .Where(x => !x.ManagesVariants)
            .OrderBy(x => x.DisplayName ?? x.Name)
            .ToList());
    }

    public async Task<IActionResult> Expiring(Guid? productId, string? category, string? status, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var lots = await inventoryQueryService.GetExpiryLotsAsync(operationContext.CurrentWarehouseId, cancellationToken);
        var categories = lots.Select(x => x.CategoryName).Distinct().OrderBy(x => x).ToList();
        if (productId.HasValue) lots = lots.Where(x => x.ProductId == productId.Value).ToList();
        if (!string.IsNullOrWhiteSpace(category)) lots = lots.Where(x => x.CategoryName == category).ToList();
        if (!string.IsNullOrWhiteSpace(status)) lots = lots.Where(x => x.Status == status).ToList();
        if (from.HasValue) lots = lots.Where(x => x.ExpiryDate >= from.Value).ToList();
        if (to.HasValue) lots = lots.Where(x => x.ExpiryDate <= to.Value).ToList();
        ViewBag.Products = (await catalogService.SearchProductsAsync(null, cancellationToken))
            .Where(x => !x.ManagesVariants)
            .OrderBy(x => x.DisplayName ?? x.Name)
            .ToList();
        ViewBag.Categories = categories;
        return View(new ExpiringProductsViewModel { Lots = lots, ProductId = productId, Category = category, Status = status, From = from, To = to });
    }

    [Authorize(Policy = PermissionCodes.ProductsManage)]
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken) =>
        View("Form", await BuildFormAsync(new ProductFormViewModel(), cancellationToken));

    [Authorize(Policy = PermissionCodes.CategoriesManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategoryInline(string name, string? description, CancellationToken cancellationToken)
    {
        try
        {
            var categoryId = await catalogService.CreateCategoryAsync(
                new SaveCategoryRequest(name, description, true),
                cancellationToken);
            var category = await catalogService.GetCategoryAsync(categoryId, cancellationToken);
            return Json(new { id = categoryId, text = category?.Name ?? name.Trim() });
        }
        catch (DomainRuleException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [Authorize(Policy = PermissionCodes.ProductsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBrandInline(string name, CancellationToken cancellationToken)
    {
        try
        {
            var brand = await catalogService.CreateBrandAsync(name, cancellationToken);
            return Json(new { id = brand, text = brand });
        }
        catch (DomainRuleException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [Authorize(Policy = PermissionCodes.ProductsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAttributeInline(string name, CancellationToken cancellationToken)
    {
        try
        {
            var attribute = await catalogService.CreateVariantTypeAsync(name, cancellationToken);
            return Json(new { id = attribute.Id, text = attribute.Name });
        }
        catch (DomainRuleException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [Authorize(Policy = PermissionCodes.ProductsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAttributeValueInline(string attributeName, string value,
        CancellationToken cancellationToken)
    {
        try
        {
            var option = await catalogService.CreateVariantOptionAsync(attributeName, value, cancellationToken);
            return Json(new { id = option.Id, text = option.Value });
        }
        catch (DomainRuleException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [Authorize(Policy = PermissionCodes.ProductsManage)]
    [HttpGet]
    public async Task<IActionResult> GenerateCode(Guid categoryId, CancellationToken cancellationToken)
    {
        try
        {
            return Json(new { code = await catalogService.GenerateProductCodeAsync(categoryId, cancellationToken) });
        }
        catch (Exception exception) when (exception is DomainRuleException or SistemiPos.Application.Common.Exceptions.NotFoundException)
        {
            return BadRequest(new { message = "Seleccione una categoría antes de generar el código." });
        }
    }

    [Authorize(Policy = PermissionCodes.ProductsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductFormViewModel model, CancellationToken cancellationToken)
    {
        CleanOptionalModelState(model);
        if (!ModelState.IsValid)
        {
            return View("Form", await BuildFormAsync(model, cancellationToken));
        }

        try
        {
            await SaveSubmittedImagesAsync(model, null, cancellationToken);
            var id = await catalogService.CreateProductAsync(MapRequest(model), cancellationToken);
            if (IsAjaxRequest() && bool.TryParse(Request.Query["returnProduct"], out var returnProduct) && returnProduct)
            {
                var product = await catalogService.GetProductAsync(id, cancellationToken);
                return Json(new
                {
                    id,
                    systemName = product?.DisplayName ?? product?.Name ?? model.Name,
                    supplierName = product?.SupplierInvoiceName,
                    systemDescription = product?.Description,
                    supplierDescription = product?.SupplierDescription,
                    managesExpiry = product?.ManagesExpiry ?? model.ManagesExpiry,
                    managesLots = product?.ManagesLots ?? model.ManagesLots,
                    salePrice = product?.SalePrice ?? model.SalePrice,
                    appliesTax = product?.AppliesTax ?? model.AppliesTax,
                    multiPriceEnabled = product?.MultiPriceEnabled ?? false,
                    priceRules = (product?.PriceRules ?? [])
                        .Select(x => new { quantity = x.Quantity, price = x.Price })
                });
            }
            if (IsAjaxRequest())
            {
                TempData["Success"] = "El producto fue creado correctamente.";
                return Json(new
                {
                    reload = true,
                    message = "El producto fue creado correctamente."
                });
            }
            TempData["Success"] = "El producto fue creado correctamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (DomainRuleException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("Form", await BuildFormAsync(model, cancellationToken));
        }
    }

    [Authorize(Policy = PermissionCodes.ProductsManage)]
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var product = await catalogService.GetProductByBranchAsync(id, Guid.Empty, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }
        var variants = (await catalogService.GetProductVariantsAsync(product.Id, cancellationToken))
            .Where(x => x.IsActive)
            .ToList();

        var model = new ProductFormViewModel
        {
            Id = product.Id,
            InternalCode = product.InternalCode,
            Barcode = product.Barcode,
            Name = product.Name,
            SupplierInvoiceName = product.SupplierInvoiceName,
            SupplierDescription = product.SupplierDescription,
            ParentProductId = product.ParentProductId,
            VariantValue = product.VariantValue,
            ManagesVariants = product.ManagesVariants,
            ManagesExpiry = product.ManagesExpiry,
            ManagesLots = product.ManagesLots,
            ExpiryAlertDays = product.ExpiryAlertDays,
            Attributes = (product.VariantOptions ?? [])
                .OrderBy(x => x.SortOrder)
                .Select(x => new ProductAttributeFormViewModel { TypeName = x.TypeName, Value = x.Value })
                .ToList(),
            VariantGroups = [],
            Combinations = variants.Select(x => new ProductCombinationFormViewModel
            {
                Id = x.Id,
                Key = string.Join("|", (x.VariantOptions ?? []).OrderBy(y => y.SortOrder).Select(y => $"{y.TypeName}={y.Value}")),
                Label = x.VariantOptions is { Count: > 0 }
                    ? string.Join(" / ", x.VariantOptions.OrderBy(y => y.SortOrder)
                        .Select(y => Product.FormatVariantLabel(y.TypeName, y.Value)))
                    : x.VariantValue ?? string.Empty,
                InternalCode = x.InternalCode,
                Barcode = x.Barcode,
                LastCost = x.LastCost,
                SalePrice = x.SalePrice,
                MinimumStock = x.MinimumStock,
                CurrentStock = x.CurrentStock,
                IsActive = x.IsActive,
                ImageUrl = x.ImageUrl
            }).ToList(),
            Description = product.Description,
            ImageUrl = product.ImageUrl,
            CategoryId = product.CategoryId,
            CategoryName = product.CategoryName,
            Brand = product.Brand,
            UnitOfMeasure = product.UnitOfMeasure,
            LastCost = product.LastCost,
            PreviousCost = product.PreviousCost,
            SalePrice = product.SalePrice,
            MinimumStock = product.MinimumStock,
            AppliesTax = product.AppliesTax,
            IsActive = product.IsActive,
            MultiPriceEnabled = product.MultiPriceEnabled,
            MultiPriceMode = product.MultiPriceMode,
            PriceRules = (product.PriceRules ?? [])
                .Select(x => new ProductPriceRuleFormViewModel
                {
                    Id = x.Id,
                    Quantity = x.Quantity,
                    Price = x.Price,
                    IsActive = x.IsActive
                })
                .ToList(),
            NoMovementDaysOverride = product.NoMovementDaysOverride,
            ArticleType = product.ArticleType,
            BaseUnitId = product.BaseUnitId,
            BranchSettings = product.BranchSettings?.Select(x => new ProductBranchSettingFormViewModel
            {
                BranchId = x.BranchId,
                BranchName = x.BranchName,
                IsEnabled = x.IsEnabled,
                IsVisible = x.IsVisible,
                SalePrice = x.SalePrice,
                MinimumStock = x.MinimumStock
            }).ToList() ?? [],
            PurchasePresentations = product.PurchasePresentations?.Select(x => new ProductPurchasePresentationFormViewModel
            {
                Id = x.Id,
                Name = x.Name,
                BaseQuantityPerPackage = x.BaseQuantityPerPackage,
                PurchaseUnitId = x.PurchaseUnitId,
                PurchaseUnitName = x.PurchaseUnitName,
                IsActive = x.IsActive
            }).ToList() ?? [],
            UnitConversions = product.UnitConversions?.Select(x => new ProductUnitConversionFormViewModel
            {
                Id = x.Id,
                FromUnitId = x.FromUnitId,
                FromUnitCode = x.FromUnitCode,
                ToUnitId = x.ToUnitId,
                ToUnitCode = x.ToUnitCode,
                Factor = x.Factor,
                IsActive = x.IsActive
            }).ToList() ?? []
        };

        return View("Form", await BuildFormAsync(model, cancellationToken));
    }

    [Authorize(Policy = PermissionCodes.ProductsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProductFormViewModel model, CancellationToken cancellationToken)
    {
        CleanOptionalModelState(model);
        if (!ModelState.IsValid || model.Id is null)
        {
            return View("Form", await BuildFormAsync(model, cancellationToken));
        }

        try
        {
            var current = await catalogService.GetProductAsync(model.Id.Value, cancellationToken);
            if (current is null)
                return NotFound();
            model.MultiPriceEnabled = current.MultiPriceEnabled;
            model.MultiPriceMode = current.MultiPriceMode;
            model.PriceRules = (current.PriceRules ?? []).Select(x => new ProductPriceRuleFormViewModel
            {
                Id = x.Id,
                Quantity = x.Quantity,
                Price = x.Price,
                IsActive = x.IsActive
            }).ToList();
            var currentVariants = await catalogService.GetProductVariantsAsync(current.Id, cancellationToken);
            await SaveSubmittedImagesAsync(model, current, cancellationToken, currentVariants);
            await catalogService.UpdateProductAsync(model.Id.Value, MapRequest(model), cancellationToken);
            TempData["Success"] = "El producto fue actualizado correctamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (DomainRuleException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("Form", await BuildFormAsync(model, cancellationToken));
        }
    }

    [Authorize(Policy = PermissionCodes.ProductsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await catalogService.DeleteProductAsync(id, cancellationToken);
            TempData["Success"] = "Producto sin uso eliminado correctamente.";
        }
        catch (DomainRuleException exception)
        {
            TempData["Error"] = exception.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<ProductFormViewModel> BuildFormAsync(ProductFormViewModel model, CancellationToken cancellationToken)
    {
        var categories = await catalogService.GetCategoriesAsync(cancellationToken);
        model.Categories = categories
            .Where(x => x.IsActive || x.Id == model.CategoryId)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToList();
        model.CategoryName = categories.FirstOrDefault(x => x.Id == model.CategoryId)?.Name ?? model.CategoryName;
        model.VariantCatalog = await catalogService.GetVariantTypesAsync(cancellationToken);
        model.Brands = await catalogService.GetProductBrandsAsync(cancellationToken);

        model.AvailableArticleTypes = ArticleTypes.Current
            .Concat(model.Id.HasValue ? new[] { model.ArticleType } : Array.Empty<ArticleType>()).Distinct()
            .Select(x => new SelectListItem(x.Label(), ((int)x).ToString(), x == model.ArticleType))
            .ToList();

        var units = await catalogService.GetUnitsOfMeasureAsync(cancellationToken);
        model.AvailableUnitsOfMeasure = units
            .Where(x => x.IsActive || x.Id == model.BaseUnitId)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem($"{x.Name} ({x.Symbol})", x.Id.ToString(), x.Id == model.BaseUnitId))
            .ToList();

        if (string.IsNullOrWhiteSpace(model.UnitOfMeasure))
            model.UnitOfMeasure = units.FirstOrDefault(x => x.Id == model.BaseUnitId)?.Code ?? "unidad";

        var authorizedBranches = await operationContext.GetAuthorizedBranchesAsync(cancellationToken);
        model.BranchSettings = model.BranchSettings.Where(x => authorizedBranches.Any(b => b.Id == x.BranchId)).ToList();
        foreach (var branch in authorizedBranches)
        {
            if (!model.BranchSettings.Any(x => x.BranchId == branch.Id))
            {
                model.BranchSettings.Add(new ProductBranchSettingFormViewModel
                {
                    BranchId = branch.Id,
                    BranchName = $"{branch.Name} ({branch.CompanyTradeName})",
                    IsEnabled = true,
                    IsVisible = true,
                    SalePrice = null,
                    MinimumStock = null
                });
            }
            else
            {
                var setting = model.BranchSettings.First(x => x.BranchId == branch.Id);
                setting.BranchName = $"{branch.Name} ({branch.CompanyTradeName})";
            }
        }

        if (model.Id.HasValue)
        {
            model.ChangeLogs = (await catalogService.GetCatalogChangeLogsAsync(model.Id.Value, cancellationToken)).ToList();
        }

        var settings = await settingsService.GetSystemSettingAsync(cancellationToken);
        model.CurrencySymbol = settings.CurrencySymbol;
        model.CurrencyDecimals = settings.CurrencyDecimals;

        return model;
    }

    private static SaveProductRequest MapRequest(ProductFormViewModel model) =>
        new SaveProductRequest(
            model.InternalCode,
            model.Barcode,
            model.Name,
            model.Description,
            model.CategoryId,
            model.Brand,
            model.UnitOfMeasure,
            model.LastCost,
            model.PreviousCost,
            model.SalePrice,
            model.MinimumStock,
            model.AppliesTax,
            model.IsActive,
            model.NoMovementDaysOverride,
            model.MultiPriceEnabled,
            model.MultiPriceMode,
            model.PriceRules.Select(x => new SaveProductPriceRuleRequest(x.Id, x.Quantity, x.Price, x.IsActive)).ToList(),
            null,
            null,
            false,
            [],
            [],
            model.ManagesVariants ? null : model.Attributes.Select(x => new SaveProductAttributeRequest(x.TypeName, x.Value)).ToList()) with
        {
            SupplierInvoiceName = model.SupplierInvoiceName,
            SupplierDescription = model.SupplierDescription,
            ManagesExpiry = model.ManagesExpiry,
            ManagesLots = model.ManagesLots,
            ExpiryAlertDays = model.ManagesExpiry ? model.ExpiryAlertDays : null,
            ManagesVariants = model.ManagesVariants,
            VariantGroups = model.VariantGroups.Select(x => new SaveVariantGroupRequest(x.TypeName, x.Values)).ToList(),
            Combinations = model.Combinations.Select(x => new SaveProductCombinationRequest(
                x.Id, x.Key, x.Barcode, x.LastCost, x.SalePrice, x.MinimumStock, x.IsActive, x.ImageUrl)).ToList(),
            ImageUrl = model.ImageUrl,
            ArticleType = model.ArticleType,
            BaseUnitId = model.BaseUnitId,
            BranchSettings = model.BranchSettings.Select(b => new SaveProductBranchSettingRequest(b.BranchId, b.IsEnabled, b.IsVisible, b.SalePrice, b.MinimumStock)).ToList(),
            PurchasePresentations = model.PurchasePresentations.Where(p => !string.IsNullOrWhiteSpace(p.Name) && p.BaseQuantityPerPackage is > 0)
                .Select(p => new SavePurchasePresentationRequest(p.Id, p.Name!.Trim(), p.BaseQuantityPerPackage!.Value, p.PurchaseUnitId, p.IsActive)).ToList(),
            Conversions = model.UnitConversions.Where(c => c.FromUnitId != Guid.Empty && c.ToUnitId != Guid.Empty && c.Factor > 0)
                .Select(c => new SaveUnitConversionRequest(model.Id, c.FromUnitId, c.ToUnitId, c.Factor, c.IsActive)).ToList()
        };

    private async Task SaveSubmittedImagesAsync(
        ProductFormViewModel model,
        ProductDto? current,
        CancellationToken cancellationToken,
        IReadOnlyList<ProductDto>? currentVariants = null)
    {
        if (!model.ManagesVariants)
        {
            model.ImageUrl = model.ImageFile is null
                ? current?.ImageUrl
                : await SaveProductImageAsync(model.ImageFile, cancellationToken);
            return;
        }

        model.ImageUrl = null;
        var existingImages = (currentVariants ?? [])
            .ToDictionary(variant => variant.Id, variant => variant.ImageUrl);
        foreach (var combination in model.Combinations)
        {
            combination.ImageUrl = combination.ImageFile is not null
                ? await SaveProductImageAsync(combination.ImageFile, cancellationToken)
                : combination.Id is { } id && existingImages.TryGetValue(id, out var imageUrl)
                    ? imageUrl
                    : null;
        }
    }

    private async Task<string?> SaveProductImageAsync(IFormFile? image, CancellationToken cancellationToken)
    {
        if (image is null || image.Length == 0) return null;
        if (image.Length > 5 * 1024 * 1024) throw new DomainRuleException("La imagen no puede superar 5 MB.");
        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            throw new DomainRuleException("La imagen debe ser JPG, PNG o WEBP.");
        var directory = Path.Combine(storagePaths.UploadsPath, "products");
        Directory.CreateDirectory(directory);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        await using var stream = System.IO.File.Create(Path.Combine(directory, fileName));
        await image.CopyToAsync(stream, cancellationToken);
        return $"/uploads/products/{fileName}";
    }

    private bool IsAjaxRequest() =>
        string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    private void CleanOptionalModelState(ProductFormViewModel model)
    {
        for (var i = 0; i < model.PurchasePresentations.Count; i++)
        {
            var p = model.PurchasePresentations[i];
            if (string.IsNullOrWhiteSpace(p.Name) && (!p.BaseQuantityPerPackage.HasValue || p.BaseQuantityPerPackage <= 0))
            {
                ModelState.Remove($"PurchasePresentations[{i}].Name");
                ModelState.Remove($"PurchasePresentations[{i}].BaseQuantityPerPackage");
                ModelState.Remove($"PurchasePresentations[{i}].PurchaseUnitId");
                ModelState.Remove($"PurchasePresentations[{i}].IsActive");
            }
        }
    }
}
