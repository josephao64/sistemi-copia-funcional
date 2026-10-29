using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Catalog;
using SistemiPos.Application.Finance;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Common.Exceptions;
using SistemiPos.Application.Purchases;
using SistemiPos.Application.Organizations;
using SistemiPos.Application.Security;
using SistemiPos.Application.Suppliers;
using SistemiPos.Application.Settings;
using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Purchases;
using SistemiPos.Web.Models.Purchases;

namespace SistemiPos.Web.Controllers;

[Authorize(Policy = PermissionCodes.PurchasesView)]
public sealed class PurchasesController(
    IPurchaseService purchaseService,
    IPurchaseOrderPdfService purchaseOrderPdfService,
    ISupplierService supplierService,
    ICatalogService catalogService,
    ISettingsService settingsService,
    IFinancialService financialService,
    IDateTimeProvider dateTimeProvider,
    IOrganizationService organizationService,
    IOperationContextAccessor operationContext) : Controller
{
    public async Task<IActionResult> Index(PurchaseOrderStatus? status, Guid? supplierId, DateTime? from, DateTime? to, CancellationToken cancellationToken)
        => View(await BuildOrdersIndexAsync("orders", "General", status, supplierId, from, to, cancellationToken));

    public async Task<IActionResult> Drafts(Guid? supplierId, DateTime? from, DateTime? to, CancellationToken cancellationToken)
        => View("Index", await BuildOrdersIndexAsync("drafts", "Borradores", PurchaseOrderStatus.Draft, supplierId, from, to, cancellationToken));

    public async Task<IActionResult> Requested(Guid? supplierId, DateTime? from, DateTime? to, CancellationToken cancellationToken)
        => View("Index", await BuildOrdersIndexAsync("requested", "Solicitadas", PurchaseOrderStatus.Requested, supplierId, from, to, cancellationToken));

    public async Task<IActionResult> Receipts(Guid? supplierId, DateTime? from, DateTime? to, CancellationToken cancellationToken)
        => View("Index", await BuildOrdersIndexAsync("receipts", "Recepciones", PurchaseOrderStatus.Requested, supplierId, from, to, cancellationToken));

    private async Task<PurchasesIndexViewModel> BuildOrdersIndexAsync(string section, string title, PurchaseOrderStatus? status, Guid? supplierId, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var today = dateTimeProvider.UtcNow.ToOffset(TimeSpan.FromHours(-6)).Date;
        from ??= new DateTime(today.Year, today.Month, 1);
        to ??= today;
        IEnumerable<PurchaseOrderDto> orders = await purchaseService.GetPurchaseOrdersAsync(cancellationToken);
        orders = orders.Where(x => x.Status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Requested or PurchaseOrderStatus.PartiallyReceived);
        if (status == PurchaseOrderStatus.Requested)
            orders = orders.Where(x => x.Status is PurchaseOrderStatus.Requested or PurchaseOrderStatus.PartiallyReceived);
        else if (status.HasValue)
            orders = orders.Where(x => x.Status == status);
        if (supplierId.HasValue) orders = orders.Where(x => x.SupplierId == supplierId);
        if (from.HasValue) orders = orders.Where(x => x.RequestedAtUtc.Date >= from.Value.Date);
        if (to.HasValue) orders = orders.Where(x => x.RequestedAtUtc.Date <= to.Value.Date);
        var suppliers = await supplierService.GetSuppliersAsync(cancellationToken);
        return new PurchasesIndexViewModel
        {
            Orders = orders.OrderByDescending(x => x.RequestedAtUtc).ToList(), Status = status, SupplierId = supplierId,
            SupplierName = suppliers.FirstOrDefault(x => x.Id == supplierId) is { } selected ? FormatSupplierName(selected) : null,
            From = from, To = to, Section = section, Title = title,
            Suppliers = suppliers.OrderBy(x => x.LegalName).Select(x => new SelectListItem(FormatSupplierName(x), x.Id.ToString())).ToList()
        };
    }

    public async Task<IActionResult> History(Guid? supplierId, DateTime? from, DateTime? to, PurchaseTermsType? terms,
        DocumentStatus? status, CancellationToken cancellationToken)
    {
        var today = dateTimeProvider.UtcNow.ToOffset(TimeSpan.FromHours(-6)).Date;
        var fromValue = from ?? new DateTime(today.Year, today.Month, 1);
        var toValue = to ?? today;
        IEnumerable<PurchaseDto> purchases = await purchaseService.GetPurchasesAsync(cancellationToken);
        purchases = purchases.Where(x => x.Status != DocumentStatus.Draft
            && x.PurchaseDateUtc.ToOffset(TimeSpan.FromHours(-6)).Date >= fromValue.Date
            && x.PurchaseDateUtc.ToOffset(TimeSpan.FromHours(-6)).Date <= toValue.Date);
        if (supplierId.HasValue) purchases = purchases.Where(x => x.SupplierId == supplierId);
        if (terms.HasValue) purchases = purchases.Where(x => x.Terms == terms);
        if (status.HasValue) purchases = purchases.Where(x => x.Status == status);
        var suppliers = await supplierService.GetSuppliersAsync(cancellationToken);
        var settings = await settingsService.GetSystemSettingAsync(cancellationToken);
        return View(new PurchaseHistoryViewModel
        {
            Purchases = purchases.ToList(), SupplierId = supplierId,
            SupplierName = suppliers.FirstOrDefault(x => x.Id == supplierId) is { } selected ? FormatSupplierName(selected) : null,
            From = fromValue, To = toValue, Terms = terms, Status = status,
            Suppliers = suppliers.OrderBy(x => x.LegalName).Select(x => new SelectListItem(FormatSupplierName(x), x.Id.ToString())).ToList(),
            CurrencySymbol = settings.CurrencySymbol, CurrencyDecimals = settings.CurrencyDecimals
        });
    }

    [HttpGet]
    public async Task<IActionResult> CompareSuppliers(Guid? productId, CancellationToken cancellationToken)
    {
        var products = await catalogService.SearchProductsAsync(null, cancellationToken);
        var suppliers = await supplierService.GetSuppliersAsync(cancellationToken);
        var settings = await settingsService.GetSystemSettingAsync(cancellationToken);
        var product = products.FirstOrDefault(x => x.Id == productId);
        return View(new SupplierComparisonViewModel
        {
            ProductId = productId, ProductName = product?.DisplayName ?? product?.Name ?? string.Empty,
            Rows = productId.HasValue ? await purchaseService.GetProductSuppliersAsync(productId, cancellationToken) : [],
            Products = products.Where(x => x.IsActive && !x.ManagesVariants)
                .OrderBy(x => x.DisplayName ?? x.Name).Select(x => new SelectListItem(x.DisplayName ?? x.Name, x.Id.ToString())).ToList(),
            Suppliers = suppliers.Where(x => x.IsActive).OrderBy(x => x.LegalName)
                .Select(x => new SelectListItem(FormatSupplierName(x), x.Id.ToString())).ToList(),
            Relation = new SupplierProductFormViewModel { ProductId = productId ?? Guid.Empty },
            CurrencySymbol = settings.CurrencySymbol, CurrencyDecimals = settings.CurrencyDecimals
        });
    }

    [Authorize(Policy = PermissionCodes.PurchasesConfirm), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProductSupplier(SupplierProductFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Revise los datos de la relación entre producto y proveedor.";
            return RedirectToAction(nameof(CompareSuppliers), new { productId = model.ProductId });
        }
        try
        {
            await purchaseService.SaveProductSupplierAsync(new SaveProductSupplierRequest(model.ProductId,
                model.SupplierId, model.SupplierProductName, model.SupplierProductCode, model.PackageType,
                model.UnitsPerPackage, model.Terms, model.CreditDays), cancellationToken);
            TempData["Success"] = "La relación con el proveedor fue guardada correctamente.";
        }
        catch (Exception ex) when (ex is DomainRuleException or NotFoundException)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(CompareSuppliers), new { productId = model.ProductId });
    }

    [HttpGet]
    public async Task<IActionResult> SupplierPriceHistory(Guid productId, Guid? supplierId, CancellationToken cancellationToken)
    {
        var products = await catalogService.SearchProductsAsync(null, cancellationToken);
        var product = products.FirstOrDefault(x => x.Id == productId);
        if (product is null) return NotFound();
        var settings = await settingsService.GetSystemSettingAsync(cancellationToken);
        return View(new SupplierPriceHistoryViewModel
        {
            ProductName = product.DisplayName ?? product.Name,
            Rows = await purchaseService.GetSupplierProductPriceHistoryAsync(productId, supplierId, cancellationToken),
            CurrencySymbol = settings.CurrencySymbol, CurrencyDecimals = settings.CurrencyDecimals
        });
    }

    [HttpGet]
    public async Task<IActionResult> SupplierProductInfo(Guid productId, Guid supplierId, CancellationToken cancellationToken)
    {
        var relation = (await purchaseService.GetProductSuppliersAsync(productId, cancellationToken))
            .FirstOrDefault(x => x.SupplierId == supplierId);
        return relation is null ? Json(null) : Json(new
        {
            relation.SupplierProductName, relation.SupplierProductCode,
            packageType = (int)relation.PackageType, relation.UnitsPerPackage,
            relation.LastPackageCost, relation.PackageName,
            hasPurchaseHistory = relation.LastPurchaseDateUtc.HasValue
        });
    }

    [Authorize(Policy = PermissionCodes.PurchasesManage), HttpGet]
    public async Task<IActionResult> SearchOrderProducts(string? query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query)) return Json(Array.Empty<object>());

        var products = await catalogService.SearchProductsAsync(query, cancellationToken);
        return Json(products
            .Where(x => x.IsActive && !x.ManagesVariants)
            .Take(12)
            .Select(x =>
            {
                var option = MapPurchaseProductOption(x);
                return new
                {
                    option.Id,
                    option.SystemName,
                    option.SupplierName,
                    option.SystemDescription,
                    option.SupplierDescription,
                    option.SuggestionLabel
                };
            }));
    }

    [HttpGet]
    public async Task<IActionResult> SupplierInvoiceExists(Guid supplierId, string invoiceNumber, CancellationToken cancellationToken) =>
        Json(new { exists = await purchaseService.InvoiceExistsForSupplierAsync(supplierId, invoiceNumber, cancellationToken) });

    [Authorize(Policy = PermissionCodes.PurchasesManage)]
    [HttpGet]
    public IActionResult Create() => RedirectToAction(nameof(CreateOrder));

    [Authorize(Policy = PermissionCodes.SuppliersManage), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSupplierInline(string legalName, string nit, string? tradeName, CancellationToken cancellationToken)
    {
        try { var id = await supplierService.CreateSupplierAsync(new SaveSupplierRequest(legalName, nit, tradeName, null, null, null, null, true), cancellationToken); return Json(new { id, text = legalName.Trim(), nit = nit.Trim(), defaultCreditDays = (int?)null, defaultTerms = (int)PurchaseTermsType.Cash }); }
        catch (DomainRuleException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [Authorize(Policy = PermissionCodes.PurchasesManage), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePresentationInline(string name, int units, CancellationToken cancellationToken)
    {
        try
        {
            var presentation = await purchaseService.CreatePurchasePresentationAsync(name, units, cancellationToken);
            return Json(new { id = presentation.Id, name = presentation.Name, units = presentation.Units });
        }
        catch (Exception ex) when (ex is DomainRuleException or NotFoundException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Policy = PermissionCodes.PurchasesConfirm), HttpGet]
    public async Task<IActionResult> DirectPurchase(CancellationToken cancellationToken) =>
        View(await BuildDirectPurchaseFormAsync(new DirectPurchaseFormViewModel
        {
            SupplierDocumentDateLocal = dateTimeProvider.UtcNow.ToOffset(TimeSpan.FromHours(-6)).Date
        }, cancellationToken));

    [Authorize(Policy = PermissionCodes.PurchasesConfirm), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DirectPurchase(DirectPurchaseFormViewModel model, CancellationToken cancellationToken)
    {
        model.Items = model.Items.Where(x => x.ProductId != Guid.Empty || x.PackageQuantity > 0 || x.PackageCost > 0).ToList();
        ModelState.Clear();
        if (model.SupplierId == Guid.Empty)
            ModelState.AddModelError(string.Empty, "Seleccione un proveedor existente.");
        if (model.BranchId == Guid.Empty || model.WarehouseId == Guid.Empty)
            ModelState.AddModelError(string.Empty, "Seleccione la sucursal y la bodega donde ingresará la mercadería.");
        if (model.Items.Count == 0)
            ModelState.AddModelError(string.Empty, "La compra debe incluir al menos un producto.");
        if (model.Items.Any(x => x.ProductId == Guid.Empty))
            ModelState.AddModelError(string.Empty, "Todos los productos de la compra son requeridos.");
        if (model.Items.Where(x => x.ProductId != Guid.Empty).GroupBy(x => x.ProductId).Any(x => x.Count() > 1))
            ModelState.AddModelError(string.Empty, "Un producto no puede repetirse en la compra.");
        if (model.Terms == PurchaseTermsType.Credit && model.CreditDays is null or <= 0)
            ModelState.AddModelError(nameof(model.CreditDays), "Los días de crédito deben ser mayores a cero.");

        if (!TryValidateModel(model))
        {
            ViewData["PurchaseCaptureSaveError"] = "No se pudo guardar la compra. Revisa los datos indicados.";
            return View(await BuildDirectPurchaseFormAsync(model, cancellationToken));
        }

        try
        {
            var purchaseId = await purchaseService.CreateDirectPurchaseAsync(new CreateDirectPurchaseRequest(
                model.SupplierId,
                new DateTimeOffset(DateTime.SpecifyKind(model.SupplierDocumentDateLocal.Date, DateTimeKind.Unspecified), TimeSpan.FromHours(-6)).ToUniversalTime(),
                model.SupplierDocumentNumber,
                model.SupplierNit,
                model.Terms,
                model.CreditDays,
                model.Observations,
                model.Items.Select(x => new DirectPurchaseItemRequest(x.ProductId, x.PackageQuantity,
                    x.UnitsPerPackage, x.PackageCost, x.PackageType, x.SupplierProductName,
                    x.SupplierProductCode, x.ExpiryDate, x.PresentationId, x.DiscountType,
                    x.DiscountValue, new PurchaseProductPricingRequest(x.SalePrice, x.MultiPriceEnabled,
                        ParsePurchasePriceRules(x.MultiPriceRulesJson)), x.LotNumber)).ToList(),
                model.GlobalDiscountType, model.GlobalDiscountValue,
                model.BranchId, model.WarehouseId), cancellationToken);
            TempData["Success"] = "La compra directa fue registrada correctamente.";
            return RedirectToAction(nameof(Details), new { id = purchaseId });
        }
        catch (Exception ex) when (ex is DomainRuleException or NotFoundException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewData["PurchaseCaptureSaveError"] = ex.Message;
            return View(await BuildDirectPurchaseFormAsync(model, cancellationToken));
        }
    }

    [Authorize(Policy = PermissionCodes.PurchasesManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(PurchaseFormViewModel model)
    {
        TempData["Error"] = "Las compras se generan automáticamente al confirmar la recepción de una orden.";
        return RedirectToAction(nameof(CreateOrder));
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var purchase = await purchaseService.GetPurchaseAsync(id, cancellationToken);
        if (purchase is null)
        {
            return NotFound();
        }

        var settings = await settingsService.GetSystemSettingAsync(cancellationToken);
        ViewBag.CurrencySymbol = settings.CurrencySymbol;
        ViewBag.CurrencyDecimals = settings.CurrencyDecimals;
        ViewBag.PayableAccount = purchase.Terms == PurchaseTermsType.Credit
            ? (await financialService.GetPayablesAsync(cancellationToken)).FirstOrDefault(x => x.PurchaseId == purchase.Id)
            : null;
        return View(purchase);
    }

    [Authorize(Policy = PermissionCodes.PurchasesConfirm)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await purchaseService.ConfirmPurchaseAsync(id, cancellationToken);
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (DomainRuleException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    [Authorize(Policy = PermissionCodes.PurchasesCancel), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelPurchase(Guid id, string reason, CancellationToken cancellationToken)
    {
        try
        {
            await purchaseService.CancelPurchaseAsync(id, reason, cancellationToken);
            TempData["Success"] = "Compra anulada. Se revirtieron sus existencias y se reabrieron los pendientes de la orden cuando corresponde.";
        }
        catch (NotFoundException) { return NotFound(); }
        catch (DomainRuleException ex) { TempData["Error"] = ex.Message; }
        catch (DbUpdateConcurrencyException)
        { TempData["Error"] = "La compra, sus existencias o sus pagos cambiaron durante la operación. Actualiza y vuelve a intentarlo."; }
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = PermissionCodes.PurchasesManage)]
    [HttpGet]
    public async Task<IActionResult> CreateOrder(CancellationToken cancellationToken) =>
        View("OrderForm", await BuildOrderFormAsync(new PurchaseOrderFormViewModel(), cancellationToken));

    [Authorize(Policy = PermissionCodes.PurchasesManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOrder(PurchaseOrderFormViewModel model, string intent, CancellationToken cancellationToken)
    {
        model.Items = model.Items.Where(x => x.ProductId != Guid.Empty || x.PackageQuantity > 0).ToList();
        ModelState.Clear();
        if (model.SupplierId == Guid.Empty)
        {
            ModelState.AddModelError(string.Empty, "Seleccione un proveedor existente.");
        }
        if (model.BranchId == Guid.Empty || model.WarehouseId == Guid.Empty)
            ModelState.AddModelError(string.Empty, "Seleccione la sucursal y la bodega donde ingresará la mercadería.");
        if (model.Items.Any(x => x.ProductId == Guid.Empty))
        {
            ModelState.AddModelError(string.Empty, "Todos los productos de la orden son requeridos.");
        }
        if (model.Items.Any(x => x.PresentationId == Guid.Empty))
        {
            ModelState.AddModelError(string.Empty, "Seleccione una presentación para cada producto.");
        }
        if (model.Items.Where(x => x.ProductId != Guid.Empty).GroupBy(x => x.ProductId).Any(group => group.Count() > 1))
        {
            ModelState.AddModelError(string.Empty, "El producto ya forma parte de la orden.");
        }
        if (!TryValidateModel(model) || model.Items.Count == 0)
        {
            if (model.Items.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "La orden debe incluir al menos un producto.");
            }
            return View("OrderForm", await BuildOrderFormAsync(model, cancellationToken));
        }

        try
        {
            var submit = string.Equals(intent, "submit", StringComparison.OrdinalIgnoreCase);
            var id = await purchaseService.CreatePurchaseOrderAsync(new CreatePurchaseOrderRequest(
                model.SupplierId,
                model.Items.Select(x => new PurchaseOrderItemRequest(x.ProductId, x.PackageQuantity, x.PresentationId)).ToList(),
                model.Observations,
                submit,
                model.BranchId,
                model.WarehouseId), cancellationToken);
            TempData["Success"] = submit
                ? "La orden fue guardada y marcada como solicitada."
                : "El borrador de la orden fue guardado correctamente.";
            return submit
                ? RedirectToAction(nameof(OrderDetails), new { id })
                : RedirectToAction(nameof(CreateOrder));
        }
        catch (Exception ex) when (ex is DomainRuleException or NotFoundException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("OrderForm", await BuildOrderFormAsync(model, cancellationToken));
        }
    }

    [Authorize(Policy = PermissionCodes.PurchasesManage), HttpGet]
    public async Task<IActionResult> EditOrder(Guid id, CancellationToken cancellationToken)
    {
        var order = await purchaseService.GetPurchaseOrderAsync(id, cancellationToken);
        if (order is null) return NotFound();
        if (order.Receipts is { Count: > 0 } || order.Status is not (SistemiPos.Domain.Purchases.PurchaseOrderStatus.Draft or SistemiPos.Domain.Purchases.PurchaseOrderStatus.Requested))
            return RedirectToAction(nameof(OrderDetails), new { id });
        return View("OrderForm", await BuildOrderFormAsync(new PurchaseOrderFormViewModel
        {
            Id = order.Id,
            Number = order.Number,
            Status = order.Status,
            SupplierId = order.SupplierId,
            SupplierName = order.SupplierName,
            BranchId = order.BranchId ?? Guid.Empty,
            WarehouseId = order.WarehouseId ?? Guid.Empty,
            RequestedAtLocal = order.RequestedAtUtc.ToOffset(TimeSpan.FromHours(-6)).DateTime,
            Observations = order.Observations,
            Items = order.Items.Where(x => !x.IsAdditional).Select(x => new PurchaseOrderItemFormViewModel
            {
                ProductId = x.ProductId,
                ProductName = x.ProductName,
                PackageQuantity = x.PackageQuantity,
                PresentationId = x.PresentationId ?? Guid.Empty,
                PresentationName = x.PresentationName,
                UnitsPerPackage = x.UnitsPerPackage,
                BaseQuantityPerPackage = x.BaseQuantityPerPackage,
                BaseUnitCode = x.BaseUnitCode,
                SupplierInvoiceName = x.SupplierInvoiceName,
                SystemDescription = x.SystemDescription,
                SupplierDescription = x.SupplierDescription
            }).ToList()
        }, cancellationToken));
    }

    [Authorize(Policy = PermissionCodes.PurchasesManage), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditOrder(PurchaseOrderFormViewModel model, string intent, CancellationToken cancellationToken)
    {
        if (model.Id is null) return BadRequest();
        model.Items = model.Items.Where(x => x.ProductId != Guid.Empty || x.PackageQuantity > 0).ToList();
        ModelState.Clear();
        if (model.SupplierId == Guid.Empty) ModelState.AddModelError(string.Empty, "Seleccione un proveedor existente.");
        if (model.BranchId == Guid.Empty || model.WarehouseId == Guid.Empty) ModelState.AddModelError(string.Empty, "Seleccione la sucursal y la bodega donde ingresará la mercadería.");
        if (model.Items.Count == 0) ModelState.AddModelError(string.Empty, "La orden debe incluir al menos un producto.");
        if (model.Items.Any(x => x.ProductId == Guid.Empty)) ModelState.AddModelError(string.Empty, "Todos los productos son requeridos.");
        if (model.Items.Any(x => x.PresentationId == Guid.Empty)) ModelState.AddModelError(string.Empty, "Seleccione una presentación para cada producto.");
        if (model.Items.GroupBy(x => x.ProductId).Any(x => x.Count() > 1)) ModelState.AddModelError(string.Empty, "El producto ya forma parte de la orden.");
        if (!TryValidateModel(model)) return View("OrderForm", await BuildOrderFormAsync(model, cancellationToken));
        try
        {
            var submit = string.Equals(intent, "submit", StringComparison.OrdinalIgnoreCase);
            await purchaseService.UpdatePurchaseOrderAsync(model.Id.Value, new UpdatePurchaseOrderRequest(
                model.SupplierId,
                model.Items.Select(x => new PurchaseOrderItemRequest(x.ProductId, x.PackageQuantity, x.PresentationId)).ToList(),
                model.Observations,
                submit,
                model.BranchId,
                model.WarehouseId), cancellationToken);
            TempData["Success"] = submit
                ? "La orden fue guardada y marcada como solicitada."
                : model.Status == PurchaseOrderStatus.Requested
                    ? "Los cambios de la orden fueron guardados correctamente."
                    : "El borrador de la orden fue guardado correctamente.";
            return RedirectToAction(nameof(OrderDetails), new { id = model.Id });
        }
        catch (Exception ex) when (ex is DomainRuleException or NotFoundException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("OrderForm", await BuildOrderFormAsync(model, cancellationToken));
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            TempData["Error"] = "La orden fue modificada por otro proceso. Se recargaron los datos actuales; revise la orden y vuelva a intentarlo.";
            return RedirectToAction(nameof(EditOrder), new { id = model.Id });
        }
    }

    [HttpGet]
    public async Task<IActionResult> OrderDetails(Guid id, CancellationToken cancellationToken)
    {
        var order = await purchaseService.GetPurchaseOrderAsync(id, cancellationToken);
        return order is null ? NotFound() : View(order);
    }

    [Authorize(Policy = PermissionCodes.PurchasesManage), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitOrder(Guid id, CancellationToken cancellationToken)
    {
        try { await purchaseService.SubmitPurchaseOrderAsync(id, cancellationToken); TempData["Success"] = "La orden fue marcada como solicitada."; }
        catch (DomainRuleException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(OrderDetails), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> OrderPdf(Guid id, bool download = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var order = await purchaseService.GetPurchaseOrderAsync(id, cancellationToken);
            if (order is null) return NotFound();
            var bytes = await purchaseOrderPdfService.GenerateAsync(id, cancellationToken);
            Response.Headers.ContentDisposition = $"{(download ? "attachment" : "inline")}; filename=orden-{order.Number}.pdf";
            return File(bytes, "application/pdf");
        }
        catch (DomainRuleException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(OrderDetails), new { id });
        }
    }

    [Authorize(Policy = PermissionCodes.PurchasesConfirm)]
    [HttpGet]
    public async Task<IActionResult> ReceiveOrder(Guid id, CancellationToken cancellationToken)
    {
        var order = await purchaseService.GetPurchaseOrderAsync(id, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }
        if (order.Status is not (PurchaseOrderStatus.Requested or PurchaseOrderStatus.PartiallyReceived))
            return RedirectToAction(nameof(OrderDetails), new { id });

        var settings = await settingsService.GetSystemSettingAsync(cancellationToken);
        ViewBag.CurrencySymbol = settings.CurrencySymbol;
        ViewBag.CurrencyDecimals = settings.CurrencyDecimals;
        ViewBag.TaxPercent = settings.TaxPercent;
        ViewBag.SupplierId = order.SupplierId;
        var supplier = await supplierService.GetSupplierAsync(order.SupplierId, cancellationToken);
        var defaultCreditDays = supplier?.DefaultCreditDays;
        var products = await catalogService.SearchProductsAsync(null, cancellationToken);
        var supplierProducts = (await purchaseService.GetProductSuppliersAsync(null, cancellationToken))
            .Where(x => x.SupplierId == order.SupplierId).ToDictionary(x => x.ProductId);
        var presentations = await BuildCapturePresentationsAsync(cancellationToken);
        var unitPresentationId = presentations.FirstOrDefault(value => value.Units == 1)?.Id ?? Guid.Empty;
        return View(new ReceivePurchaseOrderViewModel
        {
            OrderId = order.Id,
            Number = order.Number,
            SupplierName = order.SupplierName,
            BranchId = order.BranchId ?? Guid.Empty,
            BranchName = order.BranchName ?? string.Empty,
            WarehouseId = order.WarehouseId ?? Guid.Empty,
            WarehouseName = order.WarehouseName ?? string.Empty,
            SupplierNit = supplier?.Nit ?? string.Empty,
            SupplierDocumentDateLocal = dateTimeProvider.UtcNow.ToOffset(TimeSpan.FromHours(-6)).Date,
            Terms = supplier?.DefaultTerms ?? PurchaseTermsType.Cash,
            CreditDays = defaultCreditDays,
            Items = order.Items.Where(x => !x.IsAdditional && x.PendingQuantity > 0).Select(x =>
            {
                var relation = supplierProducts.GetValueOrDefault(x.ProductId);
                var product = products.FirstOrDefault(value => value.Id == x.ProductId);
                return new ReceivePurchaseOrderItemViewModel
                {
                    ItemId = x.Id, ProductName = x.ProductName, SupplierInvoiceName = relation?.SupplierProductName,
                    RequestedQuantity = x.RequestedQuantity, ReceivedQuantity = x.ReceivedQuantity,
                    PendingQuantity = x.PendingQuantity, Quantity = x.PendingQuantity, ProductId = x.ProductId,
                    ManagesExpiry = product?.ManagesExpiry == true,
                    ManagesLots = product?.ManagesLots == true,
                    SupplierProductName = relation?.SupplierProductName ?? x.ProductName,
                    SupplierProductCode = relation?.SupplierProductCode,
                    PackageType = relation?.PackageType ?? SupplierPackageType.Other,
                    PackageQuantity = x.PendingQuantity > 0
                        ? decimal.Round(x.PendingQuantity / Math.Max(x.BaseQuantityPerPackage, 1), 2)
                        : 0,
                    UnitsPerPackage = x.UnitsPerPackage > 0 ? x.UnitsPerPackage : 1,
                    PresentationName = x.PresentationName,
                    PackageCost = relation?.LastPackageCost ?? 0,
                    LastPurchaseCost = relation?.LastPurchaseDateUtc.HasValue == true
                        ? relation.LastPackageCost
                        : null,
                    PresentationId = x.PresentationId ?? unitPresentationId,
                    SalePrice = product?.SalePrice ?? 0,
                    AppliesTax = product?.AppliesTax ?? true,
                    MultiPriceEnabled = product?.MultiPriceEnabled ?? false,
                    MultiPriceRulesJson = SerializePurchasePriceRules(product?.PriceRules),
                    PricingInitialized = true
                };
            }).ToList(),
            Products = await BuildProductOptionsAsync(cancellationToken),
            Presentations = await BuildCapturePresentationsAsync(cancellationToken)
        });
    }

    [Authorize(Policy = PermissionCodes.PurchasesConfirm)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReceiveOrder(ReceivePurchaseOrderViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            if (model.Terms == PurchaseTermsType.Credit && model.CreditDays is null)
                throw new DomainRuleException("Indique los días de crédito.");
            await purchaseService.ReceivePurchaseOrderAsync(model.OrderId, new ReceivePurchaseOrderRequest(
                model.Items.Select(x => new ReceivePurchaseOrderItemRequest(x.ItemId,
                    x.PackageQuantity * x.UnitsPerPackage,
                    x.UnitsPerPackage > 0 ? x.PackageCost / x.UnitsPerPackage : 0,
                    x.ProductId, x.IsAdditional, x.ExpiryDate, x.PackageType, x.PackageQuantity,
                    x.UnitsPerPackage, x.PackageCost, x.SupplierProductName, x.SupplierProductCode,
                    x.PresentationId, x.DiscountType, x.DiscountValue,
                    new PurchaseProductPricingRequest(x.SalePrice, x.MultiPriceEnabled,
                        ParsePurchasePriceRules(x.MultiPriceRulesJson)), x.LotNumber)).ToList(),
                model.SupplierDocumentNumber,
                new DateTimeOffset(DateTime.SpecifyKind(model.SupplierDocumentDateLocal, DateTimeKind.Unspecified), TimeSpan.FromHours(-6)).ToUniversalTime(),
                model.Terms,
                model.CreditDays,
                model.Observations, model.GlobalDiscountType, model.GlobalDiscountValue,
                model.SupplierNit), cancellationToken);
            return RedirectToAction(nameof(OrderDetails), new { id = model.OrderId });
        }
        catch (Exception ex) when (ex is DomainRuleException or NotFoundException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewData["PurchaseCaptureSaveError"] = ex.Message;
            var order = await purchaseService.GetPurchaseOrderAsync(model.OrderId, cancellationToken);
            if (order is null) return NotFound();
            model.Number = order.Number;
            model.SupplierName = order.SupplierName;
            model.SupplierNit = (await supplierService.GetSupplierAsync(order.SupplierId, cancellationToken))?.Nit ?? string.Empty;
            ViewBag.SupplierId = order.SupplierId;
            var supplierProducts = (await purchaseService.GetProductSuppliersAsync(null, cancellationToken))
                .Where(x => x.SupplierId == order.SupplierId)
                .ToDictionary(x => x.ProductId);
            foreach (var item in model.Items)
            {
                var relation = supplierProducts.GetValueOrDefault(item.ProductId);
                item.LastPurchaseCost = relation?.LastPurchaseDateUtc.HasValue == true
                    ? relation.LastPackageCost
                    : null;
                var source = order.Items.FirstOrDefault(x => x.Id == item.ItemId);
                if (source is null) continue;
                item.ProductName = source.ProductName;
                item.SupplierInvoiceName = source.SupplierInvoiceName;
                item.RequestedQuantity = source.RequestedQuantity;
                item.PresentationName = source.PresentationName;
                item.ReceivedQuantity = source.ReceivedQuantity;
                item.PendingQuantity = source.PendingQuantity;
            }
            model.Products = await BuildProductOptionsAsync(cancellationToken);
            model.Presentations = await BuildCapturePresentationsAsync(cancellationToken);
            var settings = await settingsService.GetSystemSettingAsync(cancellationToken);
            ViewBag.CurrencySymbol = settings.CurrencySymbol;
            ViewBag.CurrencyDecimals = settings.CurrencyDecimals;
            ViewBag.TaxPercent = settings.TaxPercent;
            return View(model);
        }
    }

    [Authorize(Policy = PermissionCodes.PurchasesManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelOrder(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await purchaseService.CancelPurchaseOrderAsync(id, cancellationToken);
        }
        catch (DomainRuleException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(OrderDetails), new { id });
    }

    private async Task<PurchaseFormViewModel> BuildFormAsync(PurchaseFormViewModel model, CancellationToken cancellationToken)
    {
        var suppliers = await supplierService.GetSuppliersAsync(cancellationToken);
        var products = await catalogService.SearchProductsAsync(null, cancellationToken);

        model.Suppliers = suppliers
            .Where(x => x.IsActive || x.Id == model.SupplierId)
            .Select(x => new SelectListItem(x.LegalName, x.Id.ToString()))
            .ToList();

        model.Products = products
            .Where(x => x.IsActive && !x.ManagesVariants)
            .Select(x => new SelectListItem(FormatPurchaseProductName(x), x.Id.ToString()))
            .ToList();

        foreach (var item in model.Items)
            item.ProductName = model.Products.FirstOrDefault(x => x.Value == item.ProductId.ToString())?.Text ?? item.ProductName;

        if (model.Items.Count == 0)
        {
            model.Items.Add(new PurchaseItemFormViewModel());
        }

        return model;
    }

    private async Task<PurchaseOrderFormViewModel> BuildOrderFormAsync(PurchaseOrderFormViewModel model, CancellationToken cancellationToken)
    {
        var suppliers = await supplierService.GetSuppliersAsync(cancellationToken);
        var products = await catalogService.SearchProductsAsync(null, cancellationToken);
        var presentations = await BuildCapturePresentationsAsync(cancellationToken);
        model.Suppliers = suppliers.Where(x => x.IsActive || x.Id == model.SupplierId)
            .Select(x => new SelectListItem(FormatSupplierName(x), x.Id.ToString())).ToList();
        model.Products = products.Where(x => x.IsActive && !x.ManagesVariants)
            .Select(MapPurchaseProductOption).ToList();
        model.Presentations = presentations;
        await PopulateDestinationOptionsAsync(model, cancellationToken);
        model.SupplierName = model.Suppliers.FirstOrDefault(x => x.Value == model.SupplierId.ToString())?.Text ?? model.SupplierName;
        var unitPresentation = presentations.FirstOrDefault(x => x.Units == 1);
        foreach (var item in model.Items)
        {
            if (item.PresentationId == Guid.Empty && unitPresentation is not null)
                item.PresentationId = unitPresentation.Id;
            if (presentations.FirstOrDefault(x => x.Id == item.PresentationId) is { } presentation)
            {
                item.PresentationName = presentation.Name;
                item.UnitsPerPackage = presentation.Units;
                item.BaseQuantityPerPackage = presentation.BaseFactor ?? presentation.Units;
                item.BaseUnitCode = presentation.BaseUnit;
            }
            var product = model.Products.FirstOrDefault(x => x.Id == item.ProductId);
            if (product is null) continue;
            item.ProductName = product.SystemName;
            item.SupplierInvoiceName = product.SupplierName;
            item.SystemDescription = product.SystemDescription;
            item.SupplierDescription = product.SupplierDescription;
        }
        if (model.Id is null)
            model.RequestedAtLocal = dateTimeProvider.UtcNow.ToOffset(TimeSpan.FromHours(-6)).DateTime;
        if (model.Items.Count == 0) model.Items.Add(new PurchaseOrderItemFormViewModel());
        return model;
    }

    private async Task<IReadOnlyList<PurchasePresentationOptionViewModel>> BuildCapturePresentationsAsync(CancellationToken cancellationToken)
    {
        var legacy = await purchaseService.GetPurchasePresentationsAsync(cancellationToken);
        var products = await catalogService.SearchProductsAsync(null, cancellationToken);
        return legacy.Select(x => new PurchasePresentationOptionViewModel(x.Id, x.Name, x.Units))
            .Concat(products.Where(x => x.IsActive).SelectMany(product => (product.PurchasePresentations ?? [])
                .Where(x => x.IsActive).Select(x => new PurchasePresentationOptionViewModel(
                    x.Id, x.Name, 1, x.BaseQuantityPerPackage, product.Id, product.BaseUnitCode ?? product.UnitOfMeasure))))
            .ToList();
    }

    private async Task<DirectPurchaseFormViewModel> BuildDirectPurchaseFormAsync(DirectPurchaseFormViewModel model, CancellationToken cancellationToken)
    {
        var suppliers = await supplierService.GetSuppliersAsync(cancellationToken);
        var products = await catalogService.SearchProductsAsync(null, cancellationToken);
        var presentations = await purchaseService.GetPurchasePresentationsAsync(cancellationToken);
        await PopulateDestinationOptionsAsync(model, cancellationToken);
        model.Suppliers = suppliers
            .Where(x => x.IsActive || x.Id == model.SupplierId)
            .OrderBy(x => x.LegalName)
            .Select(x => new DirectPurchaseSupplierOptionViewModel(x.Id, FormatSupplierName(x), x.Nit,
                x.DefaultCreditDays, x.DefaultTerms))
            .ToList();
        model.Products = products
            .Where(x => x.IsActive && !x.ManagesVariants)
            .Select(MapPurchaseProductOption)
            .ToList();

        var selectedSupplier = suppliers.FirstOrDefault(x => x.Id == model.SupplierId);
        var supplierProducts = model.SupplierId == Guid.Empty
            ? new Dictionary<Guid, ProductSupplierDto>()
            : (await purchaseService.GetProductSuppliersAsync(null, cancellationToken))
                .Where(x => x.SupplierId == model.SupplierId).ToDictionary(x => x.ProductId);
        if (selectedSupplier is not null)
        {
            model.SupplierName = FormatSupplierName(selectedSupplier);
            if (string.IsNullOrWhiteSpace(model.SupplierNit)) model.SupplierNit = selectedSupplier.Nit;
        }
        foreach (var item in model.Items)
        {
            var product = model.Products.FirstOrDefault(x => x.Id == item.ProductId);
            if (product is null) continue;
            item.ProductName = product.SystemName;
            item.ManagesExpiry = product.ManagesExpiry;
            item.ManagesLots = product.ManagesLots;
            if (!item.PricingInitialized)
            {
                item.SalePrice = product.SalePrice;
                item.AppliesTax = product.AppliesTax;
                item.MultiPriceEnabled = product.MultiPriceEnabled;
                item.MultiPriceRulesJson = SerializePurchasePriceRules(product.PriceRules);
                item.PricingInitialized = true;
            }
            if (supplierProducts.GetValueOrDefault(item.ProductId) is { } relation)
            {
                item.LastPurchaseCost = relation.LastPurchaseDateUtc.HasValue ? relation.LastPackageCost : null;
                if (string.IsNullOrWhiteSpace(item.SupplierProductName)) item.SupplierProductName = relation.SupplierProductName;
                if (string.IsNullOrWhiteSpace(item.SupplierProductCode)) item.SupplierProductCode = relation.SupplierProductCode;
                if (item.UnitsPerPackage <= 1 && item.PackageQuantity == 0)
                {
                    item.PackageType = relation.PackageType;
                    item.UnitsPerPackage = relation.UnitsPerPackage;
                    item.PackageCost = relation.LastPackageCost;
                }
                if (item.PresentationId == Guid.Empty)
                    item.PresentationId = presentations.FirstOrDefault(value => value.Units == relation.UnitsPerPackage
                        && string.Equals(value.Name, relation.PackageName, StringComparison.OrdinalIgnoreCase))?.Id
                        ?? presentations.FirstOrDefault(value => value.Units == relation.UnitsPerPackage)?.Id
                        ?? Guid.Empty;
            }
        }
        if (model.Items.Count == 0)
            model.Items.Add(new DirectPurchaseItemFormViewModel
            {
                PresentationId = presentations.FirstOrDefault(x => x.Units == 1)?.Id ?? Guid.Empty
            });
        var settings = await settingsService.GetSystemSettingAsync(cancellationToken);
        ViewBag.CurrencySymbol = settings.CurrencySymbol;
        ViewBag.CurrencyDecimals = settings.CurrencyDecimals;
        ViewBag.TaxPercent = settings.TaxPercent;
        model.Presentations = await BuildCapturePresentationsAsync(cancellationToken);
        return model;
    }

    private async Task PopulateDestinationOptionsAsync(PurchaseOrderFormViewModel model, CancellationToken cancellationToken)
    {
        var branches = await operationContext.GetAuthorizedBranchesAsync(cancellationToken);
        var branchIds = branches.Where(x => x.IsActive).Select(x => x.Id).ToHashSet();
        var warehouses = (await organizationService.GetWarehousesAsync(onlyActive: true, cancellationToken: cancellationToken))
            .Where(x => branchIds.Contains(x.BranchId)).ToList();
        model.Branches = branches.Where(x => x.IsActive)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList();
        model.Warehouses = warehouses.Select(x => new PurchaseWarehouseOptionViewModel(
            x.Id, x.BranchId, $"{x.Name} ({x.BranchName})")).ToList();
        if (model.BranchId == Guid.Empty)
            model.BranchId = operationContext.CurrentBranchId is Guid branchId && branchIds.Contains(branchId)
                ? branchId : branchIds.FirstOrDefault();
        if (model.WarehouseId == Guid.Empty || warehouses.All(x => x.Id != model.WarehouseId || x.BranchId != model.BranchId))
            model.WarehouseId = operationContext.CurrentWarehouseId is Guid warehouseId
                && warehouses.Any(x => x.Id == warehouseId && x.BranchId == model.BranchId)
                ? warehouseId
                : warehouses.FirstOrDefault(x => x.BranchId == model.BranchId)?.Id ?? Guid.Empty;
    }

    private async Task PopulateDestinationOptionsAsync(DirectPurchaseFormViewModel model, CancellationToken cancellationToken)
    {
        var branches = await operationContext.GetAuthorizedBranchesAsync(cancellationToken);
        var branchIds = branches.Where(x => x.IsActive).Select(x => x.Id).ToHashSet();
        var warehouses = (await organizationService.GetWarehousesAsync(onlyActive: true, cancellationToken: cancellationToken))
            .Where(x => branchIds.Contains(x.BranchId)).ToList();
        model.Branches = branches.Where(x => x.IsActive)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList();
        model.Warehouses = warehouses.Select(x => new PurchaseWarehouseOptionViewModel(
            x.Id, x.BranchId, $"{x.Name} ({x.BranchName})")).ToList();
        if (model.BranchId == Guid.Empty)
            model.BranchId = operationContext.CurrentBranchId is Guid branchId && branchIds.Contains(branchId)
                ? branchId : branchIds.FirstOrDefault();
        if (model.WarehouseId == Guid.Empty || warehouses.All(x => x.Id != model.WarehouseId || x.BranchId != model.BranchId))
            model.WarehouseId = operationContext.CurrentWarehouseId is Guid warehouseId
                && warehouses.Any(x => x.Id == warehouseId && x.BranchId == model.BranchId)
                ? warehouseId
                : warehouses.FirstOrDefault(x => x.BranchId == model.BranchId)?.Id ?? Guid.Empty;
    }

    private async Task<IReadOnlyList<PurchaseProductOptionViewModel>> BuildProductOptionsAsync(CancellationToken cancellationToken)
    {
        var products = await catalogService.SearchProductsAsync(null, cancellationToken);
        return products.Where(x => x.IsActive && !x.ManagesVariants)
            .Select(MapPurchaseProductOption).ToList();
    }

    private static string FormatSupplierName(SupplierDto supplier) =>
        string.IsNullOrWhiteSpace(supplier.TradeName)
            ? $"{supplier.LegalName} — NIT {supplier.Nit}"
            : $"{supplier.LegalName} ({supplier.TradeName}) — NIT {supplier.Nit}";

    private static string FormatPurchaseProductName(ProductDto product) =>
        string.Join(" · ", new[] { product.InternalCode, product.DisplayName ?? product.Name, product.SupplierInvoiceName }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

    private static PurchaseProductOptionViewModel MapPurchaseProductOption(ProductDto product)
    {
        var systemName = product.DisplayName ?? product.Name;
        var suggestion = string.Join(" · ", new[]
        {
            systemName,
            product.SupplierInvoiceName,
            product.Description,
            product.SupplierDescription
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
        return new PurchaseProductOptionViewModel(product.Id, systemName, product.SupplierInvoiceName,
            product.Description, product.SupplierDescription, suggestion, product.ManagesExpiry,
            product.SalePrice, product.AppliesTax, product.MultiPriceEnabled,
            (product.PriceRules ?? []).Where(x => x.IsActive)
                .Select(x => new PurchasePriceRuleFormViewModel(x.Quantity, x.Price)).ToList(), product.ManagesLots);
    }

    private static string SerializePurchasePriceRules(IReadOnlyList<ProductPriceRuleDto>? rules) =>
        JsonSerializer.Serialize((rules ?? []).Where(x => x.IsActive)
            .Select(x => new PurchasePriceRuleFormViewModel(x.Quantity, x.Price)));

    private static string SerializePurchasePriceRules(IReadOnlyList<PurchasePriceRuleFormViewModel>? rules) =>
        JsonSerializer.Serialize(rules ?? []);

    private static IReadOnlyList<PurchasePriceRuleRequest> ParsePurchasePriceRules(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return (JsonSerializer.Deserialize<List<PurchasePriceRuleFormViewModel>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [])
                .Select(x => new PurchasePriceRuleRequest(x.Quantity, x.Price)).ToList();
        }
        catch (JsonException)
        {
            throw new DomainRuleException("La configuración de multiprecios no es válida.");
        }
    }
}
