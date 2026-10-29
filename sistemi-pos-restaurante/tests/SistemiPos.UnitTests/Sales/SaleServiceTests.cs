using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Sales;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Customers;
using SistemiPos.Domain.Finance;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Sales;
using SistemiPos.Domain.Audit;
using SistemiPos.Domain.Organizations;
using SistemiPos.Application.Organizations;
using SistemiPos.Application.Orders;
using SistemiPos.Domain.Orders;

namespace SistemiPos.UnitTests.Sales;

public sealed class SaleServiceTests
{
    [Theory]
    [InlineData(false, "POS-1", 0, false, true)]
    [InlineData(true, "POS-1", 0, false, true)]
    [InlineData(true, "OTHER", 0, false, false)]
    [InlineData(true, "POS-1", 1, false, true)]
    [InlineData(true, "POS-1", 2, false, false)]
    [InlineData(false, "POS-1", 0, true, false)]
    public async Task GetPosTerminalsAsync_Allows_Opening_And_Respects_Active_Session_And_Branch(
        bool hasOpenSession, string registerCode, int binding, bool otherBranch, bool expected)
    {
        var branch = Guid.NewGuid();
        var warehouse = new Warehouse(branch, "W", "Bodega");
        var terminal = new PosTerminal(branch, warehouse, "T", "POS-1");
        var session = new CashSession(registerCode, "tester", DateTimeOffset.UtcNow, 0, branchId: branch);
        if (binding != 0) session.BindTerminal(binding == 1 ? terminal.Id : Guid.NewGuid(), "America/Guatemala");
        var product = new Product("P", null, "Producto", null, Guid.NewGuid(), "Marca", "Unidad", 0, 0, 10, 0, false);
        var order = new RestaurantOrder(terminal, session.Id,
            [new RestaurantOrderLine(product.Id, product.Name, 1, 10, 10, 0, false, "{}", 1)]);
        var repository = new FakeSaleRepository(product, new InventoryStock(product.Id), session)
        {
            ActiveSession = hasOpenSession ? session : null
        };
        var service = new SaleService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider(),
            operationContext: new FakeOperationContext(otherBranch ? Guid.NewGuid() : branch),
            orderRepository: new FakeOrderRepository(order, terminal, warehouse));

        var terminals = await service.GetPosTerminalsAsync();

