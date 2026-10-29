using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Catalog;
using SistemiPos.Application.Orders;
using SistemiPos.Application.Organizations;
using SistemiPos.Application.Security;
using SistemiPos.Application.Cash;
using SistemiPos.Application.Sales;
using SistemiPos.Domain.Sales;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Orders;
using SistemiPos.Domain.Organizations;
using SistemiPos.Domain.Recipes;
using SistemiPos.Domain.Security;
using SistemiPos.Infrastructure.Orders;
using SistemiPos.Infrastructure.Persistence;
using SistemiPos.Infrastructure.Recipes;
using SistemiPos.Infrastructure.Sales;
using SistemiPos.Infrastructure.Security;
using SistemiPos.Infrastructure.Organizations;
using SistemiPos.Infrastructure.Cash;

namespace SistemiPos.IntegrationTests.Infrastructure;

public sealed class RestaurantOrderPersistenceTests
{
    [PostgresFact]
    public async Task Authorizer_Requires_Permission_And_Scope_For_The_Operational_Branch()
    {
        await WithDatabase(async options =>
        {
            await using var db = new AppDbContext(options);
            var seeded = await SeedAsync(db);
            var companyId = (await db.Branches.SingleAsync()).CompanyId;
            var other = new Branch(companyId, "OTRA", "Otra sucursal");
            var user = new AppUser { Id = Guid.NewGuid(), UserName = "supervisor", NormalizedUserName = "SUPERVISOR", SecurityStamp = Guid.NewGuid().ToString() };
            user.PasswordHash = new PasswordHasher<AppUser>().HashPassword(user, "Fase7!Test123");
            var role = new AppRole { Id = Guid.NewGuid(), Name = "Supervisor de prueba", NormalizedName = "SUPERVISOR DE PRUEBA" };
            var permission = new Permission { Code = PermissionCodes.CashDifferenceAuthorize, Module = "Caja", Action = "Autorizar diferencia", Description = "Prueba" };
            db.AddRange(other, user, role, permission, new IdentityUserRole<Guid> { UserId = user.Id, RoleId = role.Id },
                new RolePermission { RoleId = role.Id, PermissionId = permission.Id },
                new UserScopeAssignment(user.Id, UserScopeLevel.Branch, branchId: other.Id));
            await db.SaveChangesAsync();
            var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton(db);
            services.AddIdentityCore<AppUser>().AddRoles<AppRole>().AddEntityFrameworkStores<AppDbContext>();
            await using var provider = services.BuildServiceProvider();
            var authorization = new OperationAuthorizationService(provider.GetRequiredService<UserManager<AppUser>>(), db,
                new Context(seeded.Terminal.BranchId), new OrganizationRepository(db));
            Assert.False(await authorization.HasPermissionAsync(user.UserName, permission.Code));
            await Assert.ThrowsAsync<DomainRuleException>(() => authorization.ValidateCredentialsAsync(user.UserName, "Fase7!Test123", permission.Code));
            db.UserScopeAssignments.Add(new UserScopeAssignment(user.Id, UserScopeLevel.Branch, branchId: seeded.Terminal.BranchId));
            await db.SaveChangesAsync();
            Assert.True(await authorization.HasPermissionAsync(user.UserName, permission.Code));
            Assert.Equal(user.UserName, await authorization.ValidateCredentialsAsync(user.UserName, "Fase7!Test123", permission.Code));
        });
    }
    [PostgresFact]
    public async Task Revisions_And_Extra_Packaging_Persist_Once_And_Keep_Original_Lines()
    {
        await WithDatabase(async options =>
        {
            await using var db = new AppDbContext(options);
            var seeded = await SeedAsync(db);
            var service = Service(db, seeded.Terminal.BranchId);
            var id = await service.CreateOrderAsync(new(seeded.Terminal.Id, seeded.Session.Id, [new(seeded.Product.Id, 1)]));
            await service.ApplyAsync(id, new(OrderEventType.Confirm, null, 0, "confirm"));
            var originalLine = (await service.GetOrderAsync(id)).Lines.Single().Id;
            var revision = new ReviseOrderLineRequest(seeded.Product.Id, 2, "revision", "Cambio de cantidad");
            await service.ReviseLineAsync(id, originalLine, revision);
            await service.ReviseLineAsync(id, originalLine, revision);
            db.ChangeTracker.Clear();
            var revised = await service.GetOrderAsync(id);
            Assert.Equal(2, revised.Lines.Count);
            Assert.Equal(1, revised.Lines.Single(x => x.Id == originalLine).Cancelled);
            var revisedLine = revised.Lines.Single(x => x.Id != originalLine);
            Assert.Equal(2, revisedLine.Quantity);
            await service.ApplyAsync(id, new(OrderEventType.StartPreparation, revisedLine.Id, 1, "start"));
            await Assert.ThrowsAsync<DomainRuleException>(() => service.ReviseLineAsync(id, revisedLine.Id, revision with { OperationId = "too-late" }));
            var resale = new Product("BEBIDA", null, "Bebida", null, seeded.Product.CategoryId, "Marca", "Unidad", 0, 0, 5, 0, false);
            resale.SetArticleType(ArticleType.Reventa);
            var pack = new Product("CAJA", null, "Caja", null, seeded.Product.CategoryId, "Marca", "Unidad", 0, 0, 0, 0, false);
            pack.SetArticleType(ArticleType.Empaque);
            var resaleStock = new InventoryStock(resale.Id, seeded.Terminal.WarehouseId); resaleStock.ReplaceForSystem(1);
            var packStock = new InventoryStock(pack.Id, seeded.Terminal.WarehouseId); packStock.ReplaceForSystem(2);
            db.AddRange(resale, pack, resaleStock, packStock); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var resaleId = await service.CreateOrderAsync(new(seeded.Terminal.Id, seeded.Session.Id, [new(resale.Id, 1)]));
            await service.ApplyAsync(resaleId, new(OrderEventType.Confirm, null, 0, "confirm"));
            var lineId = (await service.GetOrderAsync(resaleId)).Lines.Single().Id;
            var dispatch = new OrderActionRequest(OrderEventType.Dispatch, lineId, 1, "dispatch", ExtraPackaging: [new(pack.Id, 1)]);
            await service.ApplyAsync(resaleId, dispatch); await service.ApplyAsync(resaleId, dispatch);
            await Assert.ThrowsAsync<DomainRuleException>(() => service.ApplyAsync(resaleId, dispatch with { ExtraPackaging = [new(pack.Id, 2)] }));
            db.ChangeTracker.Clear();
            Assert.Equal(0, (await db.InventoryStocks.SingleAsync(x => x.ProductId == resale.Id)).CurrentStock);
            Assert.Equal(1, (await db.InventoryStocks.SingleAsync(x => x.ProductId == pack.Id)).CurrentStock);
            Assert.Equal(3, await db.InventoryMovements.CountAsync());
            var entry = await db.Set<RestaurantOrderEvent>().SingleAsync(x => x.OrderId == resaleId && x.Type == OrderEventType.Dispatch);
            Assert.NotNull(entry.ExtraPackagingJson);
            Assert.Equal(2, await db.InventoryMovements.CountAsync(x => x.RestaurantOrderEventId == entry.Id));
        });
    }
    [PostgresFact]
    public async Task Offline_Sales_Validate_Signed_Context_Total_And_Idempotency()
    {
        await WithDatabase(async options =>
        {
            await using var db = new AppDbContext(options);
            var seeded = await SeedAsync(db);
            var product = new Product("REVENTA", null, "Reventa", null, seeded.Product.CategoryId, "Marca", "Unidad", 0, 0, 20, 0, false);
            var stock = new InventoryStock(product.Id, seeded.Terminal.WarehouseId); stock.ReplaceForSystem(2);
            db.AddRange(product, stock); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var protector = new OfflinePosProtector(new EphemeralDataProtectionProvider());
            var context = new Context(seeded.Terminal.BranchId);
            var sales = new SaleService(new SaleRepository(db, context), db, new User(), new Clock(),
                ProductPricingService.Instance, operationContext: context, orderRepository: new OrderRepository(db), offlineProtector: protector);
            var ticket = await sales.CreateOfflineTicketAsync(seeded.Session.Id, seeded.Terminal.Id);
            var cash = new SalePaymentRequest(PaymentMethodType.Cash, 20, null, null, null, null, null, null, null, null, null, null, null, null);
            var request = new CreateSaleRequest(seeded.Session.Id, null, [new(product.Id, 1, 0)], cash,
                ClientOperationId: "OFF-CONTEXT", LocalCreatedAtUtc: DateTimeOffset.UtcNow, OfflineTicket: ticket.Ticket, ExpectedTotal: 20);
            await Assert.ThrowsAsync<DomainRuleException>(() => sales.CreateSaleAsync(request with { OfflineTicket = "invalid" }));
            await Assert.ThrowsAsync<DomainRuleException>(() => sales.CreateSaleAsync(request with { ExpectedTotal = 21 }));
            var otherBranch = new Context(Guid.NewGuid());
            var isolated = new SaleService(new SaleRepository(db, otherBranch), db, new User(), new Clock(),
                operationContext: otherBranch, orderRepository: new OrderRepository(db), offlineProtector: protector);
            await Assert.ThrowsAsync<DomainRuleException>(() => isolated.CreateSaleAsync(request));
            var id = await sales.CreateSaleAsync(request);
            Assert.Equal(id, await sales.CreateSaleAsync(request));
            db.ChangeTracker.Clear();
            Assert.Equal(1, await db.Sales.CountAsync());
            Assert.Equal(1, (await db.InventoryStocks.SingleAsync(x => x.ProductId == product.Id)).CurrentStock);
            Assert.Single(await db.InventoryMovements.Where(x => x.ProductId == product.Id).ToListAsync());
            Assert.Single(await db.CashMovements.Where(x => x.Type == CashMovementType.SaleCash).ToListAsync());
        });
    }
    [PostgresFact]
    public async Task Prepared_Order_Checkout_Persists_Mixed_Payments_And_Refund_Without_Reconsuming()
    {
        await WithDatabase(async options =>
        {
            await using var db = new AppDbContext(options);
            var seeded = await SeedAsync(db);
            var orders = Service(db, seeded.Terminal.BranchId);
            var orderId = await orders.CreateOrderAsync(new(seeded.Terminal.Id, seeded.Session.Id, [new(seeded.Product.Id, 1)]));
            await orders.ApplyAsync(orderId, new(OrderEventType.Confirm, null, 0, "confirm"));
            var lineId = (await orders.GetOrderAsync(orderId)).Lines.Single().Id;
            await orders.ApplyAsync(orderId, new(OrderEventType.StartPreparation, lineId, 1, "start"));
            await orders.ApplyAsync(orderId, new(OrderEventType.FinishPreparation, lineId, 1, "finish"));
            // A later active till can collect the prepared order from the same branch/warehouse.
            var originalSession = await db.CashSessions.SingleAsync();
            originalSession.Close(DateTimeOffset.UtcNow, 0);
            await db.SaveChangesAsync();
            var checkoutSession = new CashSession("C2", "order-test", DateTimeOffset.UtcNow, 0, branchId: seeded.Terminal.BranchId);
            var warehouse = await db.Warehouses.SingleAsync();
            var checkoutTerminal = new PosTerminal(seeded.Terminal.BranchId, warehouse, "T2", "C2");
            db.AddRange(checkoutSession, checkoutTerminal); await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var context = new Context(seeded.Terminal.BranchId);
            var sales = new SaleService(new SaleRepository(db, context), db, new User(), new Clock(),
                ProductPricingService.Instance, operationContext: context, orderRepository: new OrderRepository(db));
            var cash = new SalePaymentRequest(PaymentMethodType.Cash, 10, null, null, null, null, null, null,
                null, null, null, null, null, null, AppliedAmount: 7, AccountPartNumber: 1);
            var other = cash with { ReceivedAmount = 13, AppliedAmount = 13, AccountPartNumber = 2 };
            var request = new CreateSaleRequest(checkoutSession.Id, null,
                [new(seeded.Product.Id, 1, 0, OrderLineId: lineId)], cash, ClientOperationId: "CHECKOUT-ONE",
                Payments: [cash, other], RestaurantOrderId: orderId, TerminalId: checkoutTerminal.Id, SplitAmounts: [7, 13]);
            var saleId = await sales.CreateSaleAsync(request);
            Assert.Equal(saleId, await sales.CreateSaleAsync(request));
            db.ChangeTracker.Clear();
            var sale = (await sales.GetSaleAsync(saleId))!;
            Assert.Equal(20, sale.Total); Assert.Equal(PaymentMethodType.Mixed, sale.Payment.PaymentMethod);
            Assert.Equal("T2", sale.TerminalCode);
            Assert.Equal(2, sale.Payments!.Count); Assert.Equal(2, sale.AccountParts!.Count);
            Assert.Single(await db.InventoryMovements.ToListAsync());
            Assert.Equal(0, (await db.InventoryStocks.SingleAsync()).CurrentStock);
            Assert.Equal(1, (await orders.GetOrderAsync(orderId)).Lines.Single().Sold);
            var paymentId = sale.Payments.Single(x => x.TotalAmount == 7).Id;
            var refundRequest = new RefundSaleRequest(checkoutSession.Id, 2, RefundMethodType.Cash, "Devolución financiera", "manager", null,
                PaymentId: paymentId, OperationId: "REFUND-ONE");
            await sales.RefundSaleAsync(saleId, refundRequest);
            await sales.RefundSaleAsync(saleId, refundRequest);
            db.ChangeTracker.Clear();
            Assert.Equal(paymentId, (await db.SaleRefunds.SingleAsync()).PaymentId);
            Assert.Equal(18, (await db.CashSessions.Include(x => x.Movements).SingleAsync(x => x.Id == checkoutSession.Id)).CalculateExpectedCash());
            Assert.Single(await db.InventoryMovements.ToListAsync());
            await Assert.ThrowsAsync<DomainRuleException>(() => sales.RefundSaleAsync(saleId,
                new(checkoutSession.Id, 6, RefundMethodType.Cash, "Exceso", "manager", null, PaymentId: paymentId)));
            await Assert.ThrowsAsync<DomainRuleException>(() => sales.ReturnSaleAsync(saleId,
                new ReturnSaleRequest("Preparado", [new(seeded.Product.Id, 1, true)])));
            var returnRequest = new ReturnSaleRequest("Devolución sin reposición de ingredientes", [new(seeded.Product.Id, 1, false)], OperationId: "RETURN-ONE");
            await sales.ReturnSaleAsync(saleId, returnRequest); await sales.ReturnSaleAsync(saleId, returnRequest);
            db.ChangeTracker.Clear();
            Assert.Single(await db.SaleReturns.ToListAsync());
            Assert.Single(await db.InventoryMovements.ToListAsync());
            var blind = new CashService(new CashRepository(db, context), db, new User(), new Clock(), new BlindAuthorization(), context);
            Assert.Empty((await blind.GetSessionAsync(checkoutSession.Id))!.Movements);
            await Assert.ThrowsAsync<DomainRuleException>(() => blind.GetCutAsync(checkoutSession.Id, false));
            var till = new CashService(new CashRepository(db, context), db, new User(), new Clock(), operationContext: context);
            Assert.Equal(18, (await till.GetCutAsync(checkoutSession.Id, false)).ExpectedCash);
            await till.CloseSessionAsync(checkoutSession.Id, new(18));
            var cut = await till.GetCutAsync(checkoutSession.Id, true);
            Assert.True(cut.IsFinal); Assert.Equal(18, cut.ExpectedCash); Assert.Equal(0, cut.Difference);
            await Assert.ThrowsAsync<DomainRuleException>(() => till.CloseSessionAsync(checkoutSession.Id, new(18)));
        });
    }
    [PostgresFact]
    public async Task Orders_Persist_Confirmed_Snapshots_Consume_Once_And_Roll_Back_Insufficient_Stock()
    {
        await WithDatabase(async options =>
        {
            await using var db = new AppDbContext(options);
            var (terminal, session, product, ingredient) = await SeedAsync(db);
            var service = Service(db, terminal.BranchId);
            var id = await service.CreateOrderAsync(new(terminal.Id, session.Id, [new(product.Id, 2)]));
            await service.ApplyAsync(id, new(OrderEventType.Confirm, null, 0, "confirm"));
            db.ChangeTracker.Clear();
            var confirmed = await service.GetOrderAsync(id);
            var line = confirmed.Lines.Single();
            Assert.Equal(20, line.UnitPrice);
            Assert.Single(await db.Set<RecipeVersion>().ToListAsync());
            var originalRecipe = await db.Recipes.Include(x => x.Ingredients).SingleAsync();
            var unit = originalRecipe.YieldUnitId;
            originalRecipe.Update(1, [new RecipeIngredient(ingredient.Id, unit, "unidad", 5, 5, "unidad")], null, true);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            await service.ApplyAsync(id, new(OrderEventType.StartPreparation, line.Id, 1, "start"));
            db.ChangeTracker.Clear();
            // Confirmation pinned one unit, even though the current recipe now needs five.
            Assert.Equal(0, (await db.InventoryStocks.SingleAsync()).CurrentStock);
            Assert.Single(await db.InventoryMovements.ToListAsync());
            await service.ApplyAsync(id, new(OrderEventType.StartPreparation, line.Id, 1, "start"));
            db.ChangeTracker.Clear();
            Assert.Single(await db.InventoryMovements.ToListAsync());
            await Assert.ThrowsAsync<DomainRuleException>(() => service.ApplyAsync(id,
                new(OrderEventType.StartPreparation, line.Id, 1, "start-no-stock")));
            var afterFailure = await service.GetOrderAsync(id);
            Assert.Equal(1, afterFailure.Lines.Single().Preparing);
            Assert.DoesNotContain(afterFailure.Events, x => x.OperationId == "start-no-stock");
            await service.ApplyAsync(id, new(OrderEventType.Cancel, line.Id, 2, "cancel", "Cliente canceló"));
            db.ChangeTracker.Clear();
            Assert.Equal(0, (await db.InventoryStocks.SingleAsync()).CurrentStock);
            Assert.Single(await db.InventoryMovements.ToListAsync());
            Assert.Equal(1, (await service.GetOrderAsync(id)).Lines.Single().CancelledAfterPreparation);
        });
    }

