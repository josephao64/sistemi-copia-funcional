using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Common.Exceptions;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Inventory;

namespace SistemiPos.Application.Inventory;

public sealed class PhysicalInventoryCountService(
    IPhysicalInventoryCountRepository repository,
    IInventoryRepository inventoryRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserAccessor currentUserAccessor,
    IDateTimeProvider dateTimeProvider) : IPhysicalInventoryCountService
{
    public async Task<IReadOnlyList<PhysicalInventoryCategoryOption>> GetCategoryOptionsAsync(CancellationToken cancellationToken = default) =>
        await repository.GetCategoryOptionsAsync(cancellationToken);

    public async Task<IReadOnlyList<PhysicalInventoryCountListItemDto>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        var counts = await repository.GetCountsAsync(cancellationToken);
        return counts
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new PhysicalInventoryCountListItemDto(
                x.Id,
                x.Name,
                x.CreatedAtUtc,
                x.CreatedBy ?? "system",
                x.Scope,
                x.Categories.Select(category => category.Category.Name).OrderBy(name => name).ToList(),
                x.Items.Count,
                x.Items.Count(item => item.Status == PhysicalInventoryCountItemStatus.Counted),
                x.Items.Count(item => item.Status != PhysicalInventoryCountItemStatus.Counted),
                x.Status))
            .ToList();
    }

    public async Task<Guid> CreateAsync(CreatePhysicalInventoryCountRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.Scope))
        {
            throw new DomainRuleException("El alcance de la toma no es válido.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new DomainRuleException("El nombre de la toma es requerido.");
        }

        if (request.Name.Length > 200)
        {
            throw new DomainRuleException("El nombre de la toma no puede exceder 200 caracteres.");
        }

        var categoryIds = request.CategoryIds.Where(x => x != Guid.Empty).Distinct().ToList();
        if (request.Scope == PhysicalInventoryCountScope.Categories && categoryIds.Count == 0)
        {
            throw new DomainRuleException("Seleccione al menos una categoría para la toma.");
        }

        if (request.Scope == PhysicalInventoryCountScope.Categories)
        {
            var validCategoryIds = (await repository.GetCategoryOptionsAsync(cancellationToken)).Select(x => x.Id).ToHashSet();
            if (categoryIds.Any(x => !validCategoryIds.Contains(x)))
            {
                throw new DomainRuleException("Una o más categorías seleccionadas no están disponibles.");
            }
        }

        var productIds = await repository.GetEligibleProductIdsAsync(request.Scope, categoryIds, cancellationToken);
        if (productIds.Count == 0)
        {
            throw new DomainRuleException("El alcance seleccionado no contiene productos activos.");
        }

        if (await repository.HasActiveParticipationAsync(productIds, cancellationToken))
        {
            throw new DomainRuleException("Uno o más productos ya participan en otra toma física en proceso.");
        }

        var count = new PhysicalInventoryCount(request.Name, request.Scope, request.WarehouseId);
        if (request.Scope == PhysicalInventoryCountScope.Categories)
        {
            foreach (var categoryId in categoryIds)
            {
                count.IncludeCategory(categoryId);
            }
        }

        foreach (var productId in productIds)
        {
            count.IncludeProduct(productId);
        }

        await repository.AddAsync(count, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return count.Id;
    }

    public async Task<PhysicalInventoryCountDetailDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var count = await repository.GetCountAsync(id, false, cancellationToken)
            ?? throw new NotFoundException("Toma física no encontrada.");
        var stocks = await inventoryRepository.GetStocksAsync(false, null, count.WarehouseId, cancellationToken);
        var stockByProduct = count.WarehouseId.HasValue
            ? stocks.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.First().CurrentStock)
            : stocks.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.CurrentStock));
        return MapDetail(count, stockByProduct);
    }

    public async Task<BeginPhysicalInventoryCountItemResult> BeginItemAsync(Guid countId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var item = await repository.GetItemAsync(countId, itemId, cancellationToken)
            ?? throw new NotFoundException("Producto de la toma no encontrado.");
        item.PhysicalInventoryCount.EnsureInProgress();
        var stock = await (item.PhysicalInventoryCount.WarehouseId.HasValue
            ? inventoryRepository.GetStockAsync(item.ProductId, item.PhysicalInventoryCount.WarehouseId.Value, cancellationToken)
            : inventoryRepository.GetStockAsync(item.ProductId, cancellationToken))
            ?? throw new NotFoundException("No se encontró la existencia del producto.");

        item.BeginCounting(stock.CurrentStock, stock.Version, dateTimeProvider.UtcNow, currentUserAccessor.UserName);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var count = await repository.GetCountAsync(countId, false, cancellationToken)
            ?? throw new NotFoundException("Toma física no encontrada.");
        return new BeginPhysicalInventoryCountItemResult(item.Id, stock.CurrentStock, item.Status, CreateSummary(count.Items));
    }

    public async Task<SavePhysicalInventoryCountItemResult> SaveItemAsync(
        Guid countId,
        Guid itemId,
        SavePhysicalInventoryCountItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Comment?.Length > 500)
        {
            throw new DomainRuleException("El comentario no puede exceder 500 caracteres.");
        }

        var item = await repository.GetItemAsync(countId, itemId, cancellationToken)
            ?? throw new NotFoundException("Producto de la toma no encontrado.");
        item.PhysicalInventoryCount.EnsureInProgress();
        var stock = await (item.PhysicalInventoryCount.WarehouseId.HasValue
            ? inventoryRepository.GetStockAsync(item.ProductId, item.PhysicalInventoryCount.WarehouseId.Value, cancellationToken)
            : inventoryRepository.GetStockAsync(item.ProductId, cancellationToken))
            ?? throw new NotFoundException("No se encontró la existencia del producto.");

        var saved = item.SaveCount(request.CountedStock, request.Comment, stock.Version, dateTimeProvider.UtcNow, currentUserAccessor.UserName);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var count = await repository.GetCountAsync(countId, false, cancellationToken)
            ?? throw new NotFoundException("Toma física no encontrada.");
        var currentStocks = await inventoryRepository.GetStocksAsync(false, null, count.WarehouseId, cancellationToken);
        var stockByProduct = count.WarehouseId.HasValue
            ? currentStocks.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.First().CurrentStock)
            : currentStocks.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.CurrentStock));
        var detail = MapDetail(count, stockByProduct);
        var mappedItem = detail.Items.Single(x => x.Id == itemId);
        var message = saved
            ? "Conteo guardado dentro de la toma física."
            : "Este producto tuvo movimientos mientras se realizaba el conteo. Realice nuevamente el conteo para evitar diferencias incorrectas.";
        return new SavePhysicalInventoryCountItemResult(saved, message, mappedItem, detail.Summary);
    }

    public async Task FinalizeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await repository.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            var count = await repository.GetCountAsync(id, true, transactionCancellationToken)
                ?? throw new NotFoundException("Toma física no encontrada.");
            count.EnsureInProgress();

            if (count.Items.Any(x => x.Status != PhysicalInventoryCountItemStatus.Counted))
            {
                throw new DomainRuleException("No se puede finalizar: existen productos pendientes o con reconteo requerido.");
            }

            var stocks = await repository.GetStocksForUpdateAsync(count.Items.Select(x => x.ProductId).ToList(), count.WarehouseId, transactionCancellationToken);
            var stockByProduct = stocks.DistinctBy(x => x.ProductId).ToDictionary(x => x.ProductId);
            foreach (var item in count.Items)
            {
                if (!stockByProduct.TryGetValue(item.ProductId, out var stock))
                {
                    throw new NotFoundException("No se encontró la existencia de uno de los productos de la toma.");
                }

                var difference = item.Difference ?? throw new DomainRuleException("La diferencia del conteo no está disponible.");
                var before = stock.CurrentStock;
                var after = before + difference;
                if (after < 0)
                {
                    throw new DomainRuleException($"El ajuste dejaría con existencia negativa al producto {item.Product.DisplayName}.");
                }

                if (difference != 0)
                {
                    stock.ReplaceForSystem(after);
                    await inventoryRepository.AddMovementAsync(new InventoryMovement(
                        item.ProductId,
                        difference > 0 ? InventoryMovementType.PositiveAdjustment : InventoryMovementType.NegativeAdjustment,
                        Math.Abs(difference),
                        before,
                        after,
                        dateTimeProvider.UtcNow,
                        currentUserAccessor.UserName,
                        $"TOMA-FISICA:{count.Id:N}",
                        BuildMovementNote(count.Name, item),
                        count.Id,
                        item.Id,
                        warehouseId: count.WarehouseId), transactionCancellationToken);
                }

                item.RecordAdjustment(before, difference, after);
            }

            count.Finalize(dateTimeProvider.UtcNow, currentUserAccessor.UserName);
            await unitOfWork.SaveChangesAsync(transactionCancellationToken);
        }, cancellationToken);
    }

    public async Task<PhysicalInventoryCountExportDto> GetExportAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var count = await GetAsync(id, cancellationToken);
        if (count.Status != PhysicalInventoryCountStatus.Finalized)
        {
            throw new DomainRuleException("Solo las tomas finalizadas pueden exportarse.");
        }

        return new PhysicalInventoryCountExportDto(
            count,
            count.Items.Where(x => x.Difference > 0).Sum(x => x.Difference ?? 0),
            count.Items.Where(x => x.Difference < 0).Sum(x => x.Difference ?? 0));
    }

    private static string BuildMovementNote(string countName, PhysicalInventoryCountItem item)
    {
        var note = $"Ajuste por toma física de inventario '{countName}'. Referencia: {item.ReferenceStock}; conteo: {item.CountedStock}; diferencia: {item.Difference}.";
        if (!string.IsNullOrWhiteSpace(item.Comment))
        {
            note += $" Comentario: {item.Comment}";
        }
        return note.Length <= 500 ? note : note[..500];
    }

    private static PhysicalInventoryCountDetailDto MapDetail(PhysicalInventoryCount count, IReadOnlyDictionary<Guid, decimal> currentStocks)
    {
        var items = count.Items
            .OrderBy(x => x.Product.DisplayName)
            .Select(x => new PhysicalInventoryCountItemDto(
                x.Id,
                x.ProductId,
                x.Product.InternalCode,
                x.Product.DisplayName,
                x.Product.Category.Name,
                currentStocks.GetValueOrDefault(x.ProductId),
                x.ReferenceStock,
                x.CountedStock,
                x.Difference,
                x.Comment,
                x.CountedAtUtc,
                x.CountedBy,
                x.Status,
                x.OperationalStockBeforeAdjustment,
                x.AppliedAdjustment,
                x.OperationalStockAfterAdjustment))
            .ToList();

        return new PhysicalInventoryCountDetailDto(
            count.Id,
            count.Name,
            count.CreatedAtUtc,
            count.CreatedBy ?? "system",
            count.Scope,
            count.Categories.Select(x => x.Category.Name).OrderBy(x => x).ToList(),
            count.Status,
            count.FinalizedAtUtc,
            count.FinalizedBy,
            CreateSummary(count.Items),
            items);
    }

    private static PhysicalInventoryCountSummaryDto CreateSummary(IEnumerable<PhysicalInventoryCountItem> source)
    {
        var items = source.ToList();
        return new PhysicalInventoryCountSummaryDto(
            items.Count,
            items.Count(x => x.Status == PhysicalInventoryCountItemStatus.Counted),
            items.Count(x => x.Status != PhysicalInventoryCountItemStatus.Counted),
            items.Count(x => x.Status == PhysicalInventoryCountItemStatus.Counted && x.Difference == 0),
            items.Count(x => x.Status == PhysicalInventoryCountItemStatus.Counted && x.Difference < 0),
            items.Count(x => x.Status == PhysicalInventoryCountItemStatus.Counted && x.Difference > 0),
            items.Count(x => x.Status == PhysicalInventoryCountItemStatus.RecountRequired));
    }
}