        if (expected) Assert.Equal(terminal.Id, Assert.Single(terminals).Id);
        else Assert.Empty(terminals);
    }

    [Fact]
    public async Task Prepared_Order_Checkout_Uses_History_And_Does_Not_Consume_Inventory_Again()
    {
        var branch = Guid.NewGuid(); var warehouse = new Warehouse(branch, "W", "Bodega");
        var terminal = new PosTerminal(branch, warehouse, "T", "POS-1");
        var product = new Product("P", null, "Nombre actual", null, Guid.NewGuid(), "Marca", "Unidad", 0, 0, 99, 0, true);
        product.SetArticleType(ArticleType.Preparado);
        var stock = new InventoryStock(product.Id); stock.ReplaceForSystem(5);
        var session = new CashSession("POS-1", "tester", DateTimeOffset.UtcNow, 100, branchId: branch);
        var line = new RestaurantOrderLine(product.Id, "Nombre histórico", 1, 20, 20, 12, false, "{}", 1);
        var order = new RestaurantOrder(terminal, session.Id, [line]);
        order.Apply(OrderEventType.Confirm, null, 0, "confirm", DateTimeOffset.UtcNow, "tester");
        order.Apply(OrderEventType.StartPreparation, line.Id, 1, "start", DateTimeOffset.UtcNow, "tester");
        order.Apply(OrderEventType.FinishPreparation, line.Id, 1, "finish", DateTimeOffset.UtcNow, "tester");
        var repository = new FakeSaleRepository(product, stock, session) { Warehouse = warehouse };
        var service = new SaleService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider(),
            orderRepository: new FakeOrderRepository(order, terminal, warehouse));
        var request = new CreateSaleRequest(session.Id, null, [new SaleItemRequest(product.Id, 1, 0, OrderLineId: line.Id)],
            new SalePaymentRequest(PaymentMethodType.Cash, 20, null, null, null, null, null, null, null, null, null, null, null, null),
            ClientOperationId: "ORDER-CHECKOUT", RestaurantOrderId: order.Id);
        var saleId = await service.CreateSaleAsync(request);
        Assert.Equal(saleId, await service.CreateSaleAsync(request));
        Assert.Single(repository.Sales); Assert.Empty(repository.InventoryMovements);
        Assert.Equal(5, stock.CurrentStock); Assert.Equal(1, line.SoldQuantity);
        var sale = repository.Sales.Single();
        Assert.Equal(20, sale.Total); Assert.Equal(order.Id, sale.RestaurantOrderId);
        Assert.Equal("Nombre histórico", sale.Items.Single().ProductName);
        Assert.Equal(12, sale.Items.Single().TaxPercent);
        Assert.Equal("{}", sale.Items.Single().RecipeJson);
        await Assert.ThrowsAsync<DomainRuleException>(() => service.CreateSaleAsync(request with { ClientOperationId = "SECOND-CHECKOUT" }));
    }

    [Fact]
    public async Task Mixed_Cash_Checkout_Records_Only_Applied_Amounts_In_Cash()
    {
        var product = new Product("P", null, "Producto", null, Guid.NewGuid(), "Marca", "Unidad", 0, 0, 89, 0, false);
        var stock = new InventoryStock(product.Id); stock.ReplaceForSystem(1);
        var session = new CashSession("POS-1", "tester", DateTimeOffset.UtcNow, 100);
        var repository = new FakeSaleRepository(product, stock, session);
        var service = new SaleService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());
        var first = new SalePaymentRequest(PaymentMethodType.Cash, 60, null, null, null, null, null, null, null, null, null, null, null, null, AppliedAmount: 50);
        var second = first with { ReceivedAmount = 39, AppliedAmount = 39 };
        await service.CreateSaleAsync(new CreateSaleRequest(session.Id, null, [new SaleItemRequest(product.Id, 1, 0)], first, Payments: [first, second]));
        Assert.Equal(PaymentMethodType.Mixed, repository.Sales.Single().Payment.PaymentMethod);
        Assert.Equal(89, session.Movements.Sum(x => x.Amount));
        Assert.Equal(189, session.CalculateExpectedCash());
        Assert.Single(repository.InventoryMovements);
    }

    [Fact]
    public async Task CreateSaleAsync_Rejects_CashSession_From_Another_Operational_Branch()
    {
        var product = new Product("LOCAL", null, "Producto local", null, Guid.NewGuid(), "Marca", "Unidad", 0, 0, 15, 0, false);
        var stock = new InventoryStock(product.Id);
        stock.ReplaceForSystem(5);
        var session = new CashSession("POS-1", "tester", DateTimeOffset.UtcNow, 100, branchId: Guid.NewGuid());
        var repository = new FakeSaleRepository(product, stock, session);
        var unitOfWork = new FakeUnitOfWork();
        var service = new SaleService(repository, unitOfWork, new FakeCurrentUserAccessor(), new FakeDateTimeProvider(),
            operationContext: new FakeOperationContext(Guid.NewGuid()));

        await Assert.ThrowsAsync<DomainRuleException>(() => service.CreateSaleAsync(new CreateSaleRequest(session.Id, null,
            [new SaleItemRequest(product.Id, 1, 0)],
            new SalePaymentRequest(PaymentMethodType.Cash, 15, null, null, null, null, null, null, null, null, null, null, null, null))));

        Assert.Empty(repository.Sales);
        Assert.Empty(repository.InventoryMovements);
        Assert.Equal(5, stock.CurrentStock);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateSaleAsync_Rejects_Missing_Or_Incompatible_Warehouse()
    {
        var product = new Product("LOCAL", null, "Producto local", null, Guid.NewGuid(), "Marca", "Unidad", 0, 0, 15, 0, false);
        var stock = new InventoryStock(product.Id);
        stock.ReplaceForSystem(5);
        var session = new CashSession("POS-1", "tester", DateTimeOffset.UtcNow, 100, branchId: Guid.NewGuid());
        var repository = new FakeSaleRepository(product, stock, session);
        var unitOfWork = new FakeUnitOfWork();
        var service = new SaleService(repository, unitOfWork, new FakeCurrentUserAccessor(), new FakeDateTimeProvider());
        var request = new CreateSaleRequest(session.Id, null, [new SaleItemRequest(product.Id, 1, 0)],
            new SalePaymentRequest(PaymentMethodType.Cash, 15, null, null, null, null, null, null, null, null, null, null, null, null));

        repository.Warehouse = null;
        await Assert.ThrowsAsync<DomainRuleException>(() => service.CreateSaleAsync(request));
        repository.Warehouse = new Warehouse(Guid.NewGuid(), "OTRA", "Otra sucursal");
        await Assert.ThrowsAsync<DomainRuleException>(() => service.CreateSaleAsync(request));
        Assert.Empty(repository.Sales);
        Assert.Empty(repository.InventoryMovements);
        Assert.Equal(5, stock.CurrentStock);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateSaleAsync_Uses_The_CashSession_Branch_Price()
    {
        var product = new Product("LOCAL", null, "Producto local", null, Guid.NewGuid(), "Marca", "Unidad", 0, 0, 15, 0, false);
        var branch = Guid.NewGuid();
        ((ICollection<ProductBranchSetting>)product.BranchSettings).Add(new ProductBranchSetting(product.Id, branch, true, true, 25, null));
        var stock = new InventoryStock(product.Id);
        stock.ReplaceForSystem(5);
        var session = new CashSession("POS-1", "tester", DateTimeOffset.UtcNow, 100, branchId: branch);
        var repository = new FakeSaleRepository(product, stock, session);
        var service = new SaleService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());
        await service.CreateSaleAsync(new CreateSaleRequest(session.Id, null,
            [new SaleItemRequest(product.Id, 2, 0)],
            new SalePaymentRequest(PaymentMethodType.Cash, 50, null, null, null, null, null, null, null, null, null, null, null, null)));
        Assert.Equal(50, repository.Sales.Single().Total);
        Assert.Equal(15, product.SalePrice);
    }

    [Fact]
    public async Task CreateSaleAsync_Rejects_Product_Disabled_For_CashSession_Branch()
    {
        var product = new Product("LOCAL", null, "Producto local", null, Guid.NewGuid(), "Marca", "Unidad", 0, 0, 15, 0, false);
        var branch = Guid.NewGuid();
        ((ICollection<ProductBranchSetting>)product.BranchSettings).Add(new ProductBranchSetting(product.Id, branch, false, true, null, null));
        var stock = new InventoryStock(product.Id);
        stock.ReplaceForSystem(5);
        var session = new CashSession("POS-1", "tester", DateTimeOffset.UtcNow, 100, branchId: branch);
        var repository = new FakeSaleRepository(product, stock, session);
        var work = new FakeUnitOfWork();
        var service = new SaleService(repository, work, new FakeCurrentUserAccessor(), new FakeDateTimeProvider());
        await Assert.ThrowsAsync<DomainRuleException>(() => service.CreateSaleAsync(new CreateSaleRequest(session.Id, null,
            [new SaleItemRequest(product.Id, 2, 0)],
            new SalePaymentRequest(PaymentMethodType.Cash, 50, null, null, null, null, null, null, null, null, null, null, null, null))));
        Assert.Equal(5, stock.CurrentStock);
        Assert.Empty(repository.Sales);
        Assert.Equal(0, work.SaveChangesCalls);
    }
    [Fact]
    public async Task CreateSaleAsync_Rejects_Insufficient_Stock()
    {
        var categoryId = Guid.NewGuid();
        var product = new Product("P-001", null, "Café", null, categoryId, "Marca", "Unidad", 10m, 8m, 15m, 2m, true);
        var stock = new InventoryStock(product.Id);
        stock.ReplaceForSystem(1m);
        var session = new CashSession("POS-1", "tester", new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero), 100m);

        var repository = new FakeSaleRepository(product, stock, session);
        var service = new SaleService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.CreateSaleAsync(new CreateSaleRequest(
                session.Id,
                null,
                [new SaleItemRequest(product.Id, 2m, 0m)],
                new SalePaymentRequest(PaymentMethodType.Cash, 30m, null, null, null, null, null, null, null, null, null, null, null, null))));
    }

    [Fact]
    public async Task CreateSaleAsync_With_Cash_Updates_Stock_And_Cash_Movement()
    {
        var categoryId = Guid.NewGuid();
        var product = new Product("P-001", null, "Café", null, categoryId, "Marca", "Unidad", 10m, 8m, 15m, 2m, true);
        var stock = new InventoryStock(product.Id);
        stock.ReplaceForSystem(5m);
        var session = new CashSession("POS-1", "tester", new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero), 100m);
        var customer = new Customer("Cliente Uno", "CF", null, null, null);

        var repository = new FakeSaleRepository(product, stock, session, customer);
        var unitOfWork = new FakeUnitOfWork();
        var service = new SaleService(repository, unitOfWork, new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        var saleId = await service.CreateSaleAsync(new CreateSaleRequest(
            session.Id,
            customer.Id,
            [new SaleItemRequest(product.Id, 2m, 0m)],
            new SalePaymentRequest(PaymentMethodType.Cash, 30m, null, null, null, null, null, null, null, null, null, null, null, null)));

        Assert.NotEqual(Guid.Empty, saleId);
        Assert.Equal(3m, stock.CurrentStock);
        Assert.Single(repository.Sales);
        Assert.Single(repository.InventoryMovements);
        Assert.Single(session.Movements);
        Assert.Equal(CashMovementType.SaleCash, session.Movements.First().Type);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateSaleAsync_With_Credit_Creates_Receivable_And_Updates_Inventory()
    {
        var product = new Product("P-CR-1", null, "Producto crédito", null, Guid.NewGuid(), "Marca", "Unidad", 8m, 8m, 20m, 1m, true);
        var stock = new InventoryStock(product.Id); stock.ReplaceForSystem(4m);
        var session = new CashSession("POS-1", "tester", new DateTimeOffset(2026, 9, 2, 10, 0, 0, TimeSpan.Zero), 100m);
        var customer = new Customer("Cliente Crédito", "1234567", null, null, null, creditDays: 45, graceDays: 7);
        var repository = new FakeSaleRepository(product, stock, session, customer);
        var service = new SaleService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        await service.CreateSaleAsync(new CreateSaleRequest(session.Id, customer.Id, [new SaleItemRequest(product.Id, 2m, 0m)],
            new SalePaymentRequest(PaymentMethodType.Credit, null, null, null, null, null, null, null, null, null, null, null, null, null),
            CreditDueDateUtc: new DateTimeOffset(2026, 10, 2, 13, 0, 0, TimeSpan.Zero)));

        Assert.Equal(2m, stock.CurrentStock);
        var receivable = Assert.Single(repository.Receivables);
        Assert.Equal(40m, receivable.Total);
        Assert.Equal(45, receivable.CreditDays);
        Assert.Equal(7, receivable.GraceDays);
        Assert.Equal(new DateTimeOffset(2026, 10, 14, 13, 0, 0, TimeSpan.Zero), receivable.DueDateUtc);
        Assert.Empty(session.Movements);
    }

    [Fact]
    public async Task CreateSaleAsync_ExactPrice_Allows_Special_And_Base_Lines_In_One_Sale()
    {
        var product = new Product("P-001", null, "Producto", null, Guid.NewGuid(), "Marca", "Unidad", 1m, 1m, 1.25m, 2m, true);
        product.ConfigureMultiPricing(true, MultiPriceMode.ExactQuantity);
        product.SynchronizePriceRules([new(null, 10m, 10m, true)]);
        var stock = new InventoryStock(product.Id);
        stock.ReplaceForSystem(20m);
        var session = new CashSession("POS-1", "tester", new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero), 100m);
        var repository = new FakeSaleRepository(product, stock, session);

        await new SaleService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider())
            .CreateSaleAsync(new CreateSaleRequest(
                session.Id,
                null,
                [new SaleItemRequest(product.Id, 10m, 0m), new SaleItemRequest(product.Id, 5m, 0m)],
                new SalePaymentRequest(PaymentMethodType.Cash, 20m, null, null, null, null, null, null, null, null, null, null, null, null)));

        var sale = Assert.Single(repository.Sales);
        Assert.Equal(16.25m, sale.Total);
        Assert.Equal(10m, sale.Items.First().GrossAmount);
        Assert.Equal(6.25m, sale.Items.Last().GrossAmount);
        Assert.Equal(5m, stock.CurrentStock);
    }

    [Fact]
    public async Task CreateSaleAsync_With_New_Nit_Persists_And_Assigns_Customer_In_Same_Operation()
    {
        var product = new Product("P-001", null, "Café", null, Guid.NewGuid(), "Marca", "Unidad", 10m, 8m, 15m, 2m, true);
        var stock = new InventoryStock(product.Id);
        stock.ReplaceForSystem(5m);
        var session = new CashSession("POS-1", "tester", new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero), 100m);
        var repository = new FakeSaleRepository(product, stock, session);
        var unitOfWork = new FakeUnitOfWork();
        var service = new SaleService(repository, unitOfWork, new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        await service.CreateSaleAsync(new CreateSaleRequest(
            session.Id,
            null,
            [new SaleItemRequest(product.Id, 1m, 0m)],
            new SalePaymentRequest(PaymentMethodType.Cash, 20m, null, null, null, null, null, null, null, null, null, null, null, null),
            CustomerNit: "1234-5",
            CustomerName: "Cliente consultado"));

        var customer = Assert.Single(repository.Customers);
        Assert.Equal("12345", customer.NormalizedNit);
        Assert.Equal(customer.Id, Assert.Single(repository.Sales).CustomerId);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateSaleAsync_Requires_Authorization_When_Discount_Exceeds_User_Limit()
    {
        var categoryId = Guid.NewGuid();
        var product = new Product("P-001", null, "Café", null, categoryId, "Marca", "Unidad", 10m, 8m, 15m, 2m, true);
        var stock = new InventoryStock(product.Id);
        stock.ReplaceForSystem(5m);
        var session = new CashSession("POS-1", "tester", new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero), 100m);

        var repository = new FakeSaleRepository(product, stock, session);
        repository.ConfigureDiscounts(true, 5m);
        repository.DiscountPermissionUsers.Add("tester");
        repository.DiscountPermissionUsers.Add("manager");
        repository.DiscountLimits["tester"] = 5m;
        repository.DiscountLimits["manager"] = 15m;
        var approval = new DiscountApprovalRequest(session.Id, "tester", new DateTimeOffset(2026, 8, 30, 12, 55, 0, TimeSpan.Zero),
            [new DiscountApprovalItem(product.Id, product.DisplayName, 1m, 15m, SaleDiscountType.Percentage, 10m, 15m, 1.50m, "Autorización manual")]);
        approval.Approve("manager", new DateTimeOffset(2026, 8, 30, 12, 56, 0, TimeSpan.Zero), null);
        repository.DiscountApprovals.Add(approval);

        var service = new SaleService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        var saleId = await service.CreateSaleAsync(new CreateSaleRequest(
            session.Id,
            null,
            [new SaleItemRequest(product.Id, 1m, 10m)],
            new SalePaymentRequest(PaymentMethodType.Cash, 20m, null, null, null, null, null, null, null, null, null, null, null, null),
            "Autorización manual",
            "manager",
            DiscountApprovalRequestId: approval.Id));

        var sale = Assert.Single(repository.Sales);
        Assert.Equal(sale.Id, saleId);
        Assert.Equal("tester", sale.DiscountAppliedByUserName);
        Assert.Equal("manager", sale.DiscountAuthorizedByUserName);
        Assert.Equal("Autorización manual", sale.DiscountReason);
    }

    [Fact]
    public async Task CreateSaleAsync_Requires_Per_Line_Authorization_For_Pos_Discount()
    {
        var product = new Product("P-001", null, "Café", null, Guid.NewGuid(), "Marca", "Unidad", 10m, 8m, 15m, 2m, true);
        var stock = new InventoryStock(product.Id); stock.ReplaceForSystem(5m);
        var session = new CashSession("POS-1", "tester", new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero), 100m);
        var repository = new FakeSaleRepository(product, stock, session);
        repository.ConfigureDiscounts(true, 10m);
        repository.DiscountPermissionUsers.Add("tester");
        var service = new SaleService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        var exception = await Assert.ThrowsAsync<DomainRuleException>(() => service.CreateSaleAsync(new CreateSaleRequest(
            session.Id,
            null,
            [new SaleItemRequest(product.Id, 1m, 10m, SaleDiscountType.Percentage, 10m)],
            new SalePaymentRequest(PaymentMethodType.Cash, 20m, null, null, null, null, null, null, null, null, null, null, null, null),
            RequirePerLineDiscountAuthorization: true)));

        Assert.Contains("requieren aprobación", exception.Message);
        Assert.Empty(repository.Sales);
    }

    [Fact]
    public async Task CreateSaleAsync_Applies_Fixed_Line_Discount_And_Records_Audit()
    {
        var product = new Product("P-001", null, "Café", null, Guid.NewGuid(), "Marca", "Unidad", 10m, 8m, 15m, 2m, true);
        var stock = new InventoryStock(product.Id); stock.ReplaceForSystem(5m);
        var session = new CashSession("POS-1", "tester", new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero), 100m);
        var repository = new FakeSaleRepository(product, stock, session);
        repository.ConfigureDiscounts(true, 10m);
        repository.DiscountPermissionUsers.UnionWith(["tester", "manager"]);
        var authorizedAt = new DateTimeOffset(2026, 8, 30, 12, 55, 0, TimeSpan.Zero);
        var approval = new DiscountApprovalRequest(session.Id, "tester", authorizedAt,
            [new DiscountApprovalItem(product.Id, product.DisplayName, 1m, 15m, SaleDiscountType.FixedAmount, 5m, 15m, 5m, null)]);
        approval.Approve("manager", authorizedAt, null);
        repository.DiscountApprovals.Add(approval);
        var service = new SaleService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        await service.CreateSaleAsync(new CreateSaleRequest(
            session.Id,
            null,
            [new SaleItemRequest(product.Id, 1m, 0m, SaleDiscountType.FixedAmount, 5m, "manager", authorizedAt)],
            new SalePaymentRequest(PaymentMethodType.Cash, 20m, null, null, null, null, null, null, null, null, null, null, null, null),
            RequirePerLineDiscountAuthorization: true,
            DiscountApprovalRequestId: approval.Id));

        var sale = Assert.Single(repository.Sales);
        var item = Assert.Single(sale.Items);
        Assert.Equal(5m, item.DiscountAmount);
        Assert.Equal(10m, sale.Total);
        var audit = Assert.Single(repository.FinancialAudits);
        Assert.Equal("tester", audit.UserName);
        Assert.Equal("manager", audit.AuthorizedByUserName);
        Assert.Equal(5m, audit.Amount);
        Assert.Contains("Monto fijo", audit.Comment);
        Assert.Contains("Café", audit.Comment);
    }

    [Fact]
    public async Task CreateSaleAsync_Rejects_Pos_Payment_When_Not_Approved()
    {
        var categoryId = Guid.NewGuid();
        var product = new Product("P-001", null, "Café", null, categoryId, "Marca", "Unidad", 10m, 8m, 15m, 2m, true);
        var stock = new InventoryStock(product.Id);
        stock.ReplaceForSystem(5m);
        var session = new CashSession("POS-1", "tester", new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero), 100m);

        var repository = new FakeSaleRepository(product, stock, session);
        var service = new SaleService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.CreateSaleAsync(new CreateSaleRequest(
                session.Id,
                null,
                [new SaleItemRequest(product.Id, 1m, 0m)],
                new SalePaymentRequest(
                    PaymentMethodType.Pos,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    CardType.Debit,
                    "1234",
                    "AUTH-1",
                    "VOUCH-1",
                    15m,
                    CardPaymentStatus.Pending))));
    }

    [Fact]
    public async Task CreateSaleAsync_Rejects_Transfer_When_Disabled_In_Settings()
    {
        var product = new Product("P-TR-1", null, "Producto", null, Guid.NewGuid(), "Marca", "Unidad", 10m, 8m, 15m, 2m, true);
        var stock = new InventoryStock(product.Id);
        stock.ReplaceForSystem(5m);
        var session = new CashSession("POS-1", "tester", new DateTimeOffset(2026, 9, 11, 10, 0, 0, TimeSpan.Zero), 100m);
        var repository = new FakeSaleRepository(product, stock, session);
        repository.ConfigurePaymentMethods(transferEnabled: false, posEnabled: true);
        var service = new SaleService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        var exception = await Assert.ThrowsAsync<DomainRuleException>(() => service.CreateSaleAsync(new CreateSaleRequest(
            session.Id,
            null,
            [new SaleItemRequest(product.Id, 1m, 0m)],
            new SalePaymentRequest(PaymentMethodType.Transfer, null, "Banco", null, "REF-1", 15m,
                new DateTimeOffset(2026, 9, 11, 10, 0, 0, TimeSpan.Zero), TransferPaymentStatus.Verified,
                null, null, null, null, null, null))));

        Assert.Equal("La forma de pago por transferencia está deshabilitada.", exception.Message);
        Assert.Empty(repository.Sales);
    }

    [Fact]
    public async Task CreateSaleAsync_Rejects_Credit_When_Disabled_In_Settings()
    {
        var product = new Product("P-CR-1", null, "Producto", null, Guid.NewGuid(), "Marca", "Unidad", 10m, 8m, 15m, 2m, true);
        var stock = new InventoryStock(product.Id);
        stock.ReplaceForSystem(5m);
        var session = new CashSession("POS-1", "tester", new DateTimeOffset(2026, 9, 11, 10, 0, 0, TimeSpan.Zero), 100m);
        var repository = new FakeSaleRepository(product, stock, session);
        repository.ConfigurePaymentMethods(transferEnabled: true, posEnabled: true, creditEnabled: false);
        var service = new SaleService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        var exception = await Assert.ThrowsAsync<DomainRuleException>(() => service.CreateSaleAsync(new CreateSaleRequest(
            session.Id,
            null,
            [new SaleItemRequest(product.Id, 1m, 0m)],
            new SalePaymentRequest(PaymentMethodType.Credit, null, null, null, null, null, null, null,
                null, null, null, null, null, null))));

        Assert.Equal("La forma de pago a crédito está deshabilitada.", exception.Message);
        Assert.Empty(repository.Sales);
    }

    [Fact]
    public async Task ReturnSaleAsync_Rejects_When_Accumulated_Return_Exceeds_Sold_Quantity()
    {
        var categoryId = Guid.NewGuid();
        var product = new Product("P-001", null, "Café", null, categoryId, "Marca", "Unidad", 10m, 8m, 15m, 2m, true);
        var stock = new InventoryStock(product.Id);
        stock.ReplaceForSystem(5m);
        var session = new CashSession("POS-1", "tester", new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero), 100m);
        var repository = new FakeSaleRepository(product, stock, session);

        var sale = new Sale(
            "V-000001",
            session.Id,
            null,
            new DateTimeOffset(2026, 8, 30, 11, 0, 0, TimeSpan.Zero),
            "tester",
            [new SaleItem(product.Id, product.Name, 2m, 15m, 0m, 12m, false)],
            SalePayment.CreateCash(30m, 30m, "tester"),
            "GTQ",
            "Quetzal",
            "Q",
            2,
            ".",
            ",",
            "Before");
        repository.Sales.Add(sale);
        repository.ReturnedQuantities[product.Id] = 1.5m;

        var service = new SaleService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.ReturnSaleAsync(
                sale.Id,
                new ReturnSaleRequest("Devolución parcial", [new ReturnSaleItemRequest(product.Id, 1m, true)])));
    }

    [Fact]
    public async Task RefundSaleAsync_Rejects_When_Amount_Exceeds_Pending_Total()
    {
        var categoryId = Guid.NewGuid();
        var product = new Product("P-001", null, "Café", null, categoryId, "Marca", "Unidad", 10m, 8m, 15m, 2m, true);
        var stock = new InventoryStock(product.Id);
        stock.ReplaceForSystem(5m);
        var session = new CashSession("POS-1", "tester", new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero), 100m);
        var repository = new FakeSaleRepository(product, stock, session);
        repository.RefundPermissionUsers.Add("supervisor");
        repository.RefundedTotal = 10m;

        var sale = new Sale(
            "V-000001",
            session.Id,
            null,
            new DateTimeOffset(2026, 8, 30, 11, 0, 0, TimeSpan.Zero),
            "tester",
            [new SaleItem(product.Id, product.Name, 1m, 15m, 0m, 12m, false)],
            SalePayment.CreateCash(15m, 15m, "tester"),
            "GTQ",
            "Quetzal",
            "Q",
            2,
            ".",
            ",",
            "Before");
        repository.Sales.Add(sale);

        var service = new SaleService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.RefundSaleAsync(
                sale.Id,
                new RefundSaleRequest(session.Id, 6m, RefundMethodType.Cash, "Reembolso", "supervisor", null)));
    }

    private sealed class FakeSaleRepository(Product product, InventoryStock stock, CashSession session, Customer? customer = null) : ISaleRepository
    {
        public CashSession? ActiveSession { get; init; }
        public Task<CashSession?> GetActiveCashSessionByUserAsync(string userName, CancellationToken cancellationToken = default) =>
            Task.FromResult(ActiveSession);
        public Task<bool> ExistsClientOperationIdAsync(string clientOperationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Sales.Any(x => x.ClientOperationId == clientOperationId));
        public Warehouse? Warehouse { get; set; } = session.BranchId is Guid branchId
            ? new Warehouse(branchId, "LOCAL", "Bodega local", isDefault: true) : null;
        public Task<Warehouse?> GetDefaultWarehouseForBranchAsync(Guid branchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Warehouse);
        public List<Sale> Sales { get; } = [];
        public List<Customer> Customers { get; } = customer is null ? [] : [customer];
        public List<InventoryMovement> InventoryMovements { get; } = [];
        public List<ReceivableAccount> Receivables { get; } = [];
        public List<FinancialAuditEntry> FinancialAudits { get; } = [];
        public List<DiscountApprovalRequest> DiscountApprovals { get; } = [];
        private readonly SystemSetting _setting = SystemSetting.CreateDefault();
        public HashSet<string> DiscountPermissionUsers { get; } = [];
        public HashSet<string> RefundPermissionUsers { get; } = [];
        public Dictionary<string, decimal> DiscountLimits { get; } = [];
        public Dictionary<Guid, decimal> ReturnedQuantities { get; } = [];
        public decimal RefundedTotal { get; set; }

        public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default)
        {
            InventoryMovements.Add(movement);
            return Task.CompletedTask;
        }

        public Task AddCashMovementAsync(CashMovement movement, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AddSaleAsync(Sale sale, CancellationToken cancellationToken = default)
        {
            Sales.Add(sale);
            return Task.CompletedTask;
        }

        public Task AddDiscountApprovalAsync(DiscountApprovalRequest request, CancellationToken cancellationToken = default)
        {
            DiscountApprovals.Add(request);
            return Task.CompletedTask;
        }

        public Task<DiscountApprovalRequest?> GetDiscountApprovalAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(DiscountApprovals.FirstOrDefault(x => x.Id == id));

        public Task<List<DiscountApprovalRequest>> GetPendingDiscountApprovalsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(DiscountApprovals.Where(x => x.Status == DiscountApprovalStatus.Pending).ToList());

        public Task AddCustomerAsync(Customer value, CancellationToken cancellationToken = default)
        {
            Customers.Add(value);
            return Task.CompletedTask;
        }

        public Task AddCancellationAsync(SaleCancellation cancellation, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AddReturnAsync(SaleReturn saleReturn, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AddRefundAsync(SaleRefund refund, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task AddFinancialAuditAsync(FinancialAuditEntry entry, CancellationToken cancellationToken = default)
        {
            FinancialAudits.Add(entry);
            return Task.CompletedTask;
        }
        public Task AddReceivableAccountAsync(ReceivableAccount account, CancellationToken cancellationToken = default) { Receivables.Add(account); return Task.CompletedTask; }

        public Task<BankAccount?> GetBankAccountAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<BankAccount?>(null);

        public Task<CashSession?> GetCashSessionAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<CashSession?>(id == session.Id ? session : null);

        public Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Customers.FirstOrDefault(x => x.Id == id));

        public Task<Customer?> GetActiveCustomerByNormalizedNitAsync(string normalizedNit, CancellationToken cancellationToken = default) =>
            Task.FromResult(Customers.FirstOrDefault(x => x.IsActive && x.NormalizedNit == normalizedNit));

        public Task<Product?> GetProductAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Product?>(id == product.Id ? product : null);

        public Task<Sale?> GetSaleAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Sales.FirstOrDefault(x => x.Id == id));

        public Task<List<Sale>> GetSalesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Sales.ToList());

        public Task<InventoryStock?> GetStockAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<InventoryStock?>(productId == product.Id ? stock : null);

        public Task<SystemSetting> GetSystemSettingAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_setting);

        public void ConfigureDiscounts(bool enabled, decimal defaultMaxDiscountPercent) =>
            _setting.Update(
                _setting.BusinessName,
                _setting.BusinessNit,
                _setting.BusinessAddress,
                _setting.BusinessPhone,
                _setting.BusinessLogoUrl,
                _setting.CurrencyCode,
                _setting.CurrencyName,
                _setting.CurrencySymbol,
                _setting.CurrencyDecimals,
                _setting.DecimalSeparator,
                _setting.ThousandsSeparator,
                _setting.CurrencySymbolPosition,
                _setting.TaxPercent,
                _setting.ShowTaxOnReceipt,
                _setting.ReceiptFooter,
                _setting.CashPaymentsEnabled,
                _setting.TransferPaymentsEnabled,
                _setting.PosPaymentsEnabled,
                enabled,
                defaultMaxDiscountPercent,
                _setting.NitLookupProvider);

        public void ConfigurePaymentMethods(bool transferEnabled, bool posEnabled, bool creditEnabled = true) =>
            _setting.ConfigurePaymentMethods(transferEnabled, posEnabled, creditEnabled);

        public Task<decimal?> GetUserDiscountLimitAsync(string userName, CancellationToken cancellationToken = default) =>
            Task.FromResult(DiscountLimits.TryGetValue(userName, out var limit) ? limit : (decimal?)0m);

        public Task<string> GenerateSaleNumberAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult("V-000001");

        public Task<string> GenerateCancellationNumberAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult("A-000001");

        public Task<string> GenerateReturnNumberAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult("D-000001");

        public Task<Dictionary<Guid, decimal>> GetReturnedQuantitiesByProductAsync(Guid saleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ReturnedQuantities.ToDictionary());

        public Task<decimal> GetRefundedTotalAsync(Guid saleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(RefundedTotal);

        public Task<bool> UserHasPermissionAsync(string userName, string permissionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(permissionCode switch
            {
                SistemiPos.Application.Security.PermissionCodes.DiscountsApply => DiscountPermissionUsers.Contains(userName),
                SistemiPos.Application.Security.PermissionCodes.DiscountsAuthorize => DiscountPermissionUsers.Contains(userName),
                SistemiPos.Application.Security.PermissionCodes.RefundsAuthorize => RefundPermissionUsers.Contains(userName),
                _ => false
            });
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

    private sealed class FakeOrderRepository(RestaurantOrder order, PosTerminal terminal, Warehouse warehouse) : IOrderRepository
    {
        public Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct) => action();
        public Task<RestaurantOrder?> GetOrderAsync(Guid id, CancellationToken ct) => Task.FromResult<RestaurantOrder?>(id == order.Id ? order : null);
        public Task<PosTerminal?> GetTerminalAsync(Guid id, CancellationToken ct) => Task.FromResult<PosTerminal?>(id == terminal.Id ? terminal : null);
        public Task<Warehouse?> GetWarehouseAsync(Guid id, CancellationToken ct) => Task.FromResult<Warehouse?>(id == warehouse.Id ? warehouse : null);
        public Task<List<PosTerminal>> GetTerminalsAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(id == terminal.BranchId ? new List<PosTerminal> { terminal } : []);
        public Task TrackNewDetailsAsync(RestaurantOrder value, CancellationToken ct) => Task.CompletedTask;
        public Task<List<RestaurantOrder>> GetOrdersAsync(Guid branch, CancellationToken ct) => Task.FromResult(new List<RestaurantOrder> { order });
        public Task AddTerminalAsync(PosTerminal value, CancellationToken ct) => throw new NotSupportedException();
        public Task AddOrderAsync(RestaurantOrder value, CancellationToken ct) => throw new NotSupportedException();
        public Task<Product?> GetProductAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<InventoryStock?> GetStockAsync(Guid id, Guid location, CancellationToken ct) => throw new NotSupportedException();
        public Task AddMovementAsync(InventoryMovement value, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public string UserName => "tester";
    }

    private sealed class FakeOperationContext(Guid branchId) : IOperationContextAccessor
    {
        public Guid? CurrentBranchId => branchId;
        public Guid? CurrentCompanyId => null;
        public Guid? CurrentBusinessGroupId => null;
        public Guid? CurrentWarehouseId => null;
        public string? CurrentBranchName => null;
        public string? CurrentCompanyName => null;
        public Task<IReadOnlyList<BranchDto>> GetAuthorizedBranchesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BranchDto>>([]);
        public Task SetCurrentBranchAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => new(2026, 8, 30, 13, 0, 0, TimeSpan.Zero);
    }
}
