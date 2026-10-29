using System.Text.Json;
using SistemiPos.Application.Orders;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Orders;
using SistemiPos.Domain.Organizations;
using SistemiPos.Domain.Recipes;

namespace SistemiPos.UnitTests.Orders;

public sealed class RestaurantOrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 15, 0, 0, TimeSpan.Zero);
    private static RestaurantOrder MakeOrder(bool prepared = true, decimal quantity = 3)
    {
        var branch = Guid.NewGuid();
        var terminal = new PosTerminal(branch, new Warehouse(branch, "COCINA", "Cocina"), "TERMINAL-1", "CAJA-1");
        var formula = new RecipeFormula(Guid.NewGuid(), "PIZZA", "Pizza", 1, "unidad", null, []);
        return new RestaurantOrder(terminal, Guid.NewGuid(), [new RestaurantOrderLine(formula.ProductId, "Pizza", quantity,
            20, 20 * quantity, 12, false, prepared ? JsonSerializer.Serialize(formula) : null, prepared ? 1 : null)]);
    }
    private static void Confirm(RestaurantOrder order) => order.Apply(OrderEventType.Confirm, null, 0, "confirm", Now, "tester");

    [Fact]
    public void Revision_Preserves_The_Original_Line_And_Is_Forbidden_After_Preparation_Starts()
    {
        var order = MakeOrder(); Confirm(order); var old = order.Lines.Single();
        var replacement = new RestaurantOrderLine(old.ProductId, "Nueva configuración", 2, 20, 40, 12, false, "{}", 2);
        order.AppendRevision(old.Id, replacement, "revision", "Cambio de cantidad", Now, "tester");
        Assert.Equal(2, order.Lines.Count); Assert.Equal(3, old.CancelledQuantity);
        Assert.Equal(old.Id, replacement.ReplacesLineId); Assert.Equal(2, replacement.RecipeRevision);
        order.Apply(OrderEventType.StartPreparation, replacement.Id, 1, "start", Now, "tester");
        Assert.Throws<DomainRuleException>(() => order.AppendRevision(replacement.Id,
            new RestaurantOrderLine(old.ProductId, "Otra", 1, 20, 20, 12, false, "{}", 3), "revision-after", "Cambio", Now, "tester"));
    }

    [Fact]
    public void Only_Prepared_Quantities_Can_Be_Sold_And_Cannot_Be_Sold_Or_Cancelled_Again()
    {
        var order = MakeOrder(); Confirm(order); var line = order.Lines.Single();
        Assert.Throws<DomainRuleException>(() => order.RecordSale(line.Id, 1));
        order.Apply(OrderEventType.StartPreparation, line.Id, 1, "start", Now, "tester");
        order.Apply(OrderEventType.FinishPreparation, line.Id, 1, "finish", Now, "tester");
        order.RecordSale(line.Id, 1);
        Assert.Equal(1, line.SoldQuantity); Assert.Equal(0, line.SellableQuantity);
        Assert.Throws<DomainRuleException>(() => order.RecordSale(line.Id, 1));
        Assert.Throws<DomainRuleException>(() => order.Apply(OrderEventType.Cancel, line.Id, 3, "cancel", Now, "tester", "Cancelar"));
    }

    [Fact]
    public void Partial_Checkouts_Keep_The_Exact_Original_Gross_Amount()
    {
        var branch = Guid.NewGuid();
        var terminal = new PosTerminal(branch, new Warehouse(branch, "B", "Bodega"), "T", "C");
        var line = new RestaurantOrderLine(Guid.NewGuid(), "Producto", 3, 3.333333m, 10, 0, true, null, null);
        var order = new RestaurantOrder(terminal, Guid.NewGuid(), [line]); Confirm(order);
        var values = new List<decimal>();
        for (var i = 0; i < 3; i++) { values.Add(line.QuoteGross(1)); order.RecordSale(line.Id, 1); }
        Assert.Equal(new[] { 3.33m, 3.34m, 3.33m }, values);
        Assert.Equal(10, line.AllocatedGrossAmount);
    }

    [Fact]
    public void Terminal_Rejects_Warehouse_From_Another_Branch()
    {
        var warehouse = new Warehouse(Guid.NewGuid(), "COCINA", "Cocina");
        Assert.Throws<DomainRuleException>(() => new PosTerminal(Guid.NewGuid(), warehouse, "T1", "C1"));
    }

    [Fact]
    public void Preparation_Requires_Confirmation_And_Supports_Partial_Quantities()
    {
        var order = MakeOrder();
        var line = order.Lines.Single();
        Assert.Throws<DomainRuleException>(() => order.Apply(OrderEventType.StartPreparation, line.Id, 1, "early", Now, "tester"));
        Confirm(order);
        order.Apply(OrderEventType.StartPreparation, line.Id, 2, "start", Now, "tester");
        order.Apply(OrderEventType.FinishPreparation, line.Id, 1, "finish", Now, "tester");
        order.Apply(OrderEventType.Dispatch, line.Id, 1, "dispatch", Now, "tester");
        Assert.Equal(1, line.PendingQuantity);
        Assert.Equal(1, line.PreparingQuantity);
        Assert.Equal(0, line.PreparedQuantity);
        Assert.Equal(1, line.DispatchedQuantity);
        Assert.Throws<DomainRuleException>(() => order.Apply(OrderEventType.Dispatch, line.Id, 1, "dispatch-too-soon", Now, "tester"));
        Assert.Equal(4, order.Events.Count);
    }

    [Fact]
    public void Replayed_Event_Does_Not_Advance_Or_Duplicate_And_Different_Payload_Is_Rejected()
    {
        var order = MakeOrder();
        Confirm(order);
        var line = order.Lines.Single();
        order.Apply(OrderEventType.StartPreparation, line.Id, 1, "start", Now, "tester");
        var revision = order.Revision;
        order.Apply(OrderEventType.StartPreparation, line.Id, 1, "start", Now, "tester");
        Assert.Equal(1, line.PreparingQuantity);
        Assert.Equal(2, order.Events.Count);
        Assert.Equal(revision, order.Revision);
        Assert.Throws<DomainRuleException>(() => order.Apply(OrderEventType.StartPreparation, line.Id, 2, "start", Now, "tester"));
    }

    [Fact]
    public void Reventa_Dispatches_Without_Preparation_And_Cannot_Dispatch_Twice()
    {
        var order = MakeOrder(false, 1);
        Confirm(order);
        var line = order.Lines.Single();
        Assert.Throws<DomainRuleException>(() => order.Apply(OrderEventType.StartPreparation, line.Id, 1, "start", Now, "tester"));
        order.Apply(OrderEventType.Dispatch, line.Id, 1, "dispatch", Now, "tester");
        Assert.Equal(1, line.DispatchedQuantity);
        Assert.Throws<DomainRuleException>(() => order.Apply(OrderEventType.Dispatch, line.Id, 1, "another-dispatch", Now, "tester"));
    }

    [Fact]
    public void Cancellation_Preserves_Consumed_Quantities_And_Requires_Reason()
    {
        var order = MakeOrder();
        Confirm(order);
        var line = order.Lines.Single();
        order.Apply(OrderEventType.StartPreparation, line.Id, 2, "start", Now, "tester");
        order.Apply(OrderEventType.FinishPreparation, line.Id, 1, "finish", Now, "tester");
        Assert.Throws<DomainRuleException>(() => order.Apply(OrderEventType.Cancel, line.Id, 3, "cancel", Now, "tester"));
        order.Apply(OrderEventType.Cancel, line.Id, 3, "cancel", Now, "tester", "Cliente canceló");
        Assert.Equal(2, line.StartedQuantity);
        Assert.Equal(2, line.CancelledAfterPreparationQuantity);
        Assert.Equal(3, line.CancelledQuantity);
        Assert.Equal(0, line.PendingQuantity);
        Assert.Equal(0, line.PreparingQuantity);
        Assert.Equal(0, line.PreparedQuantity);
    }

    [Fact]
    public void Confirmed_Order_Rejects_Replacement_Of_Historical_Lines()
    {
        var order = MakeOrder();
        Confirm(order);
        Assert.Throws<DomainRuleException>(() => order.ReplaceDraftLines([]));
    }

    [Fact]
    public void Recipe_Consumption_Expands_Yields_And_Aggregates_Shared_Ingredients()
    {
        var ingredient = Guid.NewGuid();
        var subrecipe = new RecipeFormula(Guid.NewGuid(), "SALSA", "Salsa", 4, "kg", null,
            [new(ingredient, "SAL", "Sal", Guid.NewGuid(), "kg", 0.000004m, 0.000004m, "kg", null)]);
        var formula = new RecipeFormula(Guid.NewGuid(), "PIZZA", "Pizza", 2, "unidad", null,
            [new(subrecipe.ProductId, "SALSA", "Salsa", Guid.NewGuid(), "kg", 2, 2, "kg", subrecipe),
             new(ingredient, "SAL", "Sal", Guid.NewGuid(), "kg", 0.000002m, 0.000002m, "kg", null)]);
        var result = OrderRecipeSnapshot.Consumption(formula, 3);
        Assert.Single(result);
        Assert.Equal(0.000006m, result[ingredient]);
    }

    [Fact]
    public void Published_Recipe_Uses_Its_Pinned_Subrecipe_Version()
    {
        var output = Guid.NewGuid();
        var ingredient = Guid.NewGuid();
        var subrecipeId = Guid.NewGuid();
        var subrecipeProduct = Guid.NewGuid();
        var unit = Guid.NewGuid();
        var recipe = new Recipe(output, unit, "kg", 1,
            [new RecipeIngredient(subrecipeProduct, unit, "kg", 1, 1, "kg", subrecipeId)], null);
        RecipeSnapshot Snapshot(Guid product, decimal quantity, Guid? child = null) => new(product, "Producto",
            unit, "kg", 6, 1, null, RecipeConsumptionMode.OnSale,
            [new(child.HasValue ? subrecipeProduct : ingredient, "ING", "Ingrediente", unit, "kg", quantity,
                unit, "kg", 6, quantity, child)]);
        var original = new RecipeVersion(subrecipeId, 1, Now, Snapshot(subrecipeProduct, 2));
        var updated = new RecipeVersion(subrecipeId, 2, Now, Snapshot(subrecipeProduct, 9));
        var published = new RecipeVersion(recipe.Id, recipe.Revision, Now, Snapshot(output, 1, original.Id));

        var formula = OrderRecipeSnapshot.Capture(recipe, [recipe], [original, updated, published]);

        Assert.Equal(2, OrderRecipeSnapshot.Consumption(formula, 1)[ingredient]);
    }

    [Fact]
    public void Inventory_Stock_Movement_And_Lot_Preserve_Six_Decimals()
    {
        var product = Guid.NewGuid();
        var stock = new InventoryStock(product);
        stock.ReplaceForSystem(0.000009m);
        var lot = new InventoryExpiryLot(product, null, 0.000009m, "PRECISION");
        lot.Consume(0.000003m);
        stock.ReplaceForSystem(stock.CurrentStock - 0.000003m);
        var movement = new InventoryMovement(product, InventoryMovementType.PosOrderConsumption, 0.000003m,
            0.000009m, stock.CurrentStock, Now, "tester", "PED", null);
        Assert.Equal(0.000006m, stock.CurrentStock);
        Assert.Equal(stock.CurrentStock, lot.AvailableQuantity);
        Assert.Equal(0.000003m, movement.Quantity);
        Assert.Equal(0.000009m, movement.PreviousStock);
        Assert.Equal(stock.CurrentStock, movement.NewStock);
    }
}
