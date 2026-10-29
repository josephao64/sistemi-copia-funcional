using Microsoft.EntityFrameworkCore;
using SistemiPos.Domain.Recipes;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.IntegrationTests.Infrastructure;

public sealed class RecipeModelTests
{
    [Fact]
    public void Recipes_Protect_Product_Uniqueness_References_Precision_And_Concurrency()
    {
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=ModelOnly;Username=postgres;Password=not-used").Options);
        var recipe = context.Model.FindEntityType(typeof(Recipe))!;
        var ingredient = context.Model.FindEntityType(typeof(RecipeIngredient))!;
        Assert.Contains(recipe.GetIndexes(), x => x.IsUnique && x.Properties.Single().Name == "ProductId");
        Assert.Contains(ingredient.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(["RecipeId", "ProductId"]));
        Assert.True(recipe.FindProperty("Version")!.IsConcurrencyToken);
        Assert.Equal("xmin", recipe.FindProperty("Version")!.GetColumnName());
        Assert.Equal(6, ingredient.FindProperty("BaseQuantity")!.GetScale());
        Assert.All(ingredient.GetForeignKeys().Where(x => x.Properties.Any(p => p.Name is "SubrecipeId" or "ProductId" or "UnitId")),
            x => Assert.Equal(DeleteBehavior.Restrict, x.DeleteBehavior));
        Assert.False(context.Database.HasPendingModelChanges());
    }
}
