using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemiPos.Application.Catalog;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Web.Models.Catalog;

namespace SistemiPos.Web.Controllers;

[Authorize(Policy = PermissionCodes.CategoriesView)]
public sealed class CategoriesController(ICatalogService catalogService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var categories = await catalogService.GetCategoriesAsync(cancellationToken);
        return View(categories);
    }

    [Authorize(Policy = PermissionCodes.CategoriesManage)]
    [HttpGet]
    public IActionResult Create(string? returnUrl = null) => View("Form", new CategoryFormViewModel
    {
        ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null
    });

    [Authorize(Policy = PermissionCodes.CategoriesManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        try
        {
            await catalogService.CreateCategoryAsync(
                new SaveCategoryRequest(model.Name, model.Description, model.IsActive),
                cancellationToken);
            return Url.IsLocalUrl(model.ReturnUrl)
                ? LocalRedirect(model.ReturnUrl)
                : RedirectToAction(nameof(Index));
        }
        catch (DomainRuleException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("Form", model);
        }
    }

    [Authorize(Policy = PermissionCodes.CategoriesManage)]
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var category = await catalogService.GetCategoryAsync(id, cancellationToken);
        if (category is null)
        {
            return NotFound();
        }

        return View("Form", new CategoryFormViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            IsActive = category.IsActive
        });
    }

    [Authorize(Policy = PermissionCodes.CategoriesManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CategoryFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || model.Id is null)
        {
            return View("Form", model);
        }

        try
        {
            await catalogService.UpdateCategoryAsync(
                model.Id.Value,
                new SaveCategoryRequest(model.Name, model.Description, model.IsActive),
                cancellationToken);
            return RedirectToAction(nameof(Index));
        }
        catch (DomainRuleException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("Form", model);
        }
    }
}