    [PostgresFact]
    public async Task Concurrent_Orders_Cannot_Consume_The_Last_Unit_Twice()
    {
        await WithDatabase(async options =>
        {
            Guid terminalId, sessionId, branchId, productId;
            await using (var setup = new AppDbContext(options))
            {
                var seeded = await SeedAsync(setup);
                terminalId = seeded.Terminal.Id; sessionId = seeded.Session.Id;
                branchId = seeded.Terminal.BranchId; productId = seeded.Product.Id;
            }
            Guid firstId, secondId;
            await using (var setup = new AppDbContext(options))
            {
                var service = Service(setup, branchId);
                firstId = await service.CreateOrderAsync(new(terminalId, sessionId, [new(productId, 1)]));
                await service.ApplyAsync(firstId, new(OrderEventType.Confirm, null, 0, "confirm-first"));
                secondId = await service.CreateOrderAsync(new(terminalId, sessionId, [new(productId, 1)]));
                await service.ApplyAsync(secondId, new(OrderEventType.Confirm, null, 0, "confirm-second"));
            }
            async Task<bool> Prepare(Guid id)
            {
                await using var db = new AppDbContext(options);
                var service = Service(db, branchId);
                var order = await service.GetOrderAsync(id);
                try
                {
                    await service.ApplyAsync(id, new(OrderEventType.StartPreparation, order.Lines.Single().Id, 1, "prepare"));
                    return true;
                }
                catch (DomainRuleException) { return false; }
            }
            var results = await Task.WhenAll(Prepare(firstId), Prepare(secondId));
            Assert.Single(results, x => x);
            await using var check = new AppDbContext(options);
            Assert.Equal(0, (await check.InventoryStocks.SingleAsync()).CurrentStock);
            Assert.Single(await check.InventoryMovements.ToListAsync());
            Assert.Single(await check.Set<RestaurantOrderEvent>().Where(x => x.Type == OrderEventType.StartPreparation).ToListAsync());
        });
    }

