using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Recipes;
using SistemiPos.Domain.Restaurant;

namespace SistemiPos.Application.Restaurant;

public sealed partial class RestaurantService
{
    private static bool IsVisible(Product product, Guid branch) => product.IsActive && !product.ManagesVariants
        && product.BaseUnit is { IsActive: true }
        && product.ArticleType is not (ArticleType.Ingrediente or ArticleType.Insumo or ArticleType.Subreceta or ArticleType.Empaque)
        && !product.BranchSettings.Any(x => x.BranchId == branch && (!x.IsEnabled || !x.IsVisible));

    private async Task<List<OrderLineSnapshot>> Quote(List<OrderItemInput> items, List<ComboInput> combos, Guid branch,
        Guid warehouse, RestaurantDefinition setup, CancellationToken ct, int comboSequence = 0)
    {
        if (items is null || combos is null || items.Count + combos.Count == 0 || items.Count + combos.Count > 100) throw new DomainRuleException("El pedido requiere entre 1 y 100 líneas.");
        var products = (await repository.ProductsAsync(ct)).ToDictionary(x => x.Id);
        var tax = await repository.TaxAsync(ct); var result = new List<OrderLineSnapshot>();
        async Task<OrderLineSnapshot> Build(OrderItemInput input, bool promotion)
        {
            if (input is null || input.OptionIds is null || input.ExcludedProducts is null) throw new DomainRuleException("Faltan selecciones de la línea.");
            if (!products.TryGetValue(input.ProductId, out var product) || !IsVisible(product, branch)) throw new DomainRuleException("Producto no vendible en la sucursal.");
            if (input.Quantity <= 0 || input.Quantity > 10000 || decimal.Round(input.Quantity, Math.Min(2, product.BaseUnit?.Precision ?? 2)) != input.Quantity)
                throw new DomainRuleException("La cantidad no respeta la precisión del producto.");
            var menu = setup.Menu.SingleOrDefault(x => x.ProductId == product.Id) ?? throw new DomainRuleException("Producto fuera del menú.");
            var station = setup.Stations.SingleOrDefault(x => x.Id == menu.StationId && x.Active) ?? throw new DomainRuleException("Estación de preparación no disponible.");
            if (input.OptionIds.Distinct().Count() != input.OptionIds.Count || input.ExcludedProducts.Distinct().Count() != input.ExcludedProducts.Count)
                throw new DomainRuleException("No repitas opciones ni exclusiones.");
            var selections = menu.Groups.SelectMany(x => x.Options).Where(x => input.OptionIds.Contains(x.Id) && x.Active).ToList();
            if (selections.Count != input.OptionIds.Count) throw new DomainRuleException("Hay opciones no permitidas.");
            foreach (var group in menu.Groups)
            {
                var count = group.Options.Count(x => input.OptionIds.Contains(x.Id));
                if (count < group.Minimum || count > group.Maximum) throw new DomainRuleException($"{group.Name}: selecciona entre {group.Minimum} y {group.Maximum} opciones.");
            }
            var version = await repository.RecipeAsync(product.Id, ct); var recipe = version?.Read();
            if (product.ArticleType == ArticleType.Preparado && recipe is null) throw new DomainRuleException("El preparado no tiene una receta activa.");
            var consumption = new List<RecipeConsumption>();
            if (recipe?.ConsumptionMode == RecipeConsumptionMode.OnSale)
            {
                if (input.ExcludedProducts.Any(id => !menu.ExcludableProducts.Contains(id) || recipe.Components.All(x => x.ProductId != id))) throw new DomainRuleException("La exclusión no está permitida en esta receta.");
                foreach (var c in recipe.Components.Where(x => !input.ExcludedProducts.Contains(x.ProductId)))
                {
                    var component = products.GetValueOrDefault(c.ProductId) ?? throw new DomainRuleException("Componente no encontrado.");
                    ValidateComponent(component, branch);
                    consumption.Add(new(component.Id, c.Name, c.BaseUnitCode, RecipeQuantities.Scale(c.BaseQuantity, input.Quantity, recipe.YieldQuantity, c.Precision), component.LastCost, "Costo al enviar pedido"));
                }
            }
            else
            {
                if (input.ExcludedProducts.Count > 0) throw new DomainRuleException("No se pueden excluir ingredientes de un artículo ya producido.");
                consumption.Add(new(product.Id, product.DisplayName, product.UnitOfMeasure, input.Quantity, product.LastCost, "Existencia terminada"));
            }
            foreach (var c in selections.SelectMany(x => x.Components))
            {
                var component = products.GetValueOrDefault(c.ProductId) ?? throw new DomainRuleException("Componente adicional no encontrado."); ValidateComponent(component, branch);
                consumption.Add(new(component.Id, component.DisplayName, component.UnitOfMeasure, RecipeQuantities.Scale(c.Quantity, input.Quantity, 1, component.BaseUnit!.Precision), component.LastCost, "Extra"));
            }
            var gross = Catalog.ProductPricingService.Instance.Calculate(product, input.Quantity, branch).GrossAmount + selections.Sum(x => x.Price) * input.Quantity;
            var rule = promotion ? setup.Promotions.Where(x => x.Active && x.ProductId == product.Id && x.FromUtc <= clock.UtcNow && x.ToUtc > clock.UtcNow)
                .OrderByDescending(x => x.Priority).ThenByDescending(x => x.Percent).ThenBy(x => x.Id).FirstOrDefault() : null;
            var discount = decimal.Round(gross * (rule?.Percent ?? 0) / 100, 2);
            var descriptions = selections.Select(x => $"{x.Name} (+{x.Price:0.00})").Concat(input.ExcludedProducts.Select(id => "Sin " + products[id].DisplayName));
            return new(product.Id, product.DisplayName, input.Quantity, decimal.Round(gross, 2), discount, product.AppliesTax ? tax : 0,
                station.Id, station.Name, version?.Id, string.Join(", ", descriptions), consumption.GroupBy(x => x.ProductId)
                    .Select(g => g.First() with { Quantity = g.Sum(x => x.Quantity) }).ToList(), PromotionId: rule?.Id, PromotionName: rule?.Name,
                PromotionFromUtc: rule?.FromUtc, PromotionToUtc: rule?.ToUtc);
        }
        foreach (var input in items) result.Add(await Build(input, true));
        foreach (var input in combos)
        {
            if (input is null || input.Picks is null || input.Picks.Any(x => x is null)) throw new DomainRuleException("Faltan selecciones del combo.");
            var combo = setup.Combos.SingleOrDefault(x => x.Id == input.ComboId && x.Active && x.FromUtc <= clock.UtcNow && x.ToUtc > clock.UtcNow)
                ?? throw new DomainRuleException("Combo no disponible o fuera de vigencia.");
            if (input.Picks.Count != combo.Groups.Sum(x => x.Quantity)) throw new DomainRuleException("Completa las selecciones del combo.");
            if (result.Count + input.Picks.Count > 200) throw new DomainRuleException("El pedido excede 200 componentes de cocina.");
            comboSequence++;
            var parts = new List<OrderLineSnapshot>();
            foreach (var group in combo.Groups)
            {
                var picks = input.Picks.Where(x => x.GroupId == group.Id).ToList();
                if (picks.Count != group.Quantity || picks.Any(x => !group.ProductIds.Contains(x.ProductId))) throw new DomainRuleException($"Revisa la selección de {group.Name}.");
                foreach (var pick in picks) parts.Add(await Build(new(pick.ProductId, 1, pick.OptionIds ?? [], pick.ExcludedProducts ?? []), false));
            }
            // Combo price includes the standard products; selected extras keep their configured surcharge.
            var extra = input.Picks.Sum(p => setup.Menu.Single(m => m.ProductId == p.ProductId).Groups.SelectMany(g => g.Options).Where(o => (p.OptionIds ?? []).Contains(o.Id)).Sum(o => o.Price));
            var comboTotal = combo.Price + extra;
            var gross = parts.Sum(x => x.Gross); decimal allocated = 0;
            for (var i = 0; i < parts.Count; i++)
            {
                var share = i == parts.Count - 1 ? comboTotal - allocated : Math.Min(comboTotal - allocated, decimal.Round(comboTotal * (gross == 0 ? 1m / parts.Count : parts[i].Gross / gross), 2));
                allocated += share; result.Add(parts[i] with { Gross = share, Discount = 0, ComboId = combo.Id, ComboName = combo.Name, ComboSequence = comboSequence, ComboFromUtc = combo.FromUtc, ComboToUtc = combo.ToUtc });
            }
        }
        if (result.Count > 200) throw new DomainRuleException("El pedido excede 200 componentes de cocina. Divide la captura en pedidos más pequeños.");
        return result;
    }
    private static void ValidateComponent(Product product, Guid branch)
    {
        if (!product.IsActive || product.ManagesVariants || product.BaseUnit is not { IsActive: true }
            || product.BranchSettings.Any(x => x.BranchId == branch && !x.IsEnabled)) throw new DomainRuleException($"Componente no disponible: {product.DisplayName}.");
    }
    private async Task<decimal> AvailableAsync(Product product, Guid warehouse, CancellationToken ct)
    {
        async Task<decimal> Stock(Guid id)
        {
            var p = (await repository.ProductsAsync(ct)).First(x => x.Id == id);
            var stock = await repository.StockAsync(id, warehouse, ct); if (stock is null) return 0;
            if (!p.ManagesLots) return Math.Max(0, stock.CurrentStock - stock.ReservedQuantity);
            var today = DateOnly.FromDateTime(clock.UtcNow.ToOffset(TimeSpan.FromHours(-6)).Date);
            var lots = await repository.LotsAsync(id, warehouse, ct);
            var reservations = await repository.ReservationsAsync(warehouse, null, ct);
            return lots.Where(x => !p.ManagesExpiry || x.ExpiryDate >= today).Sum(x => Math.Max(0, x.AvailableQuantity - reservations.Where(r => r.Status == ReservationStatus.Active && r.LotId == x.Id).Sum(r => r.Quantity)));
        }
        var recipe = (await repository.RecipeAsync(product.Id, ct))?.Read();
        if (recipe?.ConsumptionMode != RecipeConsumptionMode.OnSale) return await Stock(product.Id);
        decimal? minimum = null;
        foreach (var c in recipe.Components) { var capacity = await Stock(c.ProductId) * recipe.YieldQuantity / c.BaseQuantity; minimum = Math.Min(minimum ?? capacity, capacity); }
        var scale = (decimal)Math.Pow(10, Math.Min(2, recipe.YieldPrecision));
        return decimal.Floor((minimum ?? 0) * scale) / scale;
    }
    private async Task Reserve(RestaurantOrder order, IEnumerable<RestaurantOrderLine> lines, CancellationToken ct)
    {
        var products = (await repository.ProductsAsync(ct)).ToDictionary(x => x.Id);
        var holds = await repository.ReservationsAsync(order.WarehouseId, null, ct);
        foreach (var line in lines)
        foreach (var component in line.Read().Consumption)
        {
            var product = products[component.ProductId]; ValidateComponent(product, order.BranchId);
            var stock = await repository.StockAsync(product.Id, order.WarehouseId, ct) ?? throw new DomainRuleException($"No hay existencia de {component.Name}.");
            stock.ReserveForOrder(component.Quantity);
            if (product.ManagesLots)
            {
                var lots = await repository.LotsAsync(product.Id, order.WarehouseId, ct);
                var missing = stock.CurrentStock - lots.Sum(x => x.AvailableQuantity);
                if (missing > 0 && !product.ManagesExpiry)
                {
                    var opening = new InventoryExpiryLot(product.Id, null, missing, "SALDO INICIAL", clock.UtcNow, order.WarehouseId, isOpeningBalance: true, unitCost: product.LastCost);
                    repository.Add(opening); lots.Add(opening);
                }
                else if (missing != 0) throw new DomainRuleException($"Concilia los lotes y vencimientos de {product.DisplayName} antes de reservar.");
                var reserved = holds.Where(x => x.Status == ReservationStatus.Active && x.LotId.HasValue).GroupBy(x => x.LotId!.Value).ToDictionary(x => x.Key, x => x.Sum(r => r.Quantity));
                var plan = InventoryLotAllocation.Plan(lots, component.Quantity, product.ManagesExpiry, DateOnly.FromDateTime(clock.UtcNow.ToOffset(TimeSpan.FromHours(-6)).Date), reserved: reserved);
                foreach (var allocation in plan)
                {
                    var hold = new OrderReservation(order.Id, line.Id, product.Id, order.WarehouseId, allocation.Lot.Id, allocation.Quantity, allocation.Lot.UnitCost ?? component.UnitCost);
                    repository.Add(hold); holds.Add(hold);
                }
            }
            else { var hold = new OrderReservation(order.Id, line.Id, product.Id, order.WarehouseId, null, component.Quantity, component.UnitCost); repository.Add(hold); holds.Add(hold); }
        }
    }
    private async Task Consume(RestaurantOrder order, RestaurantOrderLine line, CancellationToken ct)
    {
        var holds = (await repository.ReservationsAsync(order.WarehouseId, order.Id, ct)).Where(x => x.LineId == line.Id && x.Status == ReservationStatus.Active).ToList();
        var consumedAt = clock.UtcNow; var sequence = 0;
        foreach (var hold in holds)
        {
            var stock = await repository.StockAsync(hold.ProductId, hold.WarehouseId, ct) ?? throw new DomainRuleException("Existencia no encontrada.");
            stock.ReleaseOrderReservation(hold.Quantity); var before = stock.CurrentStock; stock.ReplaceForSystem(before - hold.Quantity);
            var movement = new InventoryMovement(hold.ProductId, InventoryMovementType.OrderConsumption, hold.Quantity, before, stock.CurrentStock,
                consumedAt.AddTicks(sequence++), user.UserName, order.Number, $"Preparación de línea {line.Id}", warehouseId: hold.WarehouseId, unitCost: hold.UnitCost);
            movement.SetLotDetails(null, selectedLotId: hold.LotId); hold.Consume(movement.Id); repository.Add(movement);
        }
    }
    private async Task Release(RestaurantOrder order, RestaurantOrderLine line, decimal fraction, CancellationToken ct)
    {
        var holds = (await repository.ReservationsAsync(order.WarehouseId, order.Id, ct)).Where(x => x.LineId == line.Id && x.Status == ReservationStatus.Active).ToList();
        var products = (await repository.ProductsAsync(ct)).ToDictionary(x => x.Id);
        foreach (var group in holds.GroupBy(x => x.ProductId))
        {
            var current = group.Sum(x => x.Quantity);
            var retained = decimal.Round(current * (1 - fraction), products[group.Key].BaseUnit!.Precision, MidpointRounding.AwayFromZero);
            if (fraction < 1 && retained <= 0) throw new DomainRuleException("La cancelación parcial no conserva una cantidad válida del componente pendiente.");
            var release = current - retained;
            foreach (var hold in group.Reverse())
            {
                var quantity = Math.Min(release, hold.Quantity); if (quantity == 0) continue;
                var before = hold.Quantity; hold.Release(quantity / before);
                var stock = await repository.StockAsync(hold.ProductId, hold.WarehouseId, ct) ?? throw new DomainRuleException("Existencia no encontrada.");
                stock.ReleaseOrderReservation(before - hold.Quantity); release -= before - hold.Quantity;
            }
        }
    }
}
