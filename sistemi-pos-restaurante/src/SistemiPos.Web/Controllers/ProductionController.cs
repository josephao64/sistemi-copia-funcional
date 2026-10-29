using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemiPos.Application.Recipes;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Recipes;
using SistemiPos.Web.Models.Recipes;
namespace SistemiPos.Web.Controllers;
[Authorize(Policy = PermissionCodes.ProductionView)]
[Route("produccion")]
public sealed class ProductionController(IRecipeOperationsService operations, IRecipeService recipes) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await operations.OrdersAsync(ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        try { ViewBag.Warehouses = await operations.WarehousesAsync(ct); return View(await operations.OrderAsync(id, ct)); }
        catch (DomainRuleException ex) { return BadRequest(ex.Message); }
    }
    [Authorize(Policy = PermissionCodes.ProductionManage), HttpGet("nueva")]
    public async Task<IActionResult> Create(CancellationToken ct) => View(await Populate(new(), ct));
    [Authorize(Policy = PermissionCodes.ProductionManage), HttpPost("nueva"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductionFormViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid) try
        {
            var id = await operations.CreateAsync(new(model.OperationId, model.RecipeId, model.OriginWarehouseId,
                model.DestinationWarehouseId, model.PlannedQuantity, model.LotNumber, model.ExpiryDate,
                new DateTimeOffset(DateTime.SpecifyKind(model.ScheduledAt, DateTimeKind.Unspecified), TimeSpan.FromHours(-6)).ToUniversalTime()), ct);
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (DomainRuleException ex) { ModelState.AddModelError("", ex.Message); }
        return View(await Populate(model, ct));
    }
    [Authorize(Policy = PermissionCodes.ProductionConfirm), HttpPost("{id:guid}/confirmar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(Guid id, uint version, decimal actualQuantity, string? reason, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["Error"] = "Revisa el rendimiento real."; return RedirectToAction(nameof(Details), new { id }); }
        try { await operations.ConfirmAsync(id, version, actualQuantity, reason, ct); TempData["Success"] = "Producción confirmada. Consumos y entrada registrados."; }
        catch (DomainRuleException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id });
    }
    private async Task<ProductionFormViewModel> Populate(ProductionFormViewModel model, CancellationToken ct)
    {
        model.Recipes = (await recipes.GetRecipesAsync(ct)).Where(x => x.IsActive && x.ConsumptionMode == RecipeConsumptionMode.FromProduction).ToList();
        model.Warehouses = await operations.WarehousesAsync(ct); return model;
    }
}
