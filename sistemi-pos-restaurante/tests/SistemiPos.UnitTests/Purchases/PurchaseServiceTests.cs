using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Purchases;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Purchases;
using SistemiPos.Domain.Suppliers;

namespace SistemiPos.UnitTests.Purchases;

public sealed class PurchaseServiceTests
{
    [Theory]
    [InlineData(PurchaseTermsType.Cash)]
    [InlineData(PurchaseTermsType.Credit)]
    public async Task Cancel_Direct_Purchase_Preserves_History_Reverses_Stock_And_Unpaid_Account(PurchaseTermsType terms)
    {
        var product = new Product("CANCEL", null, "Producto", null, Guid.NewGuid(), "Marca", "u", 0, 0, 1, 0, false);
        var supplier = new Supplier("Proveedor", "123", null, null, null, null, null);
        var placeholder = new Purchase("OLD", supplier.Id, DateTimeOffset.UtcNow, "tester", [new PurchaseItem(product.Id, 1, 1)]);
        var repository = new FakePurchaseRepository(product, supplier, placeholder);
        var work = new FakeUnitOfWork();
        var service = new PurchaseService(repository, work, new FakeCurrentUserAccessor(), new FakeDateTimeProvider());
        var request = new CreateDirectPurchaseRequest(supplier.Id, DateTimeOffset.UtcNow, "CANCEL-1", supplier.Nit,
            terms, terms == PurchaseTermsType.Credit ? 30 : null, null,
            [new DirectPurchaseItemRequest(product.Id, 3, 1, 5, SupplierPackageType.Unit, "Producto", null)]);
        var id = await service.CreateDirectPurchaseAsync(request);
        Assert.Null(repository.CreatedPurchase!.PurchaseOrderId);
        await service.CancelPurchaseAsync(id, "  Error de factura  ");
        var purchase = repository.CreatedPurchase;
        Assert.Equal(DocumentStatus.Cancelled, purchase.Status);
        Assert.Equal("Error de factura", purchase.CancellationReason);
        Assert.Equal("tester", purchase.CancelledBy);
        Assert.NotNull(purchase.CancelledAtUtc);
        Assert.Equal(15, purchase.Total);
        Assert.Single(purchase.Items);
        Assert.Equal(0, repository.Stock!.CurrentStock);
        var reversal = Assert.Single(repository.Movements, x => x.Type == InventoryMovementType.PurchaseReversal);
        Assert.Equal(id, reversal.PurchaseId);
        Assert.Equal(3, reversal.Quantity);
        Assert.Equal(2, work.SaveChangesCalls);
        if (terms == PurchaseTermsType.Credit)
        {
            Assert.Equal(SistemiPos.Domain.Finance.AccountStatus.Cancelled, repository.Payable!.Status);
            Assert.Equal(0, repository.Payable.Balance);
            Assert.Equal(15, repository.Payable.Total);
        }
        await Assert.ThrowsAsync<DomainRuleException>(() => service.CancelPurchaseAsync(id, "Otra vez"));
        await Assert.ThrowsAsync<DomainRuleException>(() => service.ConfirmPurchaseAsync(id));
        Assert.Throws<DomainRuleException>(() => purchase.ReplaceItems([new PurchaseItem(product.Id, 1, 1)]));
        await Assert.ThrowsAsync<DomainRuleException>(() => service.CreateDirectPurchaseAsync(request));
        Assert.Equal(2, work.SaveChangesCalls);
        Assert.Equal(2, repository.Movements.Count);
    }

    [Theory]
    [InlineData("paid")]
    [InlineData("stock")]
    [InlineData("reason")]
    [InlineData("scope")]
    public async Task Cancel_Purchase_Rejects_Invalid_Reversal_Before_Mutating(string failure)
    {
        var product = new Product("BLOCK", null, "Producto", null, Guid.NewGuid(), "Marca", "u", 0, 0, 1, 0, false);
        var supplier = new Supplier("Proveedor", "123", null, null, null, null, null);
        var placeholder = new Purchase("OLD", supplier.Id, DateTimeOffset.UtcNow, "tester", [new PurchaseItem(product.Id, 1, 1)]);
        var repository = new FakePurchaseRepository(product, supplier, placeholder);
        var work = new FakeUnitOfWork();
        var service = new PurchaseService(repository, work, new FakeCurrentUserAccessor(), new FakeDateTimeProvider(),
            operationContext: failure == "scope" ? new NoBranchAccess() : null);
        var id = await service.CreateDirectPurchaseAsync(new CreateDirectPurchaseRequest(supplier.Id, DateTimeOffset.UtcNow,
            "BLOCK-1", supplier.Nit, PurchaseTermsType.Credit, 30, null,
            [new DirectPurchaseItemRequest(product.Id, 3, 1, 5, SupplierPackageType.Unit, "Producto", null)]));
        if (failure == "stock") repository.Stock!.ReplaceForSystem(2);
        if (failure == "paid") repository.Payable!.AddPayment(new SistemiPos.Domain.Finance.PayablePayment(
            DateTimeOffset.UtcNow, 1, SistemiPos.Domain.Finance.FinancialPaymentMethod.Cash,
            SistemiPos.Domain.Finance.MoneySourceType.Other, null, null, "tester", null));
        var previousStock = repository.Stock!.CurrentStock;
        var previousBalance = repository.Payable!.Balance;
        await Assert.ThrowsAsync<DomainRuleException>(() => service.CancelPurchaseAsync(id, failure == "reason" ? " " : "Error"));
        Assert.Equal(DocumentStatus.Confirmed, repository.CreatedPurchase!.Status);
        Assert.Equal(previousStock, repository.Stock.CurrentStock);
        Assert.Equal(previousBalance, repository.Payable.Balance);
        Assert.Single(repository.Movements);
        Assert.Equal(1, work.SaveChangesCalls);
    }

