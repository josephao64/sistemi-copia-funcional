using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Inventory;
using SistemiPos.Domain.Inventory;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Inventory;

public sealed class InventoryTransferRepository(AppDbContext dbContext) : IInventoryTransferRepository
{
    public Task<List<InventoryTransfer>> GetTransfersAsync(
        Guid? originWarehouseId = null,
        Guid? destinationWarehouseId = null,
        InventoryTransferStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.InventoryTransfers
            .Include(x => x.OriginWarehouse)
                .ThenInclude(w => w.Branch)
            .Include(x => x.DestinationWarehouse)
                .ThenInclude(w => w.Branch)
            .Include(x => x.Items)
                .ThenInclude(x => x.Product)
            .AsNoTracking();

        if (originWarehouseId.HasValue)
        {
            query = query.Where(x => x.OriginWarehouseId == originWarehouseId.Value);
        }

        if (destinationWarehouseId.HasValue)
        {
            query = query.Where(x => x.DestinationWarehouseId == destinationWarehouseId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        return query
            .OrderByDescending(x => x.RequestedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<InventoryTransfer?> GetTransferByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.InventoryTransfers
            .Include(x => x.OriginWarehouse)
                .ThenInclude(w => w.Branch)
            .Include(x => x.DestinationWarehouse)
                .ThenInclude(w => w.Branch)
            .Include(x => x.Items)
                .ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<InventoryTransfer?> GetTransferByNumberAsync(string number, CancellationToken cancellationToken = default) =>
        dbContext.InventoryTransfers
            .Include(x => x.OriginWarehouse)
                .ThenInclude(w => w.Branch)
            .Include(x => x.DestinationWarehouse)
                .ThenInclude(w => w.Branch)
            .Include(x => x.Items)
                .ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x => x.Number == number, cancellationToken);

    public Task AddTransferAsync(InventoryTransfer transfer, CancellationToken cancellationToken = default) =>
        dbContext.InventoryTransfers.AddAsync(transfer, cancellationToken).AsTask();

    public async Task<string> GenerateTransferNumberAsync(CancellationToken cancellationToken = default)
    {
        var lastNumber = await dbContext.InventoryTransfers
            .Select(x => x.Number)
            .Where(x => x.StartsWith("TR-"))
            .OrderByDescending(x => x)
            .FirstOrDefaultAsync(cancellationToken);

        var nextSequence = 1;
        if (!string.IsNullOrWhiteSpace(lastNumber) && lastNumber.StartsWith("TR-", StringComparison.Ordinal))
        {
            var suffix = lastNumber["TR-".Length..];
            if (int.TryParse(suffix, out var parsed))
            {
                nextSequence = parsed + 1;
            }
        }

        return $"TR-{nextSequence:000000}";
    }

    public Task<InventoryStock?> GetStockAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default) =>
        dbContext.InventoryStocks.FirstOrDefaultAsync(x => x.ProductId == productId && x.WarehouseId == warehouseId, cancellationToken);

    public Task AddStockAsync(InventoryStock stock, CancellationToken cancellationToken = default) =>
        dbContext.InventoryStocks.AddAsync(stock, cancellationToken).AsTask();

    public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default) =>
        dbContext.InventoryMovements.AddAsync(movement, cancellationToken).AsTask();
}
