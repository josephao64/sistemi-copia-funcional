using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemiPos.Application.Recipes;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Web.Models.Recipes;
namespace SistemiPos.Web.Controllers;
[Authorize(Policy = PermissionCodes.RecipesView)]
[Route("recetas/{id:guid}/historial")]
public sealed class RecipeHistoryController(IRecipeOperationsService operations) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(Guid id, Guid? versionId, Guid? warehouseId, decimal? quantity, DateTime? costDate, CancellationToken ct)
    {
        var versions = await operations.VersionsAsync(id, ct);
        if (versions.Count == 0) return NotFound();
        var version = versions.FirstOrDefault(x => x.Id == versionId) ?? versions[0];
        var warehouses = await operations.WarehousesAsync(ct);
        warehouseId ??= warehouses.FirstOrDefault()?.Id;
        var amount = quantity ?? version.Read().YieldQuantity;
        RecipeQuote? quote = null; string? error = null;
        if (warehouseId.HasValue && ModelState.IsValid) try
        {
            DateTimeOffset? at = costDate.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(costDate.Value, DateTimeKind.Unspecified), TimeSpan.FromHours(-6)).ToUniversalTime() : null;
            quote = await operations.QuoteAsync(version.Id, warehouseId.Value, amount, at, ct);
        }
        catch (DomainRuleException ex) { error = ex.Message; }
        return View(new RecipeHistoryViewModel(id, versions, warehouses, version.Id, warehouseId, amount, costDate, quote, error));
    }
}