    private static OrderService Service(AppDbContext db, Guid branchId) => new(new OrderRepository(db),
        new RecipeRepository(db), new SaleRepository(db), db, new User(), new Context(branchId), new Authorization(),
        new Clock(), ProductPricingService.Instance);

    private static async Task<(PosTerminal Terminal, CashSession Session, Product Product, Product Ingredient)> SeedAsync(AppDbContext db)
    {
        var group = new BusinessGroup("TEST", "Prueba");
        var company = new Company(group.Id, "TEST", "Prueba");
        var branch = new Branch(company.Id, "TEST", "Prueba");
        var warehouse = new Warehouse(branch.Id, "COCINA", "Cocina", isDefault: true);
        var terminal = new PosTerminal(branch.Id, warehouse, "T1", "C1");
        var session = new CashSession("C1", "order-test", DateTimeOffset.UtcNow, 0, branchId: branch.Id);
        var category = new Category("Prueba", null);
        var unit = new UnitOfMeasure("unidad", "Unidad", UnitDimension.Count, "unidad", 6);
        Product Product(string code, ArticleType type)
        {
            var value = new Product(code, null, code, null, category.Id, "Marca", "Unidad", 0, 0, 20, 0, false);
            value.SetArticleType(type); value.SetBaseUnit(unit.Id, unit.Name); return value;
        }
        var product = Product("PIZZA", ArticleType.Preparado);
        var ingredient = Product("INGREDIENTE", ArticleType.Insumo);
        var recipe = new Recipe(product.Id, unit.Id, unit.Code, 1,
            [new RecipeIngredient(ingredient.Id, unit.Id, unit.Code, 1, 1, unit.Code)], null);
        var stock = new InventoryStock(ingredient.Id, warehouse.Id);
        stock.ReplaceForSystem(1);
        db.AddRange(group, company, branch, warehouse, terminal, session, category, unit, product, ingredient, recipe, stock);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (terminal, session, product, ingredient);
    }

