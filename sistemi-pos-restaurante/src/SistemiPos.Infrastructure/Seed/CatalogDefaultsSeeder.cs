using Microsoft.EntityFrameworkCore;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Seed;

public sealed class CatalogDefaultsSeeder(AppDbContext dbContext)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // 1. Categoría y marca predeterminada
        var category = new Category("Sin categoría", "Productos sin una categoría específica.");
        var categoryNames = await dbContext.Categories.Select(x => x.Name).ToListAsync(cancellationToken);
        if (!categoryNames.Any(x => NameNormalizer.Equals(x, category.Name)))
            dbContext.Categories.Add(category);

        var brand = new Brand("Sin marca");
        var brandNames = await dbContext.Brands.Select(x => x.Name).ToListAsync(cancellationToken);
        if (!brandNames.Any(x => NameNormalizer.Equals(x, brand.Name)))
            dbContext.Brands.Add(brand);

        await dbContext.SaveChangesAsync(cancellationToken);

        // 2. Unidades de medida base estables (CAT-02)
        var defaultUnits = new List<UnitOfMeasure>
        {
            new("g", "Gramo", UnitDimension.Mass, "g", 2),
            new("kg", "Kilogramo", UnitDimension.Mass, "kg", 3),
            new("lb", "Libra", UnitDimension.Mass, "lb", 2),
            new("ml", "Mililitro", UnitDimension.Volume, "ml", 2),
            new("l", "Litro", UnitDimension.Volume, "L", 3),
            new("unidad", "Unidad", UnitDimension.Count, "un", 0),
            new("porcion", "Porción", UnitDimension.Count, "porc", 2)
        };

        var existingUnits = await dbContext.UnitsOfMeasure.ToListAsync(cancellationToken);
        foreach (var unit in defaultUnits)
        {
            if (!existingUnits.Any(x => x.Code == unit.Code))
            {
                dbContext.UnitsOfMeasure.Add(unit);
                existingUnits.Add(unit);
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        // 3. Conversiones universales estándar (CAT-03)
        var unitDict = existingUnits.ToDictionary(x => x.Code, x => x.Id);
        var globalConversions = new List<(string From, string To, decimal Factor)>
        {
            ("kg", "g", 1000m),
            ("l", "ml", 1000m),
            ("lb", "g", 453.592m)
        };

        var existingConversions = await dbContext.ProductUnitConversions
            .Where(x => x.ProductId == null)
            .ToListAsync(cancellationToken);

        foreach (var conv in globalConversions)
        {
            if (unitDict.TryGetValue(conv.From, out var fromId) && unitDict.TryGetValue(conv.To, out var toId))
            {
                if (!existingConversions.Any(x => x.FromUnitId == fromId && x.ToUnitId == toId))
                {
                    dbContext.ProductUnitConversions.Add(new ProductUnitConversion(null, fromId, toId, conv.Factor));
                }
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        // 4. Backfill seguro de productos existentes (CAT-01, CAT-02)
        var defaultCountUnitId = unitDict.TryGetValue("unidad", out var uId) ? uId : (Guid?)null;
        var productsWithoutUnit = await dbContext.Products
            .Where(x => x.BaseUnitId == null)
            .ToListAsync(cancellationToken);

        foreach (var prod in productsWithoutUnit)
        {
            var code = prod.UnitOfMeasure.Trim().ToLowerInvariant();
            if (unitDict.TryGetValue(code, out var matchedUnitId))
            {
                prod.SetBaseUnit(matchedUnitId);
            }
            else if (defaultCountUnitId.HasValue)
            {
                prod.SetBaseUnit(defaultCountUnitId.Value);
            }
        }

        // 5. Configuración por sucursal para sucursales activas existentes (CAT-05)
        var activeBranches = await dbContext.Branches.Where(x => x.IsActive).ToListAsync(cancellationToken);
        if (activeBranches.Count > 0)
        {
            var products = await dbContext.Products.Include(x => x.BranchSettings).ToListAsync(cancellationToken);
            foreach (var product in products)
            {
                foreach (var branch in activeBranches)
                {
                    if (!product.BranchSettings.Any(x => x.BranchId == branch.Id))
                    {
                        dbContext.ProductBranchSettings.Add(new ProductBranchSetting(product.Id, branch.Id, isEnabled: true, isVisible: true));
                    }
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