    [Theory]
    [InlineData(5, 0)]
    [InlineData(7, 0)]
    [InlineData(3, 2)]
    public async Task Cancel_Receipt_Reopens_Order_And_Allows_New_Receipt(decimal firstQuantity, decimal secondQuantity)
    {
        var product = new Product("REOPEN", null, "Producto", null, Guid.NewGuid(), "Marca", "u", 0, 0, 1, 0, false);
        var supplier = new Supplier("Proveedor", "123", null, null, null, null, null);
        var placeholder = new Purchase("OLD", supplier.Id, DateTimeOffset.UtcNow, "tester", [new PurchaseItem(product.Id, 1, 1)]);
        var order = new PurchaseOrder("ORD", supplier.Id, DateTimeOffset.UtcNow, "tester", [new PurchaseOrderItem(product.Id, 5)]);
        order.Submit(DateTimeOffset.UtcNow, "tester");
        var repository = new FakePurchaseRepository(product, supplier, placeholder) { Order = order };
        var service = new PurchaseService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());
        var item = order.Items.Single();
        ReceivePurchaseOrderRequest Receipt(decimal quantity, string invoice) => new(
            [new ReceivePurchaseOrderItemRequest(item.Id, quantity, 5)], invoice, DateTimeOffset.UtcNow, SupplierNit: supplier.Nit);
        await service.ReceivePurchaseOrderAsync(order.Id, Receipt(firstQuantity, "REC-1"));
        var firstPurchase = repository.CreatedPurchase!;
        if (secondQuantity > 0) await service.ReceivePurchaseOrderAsync(order.Id, Receipt(secondQuantity, "REC-2"));
        await service.CancelPurchaseAsync(firstPurchase.Id, "Recepción incorrecta");
        Assert.Equal(secondQuantity, item.ReceivedQuantity);
        Assert.Equal(5 - secondQuantity, item.PendingQuantity);
        Assert.Equal(secondQuantity == 0 ? PurchaseOrderStatus.Requested : PurchaseOrderStatus.PartiallyReceived, order.Status);
        Assert.Equal(secondQuantity, repository.Stock!.CurrentStock);
        var mapped = (await service.GetPurchaseOrderAsync(order.Id))!;
        Assert.Equal(DocumentStatus.Cancelled, mapped.Receipts!.First().Status);
        Assert.Equal(secondQuantity * 5, mapped.ActualTotal);
        Assert.Equal(firstQuantity, order.Receipts.First().Items.Single().ReceivedQuantity);
        Assert.Throws<DomainRuleException>(() => order.Update(supplier.Id, [new PurchaseOrderItem(product.Id, 8)], null));
        await service.ReceivePurchaseOrderAsync(order.Id, Receipt(item.PendingQuantity, "REC-3"));
        Assert.Equal(PurchaseOrderStatus.Received, order.Status);
        Assert.Equal(5, item.ReceivedQuantity);
        Assert.Equal(5, repository.Stock.CurrentStock);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Direct_Purchase_Records_Lot_Data_Only_When_Product_Tracks_Lots(bool tracksLots)
    {
        var product = new Product("LOTE", null, "Producto", null, Guid.NewGuid(), "Marca", "u", 0, 0, 1, 0, false);
        product.ConfigureInventoryTracking(tracksLots, tracksLots, tracksLots ? 3 : null);
        var supplier = new Supplier("Proveedor", "123", null, null, null, null, null);
        var placeholder = new Purchase("OLD", supplier.Id, DateTimeOffset.UtcNow, "tester", [new PurchaseItem(product.Id, 1, 1)]);
        var repository = new FakePurchaseRepository(product, supplier, placeholder);
        var service = new PurchaseService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());
        var expiry = new DateOnly(2026, 10, 30);
        await service.CreateDirectPurchaseAsync(new CreateDirectPurchaseRequest(supplier.Id, DateTimeOffset.UtcNow,
            "FACT-LOTE", "123", PurchaseTermsType.Cash, null, null,
            [new DirectPurchaseItemRequest(product.Id, 2, 1, 5, SupplierPackageType.Unit,
                "Producto", null, expiry, LotNumber: "  PROV-123  ")]));
        var movement = Assert.Single(repository.Movements);
        Assert.Equal(tracksLots ? "PROV-123" : null, movement.EntryLotNumber);
        Assert.Equal(tracksLots ? expiry : (DateOnly?)null, movement.EntryExpiryDate);
        Assert.Equal(2, repository.Stock!.CurrentStock);
    }

    [Fact]
    public async Task Receipt_Resolves_Lot_By_Order_Item_And_Handles_PackageQuantity_Without_Client_ProductId()
    {
        var product = new Product("LOTE", null, "Producto", null, Guid.NewGuid(), "Marca", "u", 0, 0, 1, 0, false);
        product.ConfigureExpiry(true, 3);
        var supplier = new Supplier("Proveedor", "123", null, null, null, null, null);
        var placeholder = new Purchase("OLD", supplier.Id, DateTimeOffset.UtcNow, "tester", [new PurchaseItem(product.Id, 1, 1)]);
        var order = new PurchaseOrder("ORD-LOTE", supplier.Id, DateTimeOffset.UtcNow, "tester", [new PurchaseOrderItem(product.Id, 2)]);
        order.Submit(DateTimeOffset.UtcNow, "tester");
        var repository = new FakePurchaseRepository(product, supplier, placeholder) { Order = order };
        var work = new FakeUnitOfWork();
        var service = new PurchaseService(repository, work, new FakeCurrentUserAccessor(), new FakeDateTimeProvider());
        var expiry = new DateOnly(2026, 10, 30);
        var item = new ReceivePurchaseOrderItemRequest(order.Items.Single().Id, 0,
            ProductId: Guid.NewGuid(), PackageQuantity: 2, PackageCost: 5, ExpiryDate: expiry, LotNumber: "REC-123");
        await service.ReceivePurchaseOrderAsync(order.Id, new ReceivePurchaseOrderRequest([item], "FACT-LOTE", DateTimeOffset.UtcNow, SupplierNit: "123"));
        var movement = Assert.Single(repository.Movements);
        Assert.Equal(product.Id, movement.ProductId);
        Assert.Equal("REC-123", movement.EntryLotNumber);
        Assert.Equal(expiry, movement.EntryExpiryDate);
        Assert.Equal(2, repository.Stock!.CurrentStock);
        Assert.Equal(1, work.SaveChangesCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Receipt_Rejects_Missing_Expiry_Or_Oversized_Lot_Before_Changing_Stock(bool oversizedLot)
    {
        var product = new Product("LOTE", null, "Producto", null, Guid.NewGuid(), "Marca", "u", 0, 0, 1, 0, false);
        product.ConfigureExpiry(true, 3);
        var supplier = new Supplier("Proveedor", "123", null, null, null, null, null);
        var placeholder = new Purchase("OLD", supplier.Id, DateTimeOffset.UtcNow, "tester", [new PurchaseItem(product.Id, 1, 1)]);
        var order = new PurchaseOrder("ORD-LOTE", supplier.Id, DateTimeOffset.UtcNow, "tester", [new PurchaseOrderItem(product.Id, 2)]);
        order.Submit(DateTimeOffset.UtcNow, "tester");
        var repository = new FakePurchaseRepository(product, supplier, placeholder) { Order = order };
        var work = new FakeUnitOfWork();
        var service = new PurchaseService(repository, work, new FakeCurrentUserAccessor(), new FakeDateTimeProvider());
        var item = new ReceivePurchaseOrderItemRequest(order.Items.Single().Id, 2, 5,
            ExpiryDate: oversizedLot ? new DateOnly(2026, 10, 30) : null, LotNumber: oversizedLot ? new string('L', 101) : "REC-123");
        await Assert.ThrowsAsync<DomainRuleException>(() => service.ReceivePurchaseOrderAsync(order.Id,
            new ReceivePurchaseOrderRequest([item], "FACT-LOTE", DateTimeOffset.UtcNow, SupplierNit: "123")));
        Assert.Null(repository.Stock);
        Assert.Empty(repository.Movements);
        Assert.Null(repository.CreatedPurchase);
        Assert.Equal(PurchaseOrderStatus.Requested, order.Status);
        Assert.Equal(0, work.SaveChangesCalls);
    }

    [Fact]
    public async Task Purchase_Complete_Flow_Creates_Confirms_And_Updates_Stock()
    {
        var product = new Product("P-COMP-1", null, "Café", null, Guid.NewGuid(), "Marca", "Unidad", 10m, 8m, 15m, 2m, true);
        var supplier = new Supplier("Proveedor Uno", "1234567", null, null, null, null, null);
        var placeholder = new Purchase("C-OLD", supplier.Id, DateTimeOffset.UtcNow, "tester", [new PurchaseItem(product.Id, 1m, 10m)]);
        var repository = new FakePurchaseRepository(product, supplier, placeholder);
        var service = new PurchaseService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        var purchaseId = await service.CreatePurchaseAsync(new SavePurchaseRequest(supplier.Id, DateTimeOffset.UtcNow, [new PurchaseItemRequest(product.Id, 3m, 12m)]));
        Assert.NotNull(repository.CreatedPurchase);
        var created = repository.CreatedPurchase!;
        foreach (var item in created.Items) { item.Product = product; }
        created.Supplier = supplier;
        await service.ConfirmPurchaseAsync(purchaseId);

        Assert.Equal(DocumentStatus.Confirmed, created.Status);
        Assert.Equal(3m, repository.Stock!.CurrentStock);
        Assert.Equal(12m, product.LastCost);
        Assert.Equal(InventoryMovementType.PurchaseEntry, Assert.Single(repository.Movements).Type);
    }

    [Fact]
    public async Task ConfirmPurchaseAsync_Updates_Stock_Costs_And_Movement()
    {
        var categoryId = Guid.NewGuid();
        var product = new Product("P-001", null, "Café", null, categoryId, "Marca", "Unidad", 10m, 8m, 15m, 2m, true);
        var supplier = new Supplier("Proveedor Uno", "1234567", null, null, null, null, null);
        var purchase = new Purchase(
            "C-000001",
            supplier.Id,
            new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero),
            "tester",
            [new PurchaseItem(product.Id, 3m, 12m)]);

        purchase.Supplier = supplier;
        foreach (var item in purchase.Items)
        {
            item.Product = product;
        }

        var repository = new FakePurchaseRepository(product, supplier, purchase);
        var unitOfWork = new FakeUnitOfWork();
        var service = new PurchaseService(
            repository,
            unitOfWork,
            new FakeCurrentUserAccessor(),
            new FakeDateTimeProvider());

        await service.ConfirmPurchaseAsync(purchase.Id);

        Assert.Equal(DocumentStatus.Confirmed, purchase.Status);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
        Assert.NotNull(repository.Stock);
        Assert.Equal(3m, repository.Stock!.CurrentStock);
        Assert.Equal(12m, product.LastCost);
        Assert.Equal(10m, product.PreviousCost);
        Assert.Single(repository.Movements);
        Assert.Equal(InventoryMovementType.PurchaseEntry, repository.Movements[0].Type);
        Assert.Equal("C-000001", repository.Movements[0].SourceDocument);
        Assert.Equal(0m, repository.Movements[0].PreviousStock);
        Assert.Equal(3m, repository.Movements[0].NewStock);
    }

    [Fact]
    public async Task ConfirmPurchaseAsync_Rejects_Already_Confirmed_Purchase()
    {
        var categoryId = Guid.NewGuid();
        var product = new Product("P-001", null, "Café", null, categoryId, "Marca", "Unidad", 10m, 8m, 15m, 2m, true);
        var supplier = new Supplier("Proveedor Uno", "1234567", null, null, null, null, null);
        var purchase = new Purchase(
            "C-000001",
            supplier.Id,
            new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero),
            "tester",
            [new PurchaseItem(product.Id, 3m, 12m)]);

        purchase.Supplier = supplier;
        foreach (var item in purchase.Items)
        {
            item.Product = product;
        }

        purchase.Confirm();

        var repository = new FakePurchaseRepository(product, supplier, purchase);
        var service = new PurchaseService(
            repository,
            new FakeUnitOfWork(),
            new FakeCurrentUserAccessor(),
            new FakeDateTimeProvider());

        await Assert.ThrowsAsync<DomainRuleException>(() => service.ConfirmPurchaseAsync(purchase.Id));
    }

    [Fact]
    public async Task CreateDirectPurchaseAsync_Rejects_An_Invoice_Already_Registered_By_A_Receipt_For_The_Same_Supplier()
    {
        var product = new Product("P-INVOICE", null, "Café", null, Guid.NewGuid(), "Marca", "Unidad", 10m, 8m, 15m, 2m, true);
        var supplier = new Supplier("Proveedor Uno", "1234567", null, null, null, null, null);
        var existingPurchase = new Purchase("C-EXISTING", supplier.Id, DateTimeOffset.UtcNow, "tester",
            [new PurchaseItem(product.Id, 1m, 10m)], purchaseOrderId: Guid.NewGuid(), invoiceNumber: "FAC-45892");
        var repository = new FakePurchaseRepository(product, supplier, existingPurchase);
        var service = new PurchaseService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        var exception = await Assert.ThrowsAsync<DomainRuleException>(() => service.CreateDirectPurchaseAsync(
            new CreateDirectPurchaseRequest(supplier.Id, DateTimeOffset.UtcNow, "FAC-45892", "1234567",
                PurchaseTermsType.Cash, null, null,
                [new DirectPurchaseItemRequest(product.Id, 1m, 1, 10m, SupplierPackageType.Unit, "Café", null)])));

        Assert.Equal("La factura FAC-45892 ya fue registrada para este proveedor.", exception.Message);
        Assert.Null(repository.CreatedPurchase);
        Assert.Empty(repository.Movements);
    }

    [Fact]
    public async Task ReceivePurchaseOrderAsync_Rejects_An_Invoice_Already_Registered_By_A_Direct_Purchase_For_The_Same_Supplier()
    {
        var product = new Product("P-INVOICE-REC", null, "Azúcar", null, Guid.NewGuid(), "Marca", "Unidad", 10m, 8m, 15m, 2m, true);
        var supplier = new Supplier("Proveedor Uno", "1234567", null, null, null, null, null);
        var directPurchase = new Purchase("C-DIRECT", supplier.Id, DateTimeOffset.UtcNow, "tester",
            [new PurchaseItem(product.Id, 1m, 10m)], invoiceNumber: "FAC-45892");
        var order = new PurchaseOrder("PC-INVOICE", supplier.Id, DateTimeOffset.UtcNow, "tester",
            [new PurchaseOrderItem(product.Id, 1m)]);
        order.Submit(DateTimeOffset.UtcNow, "tester");
        var repository = new FakePurchaseRepository(product, supplier, directPurchase) { Order = order };
        var service = new PurchaseService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        var exception = await Assert.ThrowsAsync<DomainRuleException>(() => service.ReceivePurchaseOrderAsync(order.Id,
            new ReceivePurchaseOrderRequest([], "FAC-45892", DateTimeOffset.UtcNow, SupplierNit: "1234567")));

        Assert.Equal("La factura FAC-45892 ya fue registrada para este proveedor.", exception.Message);
        Assert.Null(repository.CreatedPurchase);
        Assert.Empty(repository.Movements);
    }

    [Fact]
    public async Task ReceivePurchaseOrderAsync_Allows_Overdelivery_And_Prevents_A_Second_Receipt()
    {
        var product = new Product("P-002", null, "Lápices", null, Guid.NewGuid(), "Marca", "Unidad", 1m, 1m, 2m, 2m, true);
        var supplier = new Supplier("Proveedor", "7654321", null, null, null, null, null);
        var purchase = new Purchase("C-000001", supplier.Id, DateTimeOffset.UtcNow, "tester", [new PurchaseItem(product.Id, 1m, 1m)]);
        purchase.Supplier = supplier;
        var order = new PurchaseOrder("PC-000001", supplier.Id, DateTimeOffset.UtcNow, "tester", [new PurchaseOrderItem(product.Id, 5m)]);
        order.Submit(DateTimeOffset.UtcNow, "tester");
        order.Supplier = supplier;
        foreach (var item in order.Items) item.Product = product;

        var repository = new FakePurchaseRepository(product, supplier, purchase) { Order = order };
        var service = new PurchaseService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());
        var itemId = order.Items.Single().Id;

        await service.ReceivePurchaseOrderAsync(order.Id, new ReceivePurchaseOrderRequest(
            [new ReceivePurchaseOrderItemRequest(itemId, 7m, 1.25m)], "FAC-100", DateTimeOffset.UtcNow,
            SupplierNit: "7654321"));

        Assert.Equal(PurchaseOrderStatus.Received, order.Status);
        Assert.Equal(7m, repository.Stock!.CurrentStock);
        Assert.Equal(7m, Assert.Single(repository.Movements).Quantity);
        Assert.Equal(order.Id, repository.CreatedPurchase!.PurchaseOrderId);
        Assert.NotNull(repository.CreatedReceipt);
        Assert.Equal(repository.CreatedPurchase.Id, repository.CreatedReceipt!.PurchaseId);
        await Assert.ThrowsAsync<DomainRuleException>(() => service.ReceivePurchaseOrderAsync(order.Id,
            new ReceivePurchaseOrderRequest([new ReceivePurchaseOrderItemRequest(itemId, 1m, 1m)], "FAC-101", DateTimeOffset.UtcNow,
                SupplierNit: "7654321")));
    }

    [Fact]
    public async Task PurchaseOrder_Complete_Flow_Creates_Receives_And_Updates_Kardex()
    {
        var product = new Product("P-ORD-1", null, "Cuadernos", null, Guid.NewGuid(), "Marca", "Unidad", 5m, 5m, 9m, 2m, true);
        var supplier = new Supplier("Proveedor pedidos", "8765432", null, null, null, null, null);
        var placeholder = new Purchase("C-PLACEHOLDER", supplier.Id, DateTimeOffset.UtcNow, "tester", [new PurchaseItem(product.Id, 1m, 5m)]);
        var repository = new FakePurchaseRepository(product, supplier, placeholder);
        var service = new PurchaseService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        var orderId = await service.CreatePurchaseOrderAsync(new CreatePurchaseOrderRequest(supplier.Id, [new PurchaseOrderItemRequest(product.Id, 6m)], Submit: true));
        Assert.NotNull(repository.Order);
        var order = repository.Order!;
        Assert.Equal(new DateTimeOffset(2026, 8, 30, 13, 0, 0, TimeSpan.Zero), order.RequestedAtUtc);
        var item = Assert.Single(order.Items);
        await service.ReceivePurchaseOrderAsync(orderId, new ReceivePurchaseOrderRequest(
            [new ReceivePurchaseOrderItemRequest(item.Id, 6m, 5.5m)], "FAC-200", DateTimeOffset.UtcNow,
            SupplierNit: "8765432"));

        Assert.Equal(PurchaseOrderStatus.Received, order.Status);
        Assert.Equal(6m, repository.Stock!.CurrentStock);
        Assert.Equal(InventoryMovementType.PurchaseEntry, Assert.Single(repository.Movements).Type);
        Assert.Equal(DocumentStatus.Confirmed, repository.CreatedPurchase!.Status);
    }

    [Fact]
    public async Task ReceivePurchaseOrderAsync_Accumulates_Partial_Receipts_Until_Complete()
    {
        var product = new Product("P-PART", null, "Harina", null, Guid.NewGuid(), "Marca", "kg", 5m, 5m, 9m, 2m, true);
        var supplier = new Supplier("Proveedor parcial", "8877665", null, null, null, null, null);
        var placeholder = new Purchase("C-OLD", supplier.Id, DateTimeOffset.UtcNow, "tester", [new PurchaseItem(product.Id, 1m, 1m)]);
        var order = new PurchaseOrder("PC-PART", supplier.Id, DateTimeOffset.UtcNow, "tester", [new PurchaseOrderItem(product.Id, 5m)]);
        order.Supplier = supplier;
        foreach (var line in order.Items) line.Product = product;
        order.Submit(DateTimeOffset.UtcNow, "tester");
        var repository = new FakePurchaseRepository(product, supplier, placeholder) { Order = order };
        var service = new PurchaseService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());
        var itemId = order.Items.Single().Id;

        await service.ReceivePurchaseOrderAsync(order.Id, new ReceivePurchaseOrderRequest(
            [new ReceivePurchaseOrderItemRequest(itemId, 2m, 5m)], "FAC-PART-1", DateTimeOffset.UtcNow,
            SupplierNit: supplier.Nit));

        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, order.Status);
        Assert.Equal(2m, order.Items.Single().ReceivedQuantity);
        Assert.Equal(3m, order.Items.Single().PendingQuantity);

        await service.ReceivePurchaseOrderAsync(order.Id, new ReceivePurchaseOrderRequest(
            [new ReceivePurchaseOrderItemRequest(itemId, 3m, 6m)], "FAC-PART-2", DateTimeOffset.UtcNow,
            SupplierNit: supplier.Nit));

        Assert.Equal(PurchaseOrderStatus.Received, order.Status);
        Assert.Equal(5m, order.Items.Single().ReceivedQuantity);
        Assert.Equal(0m, order.Items.Single().PendingQuantity);
        Assert.Equal(2, repository.CreatedPurchases.Count);
        Assert.Equal(2, repository.CreatedReceipts.Count);
        Assert.Equal(5m, repository.Stock!.CurrentStock);
    }

    [Fact]
    public async Task ReceivePurchaseOrderAsync_Creates_Payable_Only_For_Credit()
    {
        var product = new Product("P-CREDIT", null, "Papel", null, Guid.NewGuid(), "Marca", "Unidad", 4m, 3m, 7m, 1m, true);
        var supplier = new Supplier("Proveedor crédito", "1122334", null, null, null, null, null);
        var placeholder = new Purchase("C-OLD", supplier.Id, DateTimeOffset.UtcNow, "tester", [new PurchaseItem(product.Id, 1m, 1m)]);
        var order = new PurchaseOrder("PC-000099", supplier.Id, DateTimeOffset.UtcNow, "tester", [new PurchaseOrderItem(product.Id, 2m)]);
        order.Supplier = supplier;
        foreach (var item in order.Items) item.Product = product;
        order.Submit(DateTimeOffset.UtcNow, "tester");
        var repository = new FakePurchaseRepository(product, supplier, placeholder) { Order = order };
        var service = new PurchaseService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());
        var documentDate = new DateTimeOffset(2026, 8, 30, 0, 0, 0, TimeSpan.Zero);

        await service.ReceivePurchaseOrderAsync(order.Id, new ReceivePurchaseOrderRequest(
            [new ReceivePurchaseOrderItemRequest(order.Items.Single().Id, 2m, 4.5m)],
            "FAC-CRED-1", documentDate, PurchaseTermsType.Credit, 30, SupplierNit: "1122334"));

        Assert.NotNull(repository.Payable);
        Assert.Equal("FAC-CRED-1", repository.Payable!.InvoiceNumber);
        Assert.Equal(documentDate.AddDays(30), repository.Payable.DueDateUtc);
        Assert.Equal(9m, repository.Payable.Total);
    }

    [Fact]
    public async Task CreatePurchaseOrderAsync_Preserves_Presentation_And_Equivalent_Units()
    {
        var product = new Product("P-ORD-PRES", null, "Coca Cola 600 ml", null, Guid.NewGuid(), "Marca", "Unidad", 5m, 5m, 9m, 2m, true);
        var supplier = new Supplier("Proveedor pedidos", "8765432", null, null, null, null, null);
        var presentation = new PurchasePresentation("Caja", 24);
        var placeholder = new Purchase("C-PLACEHOLDER", supplier.Id, DateTimeOffset.UtcNow, "tester", [new PurchaseItem(product.Id, 1m, 5m)]);
        var repository = new FakePurchaseRepository(product, supplier, placeholder, presentation);
        var service = new PurchaseService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        await service.CreatePurchaseOrderAsync(new CreatePurchaseOrderRequest(supplier.Id,
            [new PurchaseOrderItemRequest(product.Id, 2m, presentation.Id)]));

        var item = Assert.Single(repository.Order!.Items);
        Assert.Equal(presentation.Id, item.PresentationId);
        Assert.Equal("Caja", item.PresentationName);
        Assert.Equal(2m, item.PackageQuantity);
        Assert.Equal(24, item.UnitsPerPackage);
        Assert.Equal(48m, item.RequestedQuantity);
    }

    [Fact]
    public async Task CreatePurchaseOrderAsync_Uses_Product_Presentation_Base_Factor()
    {
        var product = new Product("PEP-ORDER", null, "Pepperoni", null, Guid.NewGuid(), "Marca", "g", 0, 0, 0, 500, false);
        product.SetBaseUnit(Guid.NewGuid(), "g");
        var supplier = new Supplier("Proveedor", "1234567", null, null, null, null, null);
        var placeholder = new Purchase("OLD", supplier.Id, DateTimeOffset.UtcNow, "tester", [new PurchaseItem(product.Id, 1, 1)]);
        var presentation = new ProductPurchasePresentation(product.Id, "Bolsa 2.5 kg", 2500m);
        var repository = new FakePurchaseRepository(product, supplier, placeholder);
        repository.ProductPresentations.Add(presentation);
        var service = new PurchaseService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        await service.CreatePurchaseOrderAsync(new CreatePurchaseOrderRequest(supplier.Id,
            [new PurchaseOrderItemRequest(product.Id, 2m, presentation.Id)]));

        var item = Assert.Single(repository.Order!.Items);
        Assert.Null(item.PresentationId);
        Assert.Equal(presentation.Id, item.ProductPresentationId);
        Assert.Equal(2500m, item.BaseQuantityPerPackage);
        Assert.Equal(5000m, item.RequestedQuantity);
        Assert.Equal("g", item.BaseUnitCode);
    }

    [Fact]
    public async Task UpdatePurchaseOrderAsync_Preserves_Existing_Line_And_Submits_Draft()
    {
        var product = new Product("P-ORD-EDIT", null, "Cuadernos", null, Guid.NewGuid(), "Marca", "Unidad", 5m, 5m, 9m, 2m, true);
        var supplier = new Supplier("Proveedor pedidos", "8765432", null, null, null, null, null);
        var placeholder = new Purchase("C-PLACEHOLDER", supplier.Id, DateTimeOffset.UtcNow, "tester", [new PurchaseItem(product.Id, 1m, 5m)]);
        var order = new PurchaseOrder("PC-000001", supplier.Id, DateTimeOffset.UtcNow, "tester",
            [new PurchaseOrderItem(product.Id, 2m)]);
        var originalItemId = order.Items.Single().Id;
        var repository = new FakePurchaseRepository(product, supplier, placeholder) { Order = order };
        var service = new PurchaseService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        await service.UpdatePurchaseOrderAsync(order.Id,
            new UpdatePurchaseOrderRequest(supplier.Id, [new PurchaseOrderItemRequest(product.Id, 5m)], null, true));

        var updatedItem = Assert.Single(order.Items);
        Assert.Equal(originalItemId, updatedItem.Id);
        Assert.Equal(5m, updatedItem.RequestedQuantity);
        Assert.Equal(PurchaseOrderStatus.Requested, order.Status);
    }

    [Fact]
    public async Task CreatePurchaseOrderAsync_Rejects_Duplicate_Products()
    {
        var product = new Product("P-ORD-1", null, "Cuadernos", null, Guid.NewGuid(), "Marca", "Unidad", 5m, 5m, 9m, 2m, true);
        var supplier = new Supplier("Proveedor pedidos", "8765432", null, null, null, null, null);
        var placeholder = new Purchase("C-PLACEHOLDER", supplier.Id, DateTimeOffset.UtcNow, "tester", [new PurchaseItem(product.Id, 1m, 5m)]);
        var repository = new FakePurchaseRepository(product, supplier, placeholder);
        var service = new PurchaseService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        var exception = await Assert.ThrowsAsync<DomainRuleException>(() => service.CreatePurchaseOrderAsync(
            new CreatePurchaseOrderRequest(supplier.Id,
            [
                new PurchaseOrderItemRequest(product.Id, 2m),
                new PurchaseOrderItemRequest(product.Id, 3m)
            ])));

        Assert.Equal("El producto ya forma parte de la orden.", exception.Message);
        Assert.Null(repository.Order);
    }

    [Fact]
    public async Task Product_Presentation_Receives_Grams_And_Preserves_Factor_And_Unit_Cost()
    {
        var product = new Product("PEP-001", null, "Pepperoni", null, Guid.NewGuid(), "Sin marca", "g", 0, 0, 0, 500, false);
        product.SetBaseUnit(Guid.NewGuid(), "g");
        var supplier = new Supplier("Proveedor", "123", null, null, null, null, null);
        var placeholder = new Purchase("OLD", supplier.Id, DateTimeOffset.UtcNow, "tester", [new PurchaseItem(product.Id, 1, 1)]);
        var repository = new FakePurchaseRepository(product, supplier, placeholder);
        var presentation = new ProductPurchasePresentation(product.Id, "Bolsa 2.5 kg", 2500);
        repository.ProductPresentations.Add(presentation);
        var service = new PurchaseService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());
        await service.CreateDirectPurchaseAsync(new CreateDirectPurchaseRequest(supplier.Id, DateTimeOffset.UtcNow,
            "FACT-2500", "123", PurchaseTermsType.Cash, null, null,
            [new DirectPurchaseItemRequest(product.Id, 2, 999, 10, SupplierPackageType.Package,
                "Pepperoni", null, PresentationId: presentation.Id)]));
        var item = Assert.Single(repository.CreatedPurchase!.Items);
        Assert.Equal(5000, item.Quantity);
        Assert.Equal(2500, item.BaseQuantityPerPackage);
        Assert.Equal("g", item.BaseUnitCode);
        Assert.Equal(0.004m, item.UnitCost);
        Assert.Equal(0.004m, product.LastCost);
        Assert.Equal(5000, repository.Stock!.CurrentStock);
        presentation.Update("Bolsa nueva", 1000);
        Assert.Equal(2500, item.BaseQuantityPerPackage);
    }

    private sealed class FakePurchaseRepository(Product product, Supplier supplier, Purchase purchase,
        PurchasePresentation? presentation = null) : IPurchaseRepository
    {
        public List<InventoryMovement> Movements { get; } = [];
        public InventoryStock? Stock { get; private set; }
        public PurchaseOrder? Order { get; set; }
        public Purchase? CreatedPurchase { get; private set; }
        public List<Purchase> CreatedPurchases { get; } = [];
        public SistemiPos.Domain.Finance.PayableAccount? Payable { get; private set; }
        public PurchaseReceipt? CreatedReceipt { get; private set; }
        public List<PurchaseReceipt> CreatedReceipts { get; } = [];

        public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default)
        {
            Movements.Add(movement);
            return Task.CompletedTask;
        }

        public Task AddPurchaseAsync(Purchase value, CancellationToken cancellationToken = default) { CreatedPurchase = value; CreatedPurchases.Add(value); return Task.CompletedTask; }

        public Task<Purchase?> GetPurchaseByOrderAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Purchase?>(CreatedPurchase?.PurchaseOrderId == purchaseOrderId ? CreatedPurchase : null);

        public Task<bool> InvoiceExistsForSupplierAsync(Guid supplierId, string invoiceNumber, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreatedPurchases.Append(purchase).Any(x => x.SupplierId == supplierId
                && string.Equals(x.InvoiceNumber, invoiceNumber.Trim(), StringComparison.Ordinal)));

        public Task<SistemiPos.Domain.Finance.PayableAccount?> GetPayableAccountAsync(Guid purchaseId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Payable?.PurchaseId == purchaseId ? Payable : null);

        public Task AddPayableAccountAsync(SistemiPos.Domain.Finance.PayableAccount account, CancellationToken cancellationToken = default)
        {
            Payable = account;
            return Task.CompletedTask;
        }

        public Task AddPurchaseReceiptAsync(PurchaseReceipt receipt, CancellationToken cancellationToken = default)
        {
            receipt.Purchase = CreatedPurchases.Single(x => x.Id == receipt.PurchaseId);
            if (Order?.Id == receipt.PurchaseOrderId) ((ICollection<PurchaseReceipt>)Order.Receipts).Add(receipt);
            CreatedReceipt = receipt;
            CreatedReceipts.Add(receipt);
            return Task.CompletedTask;
        }

        public Task AddStockAsync(InventoryStock stock, CancellationToken cancellationToken = default)
        {
            Stock = stock;
            return Task.CompletedTask;
        }

        public Task<string> GeneratePurchaseNumberAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult("C-000001");

        public Task<string> GeneratePurchaseOrderNumberAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult("PC-000001");

        public Task AddPurchaseOrderAsync(PurchaseOrder order, CancellationToken cancellationToken = default)
        {
            Order = order;
            return Task.CompletedTask;
        }

        public Task<PurchaseOrder?> GetPurchaseOrderAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (Order?.Id != id)
            {
                return Task.FromResult<PurchaseOrder?>(null);
            }

            Order.Supplier = supplier;
            foreach (var item in Order.Items.Where(x => x.ProductId == product.Id))
            {
                item.Product = product;
            }

            return Task.FromResult<PurchaseOrder?>(Order);
        }

        public Task<List<PurchaseOrder>> GetPurchaseOrdersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Order is null ? new List<PurchaseOrder>() : new List<PurchaseOrder> { Order });

        public List<ProductPurchasePresentation> ProductPresentations { get; } = [];
        public Task<ProductPurchasePresentation?> GetProductPurchasePresentationAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(ProductPresentations.FirstOrDefault(x => x.Id == id));
        public Task<Product?> GetProductAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Product?>(id == product.Id ? product : null);

        public Task<PurchasePresentation?> GetPurchasePresentationAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<PurchasePresentation?>(presentation?.Id == id ? presentation : null);

        public Task<Purchase?> GetPurchaseAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Purchase?>(CreatedPurchases.FirstOrDefault(x => x.Id == id) ?? (id == purchase.Id ? purchase : null));

        public Task<List<Purchase>> GetPurchasesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<Purchase> { purchase });

        public Task<List<Purchase>> GetPurchasesBySupplierAsync(Guid supplierId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<Purchase> { purchase });

        public Task<InventoryStock?> GetStockAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<InventoryStock?>(Stock);

        public Task<Supplier?> GetSupplierAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Supplier?>(id == supplier.Id ? supplier : null);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return Task.FromResult(1);
        }
    }

    private sealed class NoBranchAccess : IOperationContextAccessor
    {
        public Guid? CurrentBranchId => null;
        public Guid? CurrentCompanyId => null;
        public Guid? CurrentBusinessGroupId => null;
        public Guid? CurrentWarehouseId => null;
        public string? CurrentBranchName => null;
        public string? CurrentCompanyName => null;
        public Task<IReadOnlyList<SistemiPos.Application.Organizations.BranchDto>> GetAuthorizedBranchesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SistemiPos.Application.Organizations.BranchDto>>([]);
        public Task SetCurrentBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public Guid? UserId => Guid.NewGuid();
        public string UserName => "tester";
        public bool IsAuthenticated => true;
    }

    private sealed class FakeDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => new(2026, 8, 30, 13, 0, 0, TimeSpan.Zero);
    }
}
