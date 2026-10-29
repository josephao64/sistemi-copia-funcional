using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Inventory;
using SistemiPos.Domain.Inventory;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Inventory;

public sealed class InventoryRepository(AppDbContext dbContext) : IInventoryRepository
{
    public async Task<IReadOnlyDictionary<Guid, string>> GetWarehouseNamesAsync(IReadOnlyCollection<Guid> warehouseIds, CancellationToken cancellationToken = default) =>
        await dbContext.Warehouses.AsNoTracking().Where(x => warehouseIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

    public Task<InventoryStock?> GetStockAsync(Guid productId, CancellationToken cancellationToken = default) =>
        dbContext.InventoryStocks.FirstOrDefaultAsync(x => x.ProductId == productId, cancellationToken);

    public Task<InventoryStock?> GetStockAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default) =>
        dbContext.InventoryStocks.FirstOrDefaultAsync(x => x.ProductId == productId && x.WarehouseId == warehouseId, cancellationToken);

    public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default) =>
        dbContext.InventoryMovements.AddAsync(movement, cancellationToken).AsTask();

    public Task AddExpiryLotAsync(InventoryExpiryLot lot, CancellationToken cancellationToken = default) =>
        dbContext.InventoryExpiryLots.AddAsync(lot, cancellationToken).AsTask();

    public Task<List<InventoryExpiryLot>> GetExpiryLotsAsync(Guid? productId, CancellationToken cancellationToken = default) =>
        GetExpiryLotsAsync(productId, null, cancellationToken);

    public Task<List<InventoryExpiryLot>> GetExpiryLotsAsync(Guid? productId, Guid? warehouseId = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.InventoryExpiryLots.Include(x => x.Product).ThenInclude(x => x.Category).AsQueryable();
        if (productId.HasValue) query = query.Where(x => x.ProductId == productId.Value);
        if (warehouseId.HasValue) query = query.Where(x => x.WarehouseId == warehouseId.Value);
        return query.ToListAsync(cancellationToken);
    }

    public async Task<InventoryMovementPage> GetMovementPageAsync(
        Guid? productId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        await GetMovementPageAsync(productId, page, pageSize, null, cancellationToken);

    public async Task<InventoryMovementPage> GetMovementPageAsync(
        Guid? productId,
        int page,
        int pageSize,
        Guid? warehouseId = null,
        CancellationToken cancellationToken = default)
    {
        var query = BuildMovementQuery(productId, warehouseId);
        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var currentPage = Math.Min(page, totalPages);

        var pageQuery = query
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize);

        var items = await ProjectMovements(pageQuery)
            .ToListAsync(cancellationToken);

        return new InventoryMovementPage(items, totalCount, currentPage, pageSize);
    }

    public Task<List<InventoryMovementDto>> GetMovementsForExportAsync(
        Guid? productId,
        CancellationToken cancellationToken = default) =>
        GetMovementsForExportAsync(productId, null, cancellationToken);

    public Task<List<InventoryMovementDto>> GetMovementsForExportAsync(
        Guid? productId,
        Guid? warehouseId = null,
        CancellationToken cancellationToken = default) =>
        ProjectMovements(BuildMovementQuery(productId, warehouseId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.Id))
            .ToListAsync(cancellationToken);

    private IQueryable<InventoryMovement> BuildMovementQuery(Guid? productId, Guid? warehouseId = null)
    {
        var query = dbContext.InventoryMovements
            .Include(x => x.Warehouse)
            .AsNoTracking();

        if (productId.HasValue)
        {
            query = query.Where(x => x.ProductId == productId.Value);
        }

        if (warehouseId.HasValue)
        {
            query = query.Where(x => x.WarehouseId == warehouseId.Value);
        }

        return query;
    }

    private static IQueryable<InventoryMovementDto> ProjectMovements(IQueryable<InventoryMovement> query) =>
        query.Select(x => new InventoryMovementDto(
            x.Id,
            x.ProductId,
            x.Product.VariantValue == null ? x.Product.Name : x.Product.Name + " " + x.Product.VariantValue,
            x.Type,
            x.Quantity,
            x.PreviousStock,
            x.NewStock,
            x.OccurredAtUtc,
            x.UserName,
            x.SourceDocument,
            x.Note,
            x.WarehouseId,
            x.Warehouse != null ? x.Warehouse.Name : null,
            x.UnitCost));

    public Task<List<InventoryStockDto>> GetStocksAsync(
        bool onlyLowStock,
        CancellationToken cancellationToken = default) =>
        GetStocksAsync(onlyLowStock, null, null, cancellationToken);

    public Task<List<InventoryStockDto>> GetStocksAsync(
        bool onlyLowStock,
        Guid? branchId,
        Guid? warehouseId,
        CancellationToken cancellationToken = default)
    {
        IQueryable<SistemiPos.Domain.Inventory.InventoryStock> query = dbContext.InventoryStocks
            .Include(x => x.Product)
                .ThenInclude(x => x.Category)
            .Include(x => x.Warehouse)
                .ThenInclude(w => w!.Branch)
            .AsNoTracking()
            .Where(x => !x.Product.ManagesVariants);

        if (warehouseId.HasValue)
        {
            query = query.Where(x => x.WarehouseId == warehouseId.Value);
        }
        else if (branchId.HasValue)
        {
            query = query.Where(x => x.Warehouse != null && x.Warehouse.BranchId == branchId.Value);
        }

        var effectiveStocks = query.Select(x => new
        {
            Stock = x,
            MinimumStock = x.Warehouse == null ? x.Product.MinimumStock :
                x.Product.BranchSettings.Where(b => b.BranchId == x.Warehouse.BranchId)
                    .Select(b => b.MinimumStock).FirstOrDefault() ?? x.Product.MinimumStock
        });
        var projection = effectiveStocks.Select(value => new InventoryStockDto(
            value.Stock.ProductId,
            value.Stock.Product.InternalCode,
            value.Stock.Product.VariantValue == null ? value.Stock.Product.Name : value.Stock.Product.Name + " " + value.Stock.Product.VariantValue,
            value.Stock.Product.Category.Name,
            value.Stock.CurrentStock,
            value.MinimumStock,
            value.Stock.CurrentStock > 0 && value.Stock.CurrentStock <= value.MinimumStock,
            value.Stock.WarehouseId,
            value.Stock.Warehouse != null ? value.Stock.Warehouse.Name : null,
            value.Stock.Warehouse != null ? value.Stock.Warehouse.BranchId : null,
            value.Stock.Warehouse != null && value.Stock.Warehouse.Branch != null ? value.Stock.Warehouse.Branch.Name : null,
            value.Stock.Product.ManagesLots));

        if (onlyLowStock)
        {
            projection = projection.Where(x => x.IsLowStock);
        }

        return projection.ToListAsync(cancellationToken);
    }
}
