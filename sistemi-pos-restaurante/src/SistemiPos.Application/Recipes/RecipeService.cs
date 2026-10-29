using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Catalog;
using SistemiPos.Application.Common.Exceptions;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Recipes;

namespace SistemiPos.Application.Recipes;

public sealed class RecipeService(IRecipeRepository repository, IUnitOfWork unitOfWork,
    IUnitConversionService conversions, IOperationAuthorizationService authorization,
    ICurrentUserAccessor currentUser) : IRecipeService
{
    private async Task AuthorizeAsync(string permission, CancellationToken ct)
    {
        if (!currentUser.UserId.HasValue || !await authorization.HasPermissionAsync(currentUser.UserName, permission, ct))
            throw new DomainRuleException("No tienes permiso para realizar esta operación de recetas.");
    }

    public async Task<IReadOnlyList<RecipeDto>> GetRecipesAsync(CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(PermissionCodes.RecipesView, cancellationToken);
        return (await repository.GetRecipesAsync(cancellationToken)).Select(Map).OrderBy(x => x.ProductName).ToList();
    }

    public async Task<RecipeDto?> GetRecipeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(PermissionCodes.RecipesView, cancellationToken);
        var recipe = await repository.GetRecipeAsync(id, cancellationToken);
        return recipe is null ? null : Map(recipe);
    }

    public async Task<RecipeOptions> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(PermissionCodes.RecipesView, cancellationToken);
        var recipes = await repository.GetRecipesAsync(cancellationToken);
        var products = (await repository.GetProductsAsync(cancellationToken))
            .Where(x => x.IsActive && !x.ManagesVariants && x.BaseUnit is { IsActive: true }).ToList();
        RecipeProductOption Option(Product x) => new(x.Id, x.InternalCode, x.DisplayName, x.ArticleType,
            x.BaseUnitId!.Value, x.BaseUnit!.Code, recipes.FirstOrDefault(r => r.ProductId == x.Id && r.IsActive)?.Id);
        return new RecipeOptions(products.Where(x => RecipeRules.CanHaveRecipe(x.ArticleType)).Select(Option).ToList(),
            products.Where(x => x.ArticleType != ArticleType.Subreceta
                || (x.ArticleType == ArticleType.Subreceta && recipes.Any(r => r.ProductId == x.Id && r.IsActive)))
                .Select(Option).ToList(),
            (await repository.GetUnitsAsync(cancellationToken)).Where(x => x.IsActive)
                .Select(x => new RecipeUnitOption(x.Id, x.Name, x.Code, x.Precision)).ToList());
    }

    public async Task<Guid> CreateRecipeAsync(SaveRecipeRequest request, CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(PermissionCodes.RecipesManage, cancellationToken);
        return await repository.InTransactionAsync(async () =>
        {
            var recipes = await repository.GetRecipesAsync(cancellationToken);
            if (recipes.Any(x => x.ProductId == request.ProductId))
                throw new DomainRuleException("Este producto ya tiene una receta. Edita la receta existente.");
            var (product, ingredients) = await ValidateAsync(request, recipes, null, cancellationToken);
            var recipe = new Recipe(product.Id, product.BaseUnitId!.Value, product.BaseUnit!.Code,
                request.YieldQuantity, ingredients, request.Instructions, request.IsActive);
            recipe.SetConsumptionMode(ResolveMode(request, product));
            await repository.AddRecipeAsync(recipe, cancellationToken);
            await repository.CaptureVersionAsync(recipe, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return recipe.Id;
        }, cancellationToken);
    }

    public async Task UpdateRecipeAsync(Guid id, SaveRecipeRequest request, CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(PermissionCodes.RecipesManage, cancellationToken);
        await repository.InTransactionAsync(async () =>
        {
            var recipe = await repository.GetRecipeAsync(id, cancellationToken)
                ?? throw new NotFoundException("Receta no encontrada.");
            if (request.Version != recipe.Version)
                throw new DomainRuleException("La receta cambió desde que la abriste. Recarga los datos antes de guardar.");
            if (recipe.ProductId != request.ProductId)
                throw new DomainRuleException("No se puede cambiar el producto de una receta existente.");
            var recipes = await repository.GetRecipesAsync(cancellationToken);
            if (!request.IsActive && recipes.Any(x => x.Id != id && x.IsActive && x.Ingredients.Any(i => i.SubrecipeId == id)))
                throw new DomainRuleException("Esta subreceta se utiliza en una receta activa. Retira la referencia antes de desactivarla.");
            var (product, ingredients) = await ValidateAsync(request, recipes, id, cancellationToken);
            var mode = ResolveMode(request, product);
            if (mode == RecipeConsumptionMode.OnSale && recipes.Any(x => x.Id != id && x.Ingredients.Any(i => i.SubrecipeId == id)))
                throw new DomainRuleException("Un producto utilizado como componente debe consumirse desde existencias producidas.");
            recipe.Update(request.YieldQuantity, ingredients, request.Instructions, request.IsActive);
            recipe.SetConsumptionMode(mode);
            await repository.UpdateRecipeAsync(recipe, cancellationToken);
            await repository.CaptureVersionAsync(recipe, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    private async Task<(Product Product, List<RecipeIngredient> Ingredients)> ValidateAsync(
        SaveRecipeRequest request, List<Recipe> recipes, Guid? recipeId, CancellationToken ct)
    {
        var products = (await repository.GetProductsAsync(ct)).ToDictionary(x => x.Id);
        if (!products.TryGetValue(request.ProductId, out var output)) throw new NotFoundException("Producto elaborado no encontrado.");
        if (!output.IsActive || output.ManagesVariants || !RecipeRules.CanHaveRecipe(output.ArticleType))
            throw new DomainRuleException("Selecciona un producto activo de tipo Preparado o Subreceta, o una variante concreta.");
        if (output.BaseUnit is not { IsActive: true })
            throw new DomainRuleException("El producto elaborado debe tener una unidad base activa.");
        RecipeRules.ValidateQuantity(request.YieldQuantity, "El rendimiento");
        if (decimal.Round(request.YieldQuantity, output.BaseUnit.Precision) != request.YieldQuantity)
            throw new DomainRuleException($"El rendimiento admite hasta {output.BaseUnit.Precision} decimales en {output.BaseUnit.Code}.");
        if (request.Ingredients.Count == 0 || request.Ingredients.GroupBy(x => x.ProductId).Any(x => x.Count() > 1))
            throw new DomainRuleException("Agrega ingredientes o subrecetas sin repetir productos.");
        var units = (await repository.GetUnitsAsync(ct)).ToDictionary(x => x.Id);
        var ingredients = new List<RecipeIngredient>();
        foreach (var input in request.Ingredients)
        {
            if (!products.TryGetValue(input.ProductId, out var product)) throw new NotFoundException("Ingrediente no encontrado.");
            if (product.Id == output.Id) throw new DomainRuleException("El producto elaborado no puede ser ingrediente de su propia receta.");
            if (!product.IsActive || product.ManagesVariants || product.BaseUnit is not { IsActive: true })
                throw new DomainRuleException($"El ingrediente {product.DisplayName} debe estar activo y tener unidad base activa.");
            var subrecipe = recipes.SingleOrDefault(x => x.ProductId == product.Id);
            if (product.ArticleType == ArticleType.Subreceta || subrecipe is not null)
            {
                if (subrecipe is not { IsActive: true })
                    throw new DomainRuleException($"Define y activa la receta de {product.DisplayName} antes de utilizarla como subreceta.");
                if (subrecipe.ConsumptionMode != RecipeConsumptionMode.FromProduction)
                    throw new DomainRuleException($"{product.DisplayName} debe producirse antes de utilizarse como componente.");
            }
            if (!units.TryGetValue(input.UnitId, out var unit) || !unit.IsActive)
                throw new DomainRuleException("Selecciona una unidad de medida activa para cada ingrediente.");
            RecipeRules.ValidateQuantity(input.Quantity, "La cantidad del ingrediente");
            if (decimal.Round(input.Quantity, unit.Precision) != input.Quantity)
                throw new DomainRuleException($"La cantidad de {product.DisplayName} admite hasta {unit.Precision} decimales en {unit.Code}.");
            var baseQuantity = await conversions.ConvertAsync(input.Quantity, unit.Id, product.BaseUnitId!.Value, product.Id, ct);
            ingredients.Add(new RecipeIngredient(product.Id, unit.Id, unit.Code, input.Quantity, baseQuantity,
                product.BaseUnit.Code, subrecipe?.Id));
        }
        if (recipeId.HasValue)
            RecipeRules.EnsureAcyclic(recipeId.Value, ingredients.Where(x => x.SubrecipeId.HasValue).Select(x => x.SubrecipeId!.Value),
                recipes.ToDictionary(x => x.Id, x => (IReadOnlyList<Guid>)x.Ingredients.Where(i => i.SubrecipeId.HasValue).Select(i => i.SubrecipeId!.Value).ToList()));
        return (output, ingredients);
    }

    private static RecipeDto Map(Recipe x) => new(x.Id, x.ProductId, x.Product.InternalCode,
        x.Product.DisplayName, x.Product.ArticleType, x.YieldQuantity, x.YieldUnitCode, x.IsActive,
        x.Instructions, x.Version, x.Ingredients.Select(i => new RecipeIngredientDto(i.ProductId,
            i.Product.InternalCode, i.Product.DisplayName, i.UnitId, i.UnitCode, i.Quantity,
            i.BaseQuantity, i.BaseUnitCode, i.SubrecipeId)).ToList(), x.Revision, x.ConsumptionMode);

    private static RecipeConsumptionMode ResolveMode(SaveRecipeRequest request, Product product)
    {
        var mode = request.ConsumptionMode ?? (product.ArticleType == ArticleType.Subreceta
            ? RecipeConsumptionMode.FromProduction : RecipeConsumptionMode.OnSale);
        if (!Enum.IsDefined(mode) || (product.ArticleType == ArticleType.Subreceta && mode != RecipeConsumptionMode.FromProduction))
            throw new DomainRuleException("Las subrecetas se producen antes de consumir sus existencias.");
        return mode;
    }
}
