using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Catalog;
using SistemiPos.Application.Common.Exceptions;
using SistemiPos.Application.Organizations;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Inventory;

namespace SistemiPos.Application.Inventory;

public sealed class InventoryTransferService(
    IInventoryTransferRepository transferRepository,
    IOrganizationRepository organizationRepository,
    ICatalogRepository catalogRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserAccessor currentUserAccessor,
    IDateTimeProvider dateTimeProvider) : IInventoryTransferService
{
    public async Task<IReadOnlyList<InventoryTransferDto>> GetTransfersAsync(
        Guid? originWarehouseId = null,
        Guid? destinationWarehouseId = null,
        InventoryTransferStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var transfers = await transferRepository.GetTransfersAsync(originWarehouseId, destinationWarehouseId, status, cancellationToken);
        return transfers.Select(Map).ToList();
    }

    public async Task<InventoryTransferDto?> GetTransferByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var transfer = await transferRepository.GetTransferByIdAsync(id, cancellationToken);
        return transfer is null ? null : Map(transfer);
    }

    public async Task<InventoryTransferDto> CreateTransferAsync(
        CreateInventoryTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.OriginWarehouseId == Guid.Empty || request.DestinationWarehouseId == Guid.Empty)
        {
            throw new DomainRuleException("Las bodegas de origen y destino son requeridas.");
        }

        if (request.OriginWarehouseId == request.DestinationWarehouseId)
        {
            throw new DomainRuleException("La bodega de origen y destino no pueden ser la misma.");
        }

        var originWarehouse = await organizationRepository.GetWarehouseByIdAsync(request.OriginWarehouseId, cancellationToken)
            ?? throw new NotFoundException("Bodega de origen no encontrada.");

        if (!originWarehouse.IsActive)
        {
            throw new DomainRuleException("La bodega de origen está inactiva.");
        }

        var destinationWarehouse = await organizationRepository.GetWarehouseByIdAsync(request.DestinationWarehouseId, cancellationToken)
            ?? throw new NotFoundException("Bodega de destino no encontrada.");

        if (!destinationWarehouse.IsActive)
        {
            throw new DomainRuleException("La bodega de destino está inactiva.");
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            throw new DomainRuleException("El traslado debe incluir al menos un producto.");
        }

        if (request.Items.GroupBy(x => x.ProductId).Any(g => g.Count() > 1))
        {
            throw new DomainRuleException("Un producto no puede repetirse en las líneas del traslado.");
        }

        var items = new List<InventoryTransferItem>();
        foreach (var reqItem in request.Items)
        {
            var product = await catalogRepository.GetProductAsync(reqItem.ProductId, cancellationToken)
                ?? throw new NotFoundException("Producto no encontrado.");

            if (!product.IsActive)
            {
                throw new DomainRuleException($"El producto {product.DisplayName} está inactivo.");
            }

            var originCost = product.LastCost > 0 ? product.LastCost : product.PreviousCost;
            var assignedCost = reqItem.AssignedUnitCost.HasValue && reqItem.AssignedUnitCost.Value >= 0
                ? reqItem.AssignedUnitCost.Value
                : originCost;

            items.Add(new InventoryTransferItem(
                reqItem.ProductId,
                reqItem.Quantity,
                originCost,
                assignedCost,
                reqItem.OriginLotId,
                reqItem.LotNumber,
                reqItem.ExpiryDate));
        }

        var number = await transferRepository.GenerateTransferNumberAsync(cancellationToken);
        var transfer = new InventoryTransfer(
            number,
            originWarehouse.Id,
            destinationWarehouse.Id,
            dateTimeProvider.UtcNow,
            currentUserAccessor.UserName,
            items,
            request.Observations);

        await transferRepository.AddTransferAsync(transfer, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await transferRepository.GetTransferByIdAsync(transfer.Id, cancellationToken);
        return Map(saved ?? transfer);
    }

    public async Task<InventoryTransferDto> DispatchTransferAsync(Guid transferId, CancellationToken cancellationToken = default)
    {
        var transfer = await transferRepository.GetTransferByIdAsync(transferId, cancellationToken)
            ?? throw new NotFoundException("Traslado no encontrado.");

        if (transfer.Status != InventoryTransferStatus.Draft)
        {
            throw new DomainRuleException("Solo un traslado en borrador puede ser despachado.");
        }

        var now = dateTimeProvider.UtcNow;
        var userName = currentUserAccessor.UserName;

        foreach (var item in transfer.Items)
        {
            var stock = await transferRepository.GetStockAsync(item.ProductId, transfer.OriginWarehouseId, cancellationToken);
            if (stock is null || stock.CurrentStock < item.QuantitySent)
            {
                var current = stock?.CurrentStock ?? 0m;
                var product = await catalogRepository.GetProductAsync(item.ProductId, cancellationToken);
                var productName = product?.DisplayName ?? "Producto";
                throw new DomainRuleException($"Existencia insuficiente en bodega de origen para '{productName}'. Existencia actual: {current:N2}, Requerido: {item.QuantitySent:N2}.");
            }

            var previous = stock.CurrentStock;
            stock.ReplaceForSystem(previous - item.QuantitySent);

            var movement = new InventoryMovement(
                item.ProductId,
                InventoryMovementType.TransferExit,
                item.QuantitySent,
                previous,
                stock.CurrentStock,
                now,
                userName,
                transfer.Number,
                $"Despacho de traslado {transfer.Number} hacia {transfer.DestinationWarehouse?.Name ?? "destino"}.",
                warehouseId: transfer.OriginWarehouseId,
                transferId: transfer.Id,
                transferItemId: item.Id,
                unitCost: item.UnitCostOrigin);

            if (item.OriginLotId.HasValue)
            {
                movement.SetLotDetails(item.ExpiryDate, item.LotNumber, item.OriginLotId);
            }

            await transferRepository.AddMovementAsync(movement, cancellationToken);
        }

        transfer.Dispatch(userName, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await transferRepository.GetTransferByIdAsync(transferId, cancellationToken);
        return Map(updated ?? transfer);
    }

    public async Task<InventoryTransferDto> ReceiveTransferAsync(
        Guid transferId,
        ReceiveInventoryTransferRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var transfer = await transferRepository.GetTransferByIdAsync(transferId, cancellationToken)
            ?? throw new NotFoundException("Traslado no encontrado.");

        if (transfer.Status != InventoryTransferStatus.Dispatched)
        {
            throw new DomainRuleException("Solo un traslado despachado puede ser recibido.");
        }

        var now = dateTimeProvider.UtcNow;
        var userName = currentUserAccessor.UserName;

        var receivedMap = request?.Lines?.ToDictionary(x => x.ItemId, x => x.QuantityReceived);

        foreach (var item in transfer.Items)
        {
            var qtyToReceive = receivedMap != null && receivedMap.TryGetValue(item.Id, out var customQty)
                ? customQty
                : item.QuantitySent;

            if (qtyToReceive < 0)
            {
                throw new DomainRuleException("La cantidad recibida no puede ser negativa.");
            }

            if (qtyToReceive > item.QuantitySent)
            {
                throw new DomainRuleException("La cantidad recibida no puede exceder la cantidad enviada.");
            }

            var destStock = await transferRepository.GetStockAsync(item.ProductId, transfer.DestinationWarehouseId, cancellationToken);
            var isNewStock = destStock is null;
            destStock ??= new InventoryStock(item.ProductId, transfer.DestinationWarehouseId);

            var previous = destStock.CurrentStock;
            destStock.ReplaceForSystem(previous + qtyToReceive);

            if (isNewStock)
            {
                await transferRepository.AddStockAsync(destStock, cancellationToken);
            }

            var movement = new InventoryMovement(
                item.ProductId,
                InventoryMovementType.TransferEntry,
                qtyToReceive,
                previous,
                destStock.CurrentStock,
                now,
                userName,
                transfer.Number,
                $"Recepción de traslado {transfer.Number} desde {transfer.OriginWarehouse?.Name ?? "origen"}.",
                warehouseId: transfer.DestinationWarehouseId,
                transferId: transfer.Id,
                transferItemId: item.Id,
                unitCost: item.AssignedUnitCost);

            movement.SetLotDetails(item.ExpiryDate, item.LotNumber, item.OriginLotId);
            await transferRepository.AddMovementAsync(movement, cancellationToken);
        }

        transfer.Receive(userName, now, receivedMap);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await transferRepository.GetTransferByIdAsync(transferId, cancellationToken);
        return Map(updated ?? transfer);
    }

    public async Task<InventoryTransferDto> CancelTransferAsync(
        Guid transferId,
        CancelInventoryTransferRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var transfer = await transferRepository.GetTransferByIdAsync(transferId, cancellationToken)
            ?? throw new NotFoundException("Traslado no encontrado.");

        if (transfer.Status != InventoryTransferStatus.Draft)
        {
            throw new DomainRuleException("Solo un traslado en borrador puede ser cancelado.");
        }

        transfer.Cancel(currentUserAccessor.UserName, dateTimeProvider.UtcNow, request?.Reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await transferRepository.GetTransferByIdAsync(transferId, cancellationToken);
        return Map(updated ?? transfer);
    }

    private static InventoryTransferDto Map(InventoryTransfer transfer) =>
        new(
            transfer.Id,
            transfer.Number,
            transfer.OriginWarehouseId,
            FormatWarehouseLabel(transfer.OriginWarehouse),
            transfer.DestinationWarehouseId,
            FormatWarehouseLabel(transfer.DestinationWarehouse),
            transfer.Status,
            transfer.RequestedAtUtc,
            transfer.RequestedBy,
            transfer.DispatchedAtUtc,
            transfer.DispatchedBy,
            transfer.ReceivedAtUtc,
            transfer.ReceivedBy,
            transfer.CancelledAtUtc,
            transfer.CancelledBy,
            transfer.CancellationReason,
            transfer.Observations,
            transfer.Items.Select(i => new InventoryTransferItemDto(
                i.Id,
                i.ProductId,
                i.Product?.DisplayName ?? string.Empty,
                i.Product?.InternalCode ?? string.Empty,
                i.QuantitySent,
                i.QuantityReceived,
                i.UnitCostOrigin,
                i.AssignedUnitCost,
                i.OriginLotId,
                i.LotNumber,
                i.ExpiryDate)).ToList());

    private static string FormatWarehouseLabel(Domain.Organizations.Warehouse? warehouse)
    {
        if (warehouse is null) return string.Empty;
        return warehouse.Branch is not null
            ? $"{warehouse.Branch.Name} - {warehouse.Name}"
            : warehouse.Name;
    }
}
