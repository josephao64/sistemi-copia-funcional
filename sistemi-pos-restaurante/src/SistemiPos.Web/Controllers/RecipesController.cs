using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemiPos.Application.Common.Exceptions;
using SistemiPos.Application.Recipes;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Web.Models.Recipes;

namespace SistemiPos.Web.Controllers;

[Authorize(Policy = PermissionCodes.RecipesView)]
[Route("recetas")]
public sealed class RecipesController(IRecipeService recipes) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? query, bool? isActive, CancellationToken cancellationToken)
    {
        var items = await recipes.GetRecipesAsync(cancellationToken);
        var filtered = items.Where(x => (!isActive.HasValue || x.IsActive == isActive)
            && (string.IsNullOrWhiteSpace(query) || x.ProductName.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)
                || x.ProductCode.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))).ToList();
        return View(new RecipesIndexViewModel(filtered, query, isActive));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var recipe = await recipes.GetRecipeAsync(id, cancellationToken);
        return recipe is null ? NotFound() : View(recipe);
    }

    [Authorize(Policy = PermissionCodes.RecipesManage), HttpGet("nueva")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken) =>
        View("Form", await PopulateAsync(new RecipeFormViewModel(), cancellationToken));

    [Authorize(Policy = PermissionCodes.RecipesManage), HttpGet("{id:guid}/editar")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var recipe = await recipes.GetRecipeAsync(id, cancellationToken);
        if (recipe is null) return NotFound();
        return View("Form", await PopulateAsync(new RecipeFormViewModel
        {
            Id = recipe.Id, ProductId = recipe.ProductId, ProductName = $"{recipe.ProductCode} · {recipe.ProductName}",
            Version = recipe.Version, YieldQuantity = recipe.YieldQuantity, YieldUnitCode = recipe.YieldUnitCode,
            Instructions = recipe.Instructions, IsActive = recipe.IsActive, ConsumptionMode = recipe.ConsumptionMode,
            Ingredients = recipe.Ingredients.Select(x => new RecipeIngredientFormViewModel
            {
                ProductId = x.ProductId, ProductName = $"{x.ProductCode} · {x.ProductName}",
                UnitId = x.UnitId, Quantity = x.Quantity
            }).ToList()
        }, cancellationToken));
    }

    [Authorize(Policy = PermissionCodes.RecipesManage), HttpPost("nueva"), ValidateAntiForgeryToken]
    public Task<IActionResult> Create(RecipeFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = null;
        return SaveAsync(model, cancellationToken);
    }

    [Authorize(Policy = PermissionCodes.RecipesManage), HttpPost("{id:guid}/editar"), ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(Guid id, RecipeFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        return SaveAsync(model, cancellationToken);
    }

    private async Task<IActionResult> SaveAsync(RecipeFormViewModel model, CancellationToken ct)
    {
        if (model.ProductId == Guid.Empty)
            ModelState.AddModelError(nameof(model.ProductId), "Selecciona el producto elaborado desde las sugerencias.");
        if (model.Ingredients.Count == 0)
            ModelState.AddModelError(string.Empty, "Agrega al menos un ingrediente o subreceta.");
        for (var i = 0; i < model.Ingredients.Count; i++)
        {
            if (model.Ingredients[i].ProductId == Guid.Empty)
                ModelState.AddModelError($"Ingredients[{i}].ProductId", "Selecciona un ingrediente desde las sugerencias.");
            if (model.Ingredients[i].UnitId == Guid.Empty)
                ModelState.AddModelError($"Ingredients[{i}].UnitId", "Selecciona la unidad del ingrediente.");
        }
        if (ModelState.IsValid)
        {
            try
            {
                var request = new SaveRecipeRequest(model.ProductId, model.YieldQuantity,
                    model.Ingredients.Select(x => new RecipeIngredientRequest(x.ProductId, x.Quantity, x.UnitId)).ToList(),
                    model.Instructions, model.IsActive, model.Version, model.ConsumptionMode);
                if (model.Id.HasValue) await recipes.UpdateRecipeAsync(model.Id.Value, request, ct);
                else model.Id = await recipes.CreateRecipeAsync(request, ct);
                TempData["Success"] = "La receta fue guardada correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (ex is DomainRuleException or NotFoundException)
            { ModelState.AddModelError(string.Empty, ex.Message); }
        }
        return View("Form", await PopulateAsync(model, ct));
    }

    private async Task<RecipeFormViewModel> PopulateAsync(RecipeFormViewModel model, CancellationToken ct)
    {
        model.Options = await recipes.GetOptionsAsync(ct);
        var product = model.Options.Outputs.FirstOrDefault(x => x.Id == model.ProductId);
        if (product is not null)
        {
            model.ProductName = $"{product.Code} · {product.Name}";
            model.YieldUnitCode = product.BaseUnitCode;
        }
        foreach (var item in model.Ingredients)
        {
            var ingredient = model.Options.Ingredients.FirstOrDefault(x => x.Id == item.ProductId);
            if (ingredient is not null) item.ProductName = $"{ingredient.Code} · {ingredient.Name}";
        }
        return model;
    }
}