    private static async Task WithDatabase(Func<DbContextOptions<AppDbContext>, Task> test)
    {
        var name = "sistemi_order_test_" + Guid.NewGuid().ToString("N");
        var source = Environment.GetEnvironmentVariable("SISTEMI_TEST_CONNECTION");
        await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(source) { Database = "postgres", Pooling = false }.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin)) await create.ExecuteNonQueryAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(source)
            { Database = name, Pooling = false }.ConnectionString).Options;
        try
        {
            await using (var migrate = new AppDbContext(options)) await migrate.Database.MigrateAsync();
            await test(options);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class User : ICurrentUserAccessor
    {
        public string UserName => "order-test";
        public Guid? UserId => Guid.Parse("dc8a5192-a22a-47f2-aa2e-a4cb8c784ef1");
    }
    private sealed class Authorization : IOperationAuthorizationService
    {
        public Task<bool> HasPermissionAsync(string user, string permission, CancellationToken ct = default) => Task.FromResult(true);
        public Task<string> ValidateCredentialsAsync(string user, string password, string? permission = null, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class Clock : IDateTimeProvider { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
    private sealed class BlindAuthorization : IOperationAuthorizationService
    {
        public Task<bool> HasPermissionAsync(string user, string permission, CancellationToken ct = default) =>
            Task.FromResult(permission is PermissionCodes.CashCountHideExpected or PermissionCodes.CashClosingReport);
        public Task<string> ValidateCredentialsAsync(string user, string password, string? permission = null, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class Context(Guid branchId) : IOperationContextAccessor
    {
        public Guid? CurrentBranchId => branchId;
        public Guid? CurrentCompanyId => null;
        public Guid? CurrentBusinessGroupId => null;
        public Guid? CurrentWarehouseId => null;
        public string? CurrentBranchName => "Prueba";
        public string? CurrentCompanyName => null;
        public Task<IReadOnlyList<BranchDto>> GetAuthorizedBranchesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BranchDto>>([]);
        public Task SetCurrentBranchAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }
}
