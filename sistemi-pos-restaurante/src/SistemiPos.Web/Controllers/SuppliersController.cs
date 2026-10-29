using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemiPos.Application.Purchases;
using SistemiPos.Application.Security;
using SistemiPos.Application.Suppliers;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Suppliers;
using SistemiPos.Web.Models.Suppliers;

namespace SistemiPos.Web.Controllers;

[Authorize(Policy = PermissionCodes.SuppliersView)]
public sealed class SuppliersController(ISupplierService supplierService, IPurchaseService purchaseService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var suppliers = await supplierService.GetSuppliersAsync(cancellationToken);
        return View(suppliers);
    }

    public async Task<IActionResult> History(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await supplierService.GetSupplierAsync(id, cancellationToken);
        if (supplier is null)
        {
            return NotFound();
        }

        ViewData["SupplierName"] = supplier.LegalName;
        var purchases = await purchaseService.GetPurchasesBySupplierAsync(id, cancellationToken);
        return View(purchases);
    }

    [Authorize(Policy = PermissionCodes.SuppliersManage)]
    [HttpGet]
    public IActionResult Create() => View("Form", new SupplierFormViewModel());

    [Authorize(Policy = PermissionCodes.SuppliersManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SupplierFormViewModel model, CancellationToken cancellationToken)
    {
        NormalizeNit(model);
        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        try
        {
            var id = await supplierService.CreateSupplierAsync(Map(model), cancellationToken);
            if (IsAjaxRequest() && string.Equals(Request.Query["returnSupplier"], "true", StringComparison.OrdinalIgnoreCase))
            {
                var supplier = await supplierService.GetSupplierAsync(id, cancellationToken);
                return Json(new
                {
                    id,
                    text = supplier is null ? model.LegalName : FormatSupplierName(supplier),
                    nit = supplier?.Nit ?? model.Nit,
                    defaultTerms = (int)(supplier?.DefaultTerms ?? model.DefaultTerms),
                    defaultCreditDays = supplier?.DefaultCreditDays ?? model.DefaultCreditDays
                });
            }
            return RedirectToAction(nameof(Index));
        }
        catch (DomainRuleException ex)
        {
            return ReturnDuplicateValidationError(model, ex);
        }
    }

    [Authorize(Policy = PermissionCodes.SuppliersManage)]
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await supplierService.GetSupplierAsync(id, cancellationToken);
        if (supplier is null)
        {
            return NotFound();
        }

        return View("Form", new SupplierFormViewModel
        {
            Id = supplier.Id,
            LegalName = supplier.LegalName,
            Nit = supplier.Nit,
            TradeName = supplier.TradeName,
            Phone = supplier.Phone,
            Email = supplier.Email,
            Address = supplier.Address,
            ContactName = supplier.ContactName,
            ContactPosition = supplier.ContactPosition,
            ContactPhone = supplier.ContactPhone,
            DefaultTerms = supplier.DefaultTerms,
            DefaultCreditDays = supplier.DefaultCreditDays,
            Notes = supplier.Notes,
            IsActive = supplier.IsActive
        });
    }

    [Authorize(Policy = PermissionCodes.SuppliersManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(SupplierFormViewModel model, CancellationToken cancellationToken)
    {
        NormalizeNit(model);
        if (!ModelState.IsValid || model.Id is null)
        {
            return View("Form", model);
        }

        try
        {
            await supplierService.UpdateSupplierAsync(model.Id.Value, Map(model), cancellationToken);
            return RedirectToAction(nameof(Index));
        }
        catch (DomainRuleException ex)
        {
            return ReturnDuplicateValidationError(model, ex);
        }
    }

    private static SaveSupplierRequest Map(SupplierFormViewModel model) =>
        new(model.LegalName, Supplier.NormalizeNit(model.Nit), model.TradeName, model.Phone, model.Email, model.Address,
            model.ContactName, model.IsActive, model.DefaultCreditDays, model.DefaultTerms,
            model.ContactPosition, model.ContactPhone, model.Notes);

    private void NormalizeNit(SupplierFormViewModel model)
    {
        model.Nit = Supplier.NormalizeNit(model.Nit);
        ModelState.Remove(nameof(model.Nit));
        TryValidateModel(model);
    }

    private IActionResult ReturnDuplicateValidationError(SupplierFormViewModel model, DomainRuleException exception)
    {
        var isNitDuplicate = exception.Message.Contains("mismo NIT", StringComparison.OrdinalIgnoreCase);
        var field = isNitDuplicate ? nameof(model.Nit) : nameof(model.LegalName);
        var message = isNitDuplicate
            ? "Ya existe un proveedor registrado con este NIT."
            : "Ya existe un proveedor registrado con esta razón social.";

        ModelState.AddModelError(field, message);
        ViewData["SupplierFormSaveError"] = "No se pudo guardar el proveedor. Revisa los datos indicados.";
        return View("Form", model);
    }

    private static string FormatSupplierName(SupplierDto supplier) =>
        string.IsNullOrWhiteSpace(supplier.TradeName) || string.Equals(supplier.TradeName, supplier.LegalName, StringComparison.OrdinalIgnoreCase)
            ? supplier.LegalName
            : $"{supplier.TradeName} · {supplier.LegalName}";

    private bool IsAjaxRequest() =>
        string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
}
