using System.Data;
using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Inventory;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Inventory;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Inventory;

public sealed class InventoryLotService(AppDbContext db, IOperationContextAccessor operationContext,
    ICurrentUserAccessor currentUser, IDateTimeProvider clock) : IInventoryLotService
{
    private async Task<InventoryStock> GetStockAsync(Guid productId, Guid warehouseId, CancellationToken ct)
    {
        var branches = (await operationContext.GetAuthorizedBranchesAsync(ct)).Select(x => x.Id).ToList();
        return await db.InventoryStocks.Include(x => x.Product).ThenInclude(x => x.BaseUnit).Include(x => x.Warehouse)
            .SingleOrDefaultAsync(x => x.ProductId == productId && x.WarehouseId == warehouseId
                && x.Warehouse != null && branches.Contains(x.Warehouse.BranchId), ct)
            ?? throw new DomainRuleException("No existe inventario autorizado para este producto y bodega.");
    }

    public async Task<ProductLotsDto> GetAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default)
    {
        var stock = await GetStockAsync(productId, warehouseId, cancellationToken);
        var lots = await db.InventoryExpiryLots.Where(x => x.ProductId == productId && x.WarehouseId == warehouseId)
            .OrderBy(x => x.ReceivedAtUtc).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var movements = await db.InventoryMovementLots.Where(x => x.Movement.ProductId == productId
                && x.Movement.WarehouseId == warehouseId)
            .OrderByDescending(x => x.Movement.OccurredAtUtc)
            .Select(x => new InventoryLotTraceRow(x.Movement.OccurredAtUtc, x.Movement.Type == InventoryMovementType.PurchaseEntry ? "Compra" :
                x.Movement.Type == InventoryMovementType.SaleExit ? "Venta" :
                x.Movement.Type == InventoryMovementType.ReturnEntry ? "Devolución" :
                x.Movement.Type == InventoryMovementType.CancellationReversal ? "Anulación" :
                x.Movement.Type == InventoryMovementType.ProductionConsumption ? "Consumo de producción" :
                x.Movement.Type == InventoryMovementType.ProductionEntry ? "Entrada de producción" :
                x.Movement.Type == InventoryMovementType.PositiveAdjustment ? "Ajuste positivo" : "Ajuste negativo",
                x.Movement.SourceDocument, x.Lot.LotNumber, x.Quantity, x.Movement.UserName))
            .Take(200).ToListAsync(cancellationToken);
        return new ProductLotsDto(productId, warehouseId, stock.Product.DisplayName,
            stock.Product.BaseUnit?.Symbol ?? stock.Product.UnitOfMeasure, stock.Warehouse!.Name,
            stock.Product.ManagesExpiry, stock.CurrentStock, stock.Product.ManagesLots ? stock.CurrentStock - lots.Sum(x => x.AvailableQuantity) : 0m,
            lots.Select(x => new InventoryLotRow(x.Id, x.LotNumber, x.ReceivedAtUtc, x.ExpiryDate,
                x.ReceivedQuantity, x.AvailableQuantity, x.IsOpeningBalance)).ToList(), movements, stock.Product.ManagesLots);
    }

    public async Task AddOpeningAsync(Guid productId, Guid warehouseId, decimal quantity, DateOnly? expiryDate,
        string? lotNumber, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var stock = await GetStockAsync(productId, warehouseId, cancellationToken);
        if (!stock.Product.ManagesLots)
            throw new DomainRuleException("El producto no tiene habilitado el control por lote.");
        var assigned = await db.InventoryExpiryLots.Where(x => x.ProductId == productId && x.WarehouseId == warehouseId)
            .SumAsync(x => x.AvailableQuantity, cancellationToken);
        if (quantity <= 0 || quantity != decimal.Round(quantity, 2) || quantity > stock.CurrentStock - assigned)
            throw new DomainRuleException("La cantidad debe corresponder al saldo inicial pendiente de asignar.");
        if (stock.Product.ManagesExpiry && !expiryDate.HasValue)
            throw new DomainRuleException("Indica la fecha de vencimiento real del saldo inicial.");
        db.InventoryExpiryLots.Add(new InventoryExpiryLot(productId, stock.Product.ManagesExpiry ? expiryDate : null,
            quantity, "SALDO INICIAL", clock.UtcNow, warehouseId, true, lotNumber));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DiscardAsync(Guid productId, Guid warehouseId, Guid lotId, decimal quantity, string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainRuleException("Indica el motivo de la baja.");
        var stock = await GetStockAsync(productId, warehouseId, cancellationToken);
        if (!stock.Product.ManagesLots)
            throw new DomainRuleException("El producto no tiene habilitado el control por lote.");
        var lot = await db.InventoryExpiryLots.SingleOrDefaultAsync(x => x.Id == lotId && x.ProductId == productId
            && x.WarehouseId == warehouseId, cancellationToken)
            ?? throw new DomainRuleException("El lote no pertenece a esta bodega y producto.");
        if (quantity <= 0 || quantity != decimal.Round(quantity, 2) || quantity > lot.AvailableQuantity)
            throw new DomainRuleException("La cantidad de baja no es válida.");
        var previous = stock.CurrentStock;
        stock.ReplaceForSystem(previous - quantity);
        var movement = new InventoryMovement(productId, InventoryMovementType.NegativeAdjustment, quantity,
            previous, stock.CurrentStock, clock.UtcNow, currentUser.UserName, lot.LotNumber, reason, warehouseId: warehouseId);
        movement.SetLotDetails(null, selectedLotId: lot.Id);
        db.InventoryMovements.Add(movement);
        await db.SaveChangesAsync(cancellationToken);
    }
}
