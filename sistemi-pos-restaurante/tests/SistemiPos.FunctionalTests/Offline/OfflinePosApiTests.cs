using Microsoft.AspNetCore.Mvc;
using SistemiPos.Application.Cash;
using SistemiPos.Application.Catalog;
using SistemiPos.Application.Customers;
using SistemiPos.Application.Sales;
using SistemiPos.Application.Settings;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Sales;
using SistemiPos.Web.Controllers.Api;
using SistemiPos.Web.Models.Offline;

namespace SistemiPos.FunctionalTests.Offline;

public sealed class OfflinePosApiTests
{
    [Fact]
    public async Task Get_Bootstrap_Excludes_Products_Hidden_For_The_Branch()
    {
        var services = new OfflineTestServices();
        services.CatalogService.IsVisible = false;
        var result = await services.CreateController().GetBootstrapAsync(CancellationToken.None);
        var payload = Assert.IsType<OfflineBootstrapResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Empty(payload.Products);
    }
    [Fact]
    public async Task Get_Bootstrap_Returns_Offline_Data_Snapshot()
    {
        var services = new OfflineTestServices();
        var controller = services.CreateController();

        var result = await controller.GetBootstrapAsync(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<OfflineBootstrapResponse>(ok.Value);
        Assert.Single(payload.Products);
        Assert.Equal("P-001", payload.Products[0].InternalCode);
        Assert.True(payload.Products[0].MultiPriceEnabled);
        Assert.Equal("ExactQuantity", payload.Products[0].MultiPriceMode);
        Assert.Single(payload.Products[0].PriceRules!);
        Assert.Single(payload.Customers);
        Assert.Single(payload.CashSessions);
        Assert.Single(payload.BankAccounts);
        Assert.Equal("GTQ", payload.Settings.CurrencyCode);
    }

    [Fact]
    public async Task Post_Sync_Is_Idempotent_For_Repeated_ClientOperationId()
    {
        var services = new OfflineTestServices();
        var controller = services.CreateController();
        var request = services.CreateSyncRequest("OFF-001");

        var first = await controller.SyncAsync(request, CancellationToken.None);
        var second = await controller.SyncAsync(request, CancellationToken.None);

        var firstPayload = Assert.IsType<OfflineSyncBatchResponse>(Assert.IsType<OkObjectResult>(first.Result).Value);
        var secondPayload = Assert.IsType<OfflineSyncBatchResponse>(Assert.IsType<OkObjectResult>(second.Result).Value);

        Assert.Equal("Synced", firstPayload.Results[0].Status);
        Assert.Equal("Synced", secondPayload.Results[0].Status);
        Assert.Equal("V-000001", firstPayload.Results[0].SaleNumber);
        Assert.Equal("V-000001", secondPayload.Results[0].SaleNumber);
        Assert.Equal(1, services.SaleService.CreateCalls);
    }

    [Fact]
    public async Task Post_Sync_Returns_Conflict_When_Server_Rejects_Sale()
    {
        var services = new OfflineTestServices();
        services.SaleService.ThrowConflict = true;
        var controller = services.CreateController();

        var response = await controller.SyncAsync(services.CreateSyncRequest("OFF-ERR-001"), CancellationToken.None);

        var payload = Assert.IsType<OfflineSyncBatchResponse>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal("Conflict", payload.Results[0].Status);
        Assert.Equal("No hay existencia suficiente para confirmar la venta.", payload.Results[0].Error);
    }

    [Fact]
    public async Task Post_Sync_Forwards_New_Customer_Nit_And_Name()
    {
        var services = new OfflineTestServices();
        var sale = services.CreateSyncRequest("OFF-NIT-001").Sales[0] with
        {
            CustomerId = null,
            CustomerNit = "1234-5",
            CustomerName = "Cliente offline"
        };

        await services.CreateController().SyncAsync(new OfflineSyncBatchRequest([sale]), CancellationToken.None);

        Assert.Equal("1234-5", services.SaleService.LastRequest?.CustomerNit);
        Assert.Equal("Cliente offline", services.SaleService.LastRequest?.CustomerName);
    }

    [Fact]
    public async Task Post_Sync_Rejects_Discount_Without_Online_Approval()
    {
        var services = new OfflineTestServices();
        var sale = services.CreateSyncRequest("OFF-DISCOUNT-001").Sales[0] with
        {
            Items = [new OfflineSaleItemSyncRequest(services.ProductId, 1m, 10m)]
        };

        var response = await services.CreateController().SyncAsync(new OfflineSyncBatchRequest([sale]), CancellationToken.None);

        var payload = Assert.IsType<OfflineSyncBatchResponse>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal("Conflict", payload.Results[0].Status);
        Assert.Contains("aprobación", payload.Results[0].Error);
        Assert.Equal(0, services.SaleService.CreateCalls);
    }

    private sealed class OfflineTestServices
    {
        public Guid ProductId { get; } = Guid.NewGuid();
        public Guid ActiveCustomerId { get; } = Guid.NewGuid();
        public Guid ActiveSessionId { get; } = Guid.NewGuid();
        public Guid BankAccountId { get; } = Guid.NewGuid();
        public FakeCatalogService CatalogService { get; }
        public FakeCustomerService CustomerService { get; }
        public FakeCashService CashService { get; }
        public FakeSettingsService SettingsService { get; }
        public FakeSaleService SaleService { get; }

        public OfflineTestServices()
        {
            CatalogService = new FakeCatalogService(ProductId);
            CustomerService = new FakeCustomerService(ActiveCustomerId);
            CashService = new FakeCashService(ActiveSessionId);
            SettingsService = new FakeSettingsService(BankAccountId);
            SaleService = new FakeSaleService(ProductId, ActiveCustomerId, ActiveSessionId);
        }

        public OfflinePosController CreateController() =>
            new(CatalogService, CustomerService, CashService, SettingsService, SaleService);

        public OfflineSyncBatchRequest CreateSyncRequest(string clientOperationId) =>
            new(
                [
                    new OfflineSaleSyncRequest(
                        clientOperationId,
                        new DateTimeOffset(2026, 8, 30, 15, 0, 0, TimeSpan.Zero),
                        ActiveSessionId,
                        ActiveCustomerId,
                        null,
                        null,
                        PaymentMethodType.Cash,
                        20m,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        [new OfflineSaleItemSyncRequest(ProductId, 1m, 0m)])
                ]);
    }

    private sealed class FakeCatalogService(Guid productId) : ICatalogService
    {
        public bool IsVisible { get; set; } = true;
        public Task<Guid> CreateCategoryAsync(SaveCategoryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Guid> CreateProductAsync(SaveProductRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CategoryDto?> GetCategoryAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<CategoryDto?>(null);
        public Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CategoryDto>>([]);
        public Task<ProductDto?> GetProductAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<ProductDto?>(null);
        public Task UpdateCategoryAsync(Guid id, SaveCategoryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteProductAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateProductAsync(Guid id, SaveProductRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<ProductDto>> SearchProductsAsync(string? query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProductDto>>(
            [
                new ProductDto(
                    productId,
                    "P-001",
                    "7500001",
                    "Café",
                    null,
                    Guid.NewGuid(),
                    "Bebidas",
                    "Marca",
                    "Unidad",
                    8m,
                    7m,
                    15m,
                    2m,
                    true,
                    12m,
                    true,
                    null,
                    true,
                    MultiPriceMode.ExactQuantity,
                    [new ProductPriceRuleDto(Guid.NewGuid(), 10m, 100m, true)],
                    IsVisible: IsVisible)
            ]);
    }

    private sealed class FakeCustomerService(Guid customerId) : ICustomerService
    {
        public Task<Guid> CreateCustomerAsync(SaveCustomerRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CustomerDto?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<CustomerDto?>(new CustomerDto(customerId, "Cliente Uno", "CF", null, null, null, true));
        public Task<IReadOnlyList<CustomerDto>> GetCustomersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CustomerDto>>([new CustomerDto(customerId, "Cliente Uno", "CF", null, null, null, true)]);
        public Task<IReadOnlyList<CustomerHistoryDto>> GetHistoryAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CustomerHistoryDto>>([]);
        public Task UpdateCustomerAsync(Guid id, SaveCustomerRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeCashService(Guid sessionId) : ICashService
    {
        public Task CloseSessionAsync(Guid id, CloseCashSessionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CashSessionDto?> GetSessionAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<CashSessionDto?>(null);
        public Task<Guid> OpenSessionAsync(OpenCashSessionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RecordAdjustmentAsync(Guid id, RecordCashMovementRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RecordMovementAsync(Guid id, RecordCashMovementRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<CashSessionDto>> GetSessionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CashSessionDto>>(
            [
                new CashSessionDto(sessionId, "POS-1", "tester", new DateTimeOffset(2026, 8, 30, 14, 0, 0, TimeSpan.Zero), 100m, CashSessionStatus.Active, null, null, null, null, null, null, null, null, null, null, [])
            ]);
        public async Task<CashSessionDto?> GetCurrentSessionAsync(CancellationToken cancellationToken = default) =>
            (await GetSessionsAsync(cancellationToken)).Single();
    }

    private sealed class FakeSettingsService(Guid bankAccountId) : ISettingsService
    {
        public Task<Guid> CreateBankAccountAsync(SaveBankAccountRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BankAccountDto?> GetBankAccountAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<BankAccountDto?>(null);
        public Task<IReadOnlyList<BankAccountDto>> GetBankAccountsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BankAccountDto>>([new BankAccountDto(bankAccountId, "Banco Demo", "Cuenta Principal", "****1234", true)]);
        public Task<IReadOnlyList<RoleDiscountSettingDto>> GetRoleDiscountSettingsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RoleDiscountSettingDto>>([]);
        public Task<SystemSettingDto> GetSystemSettingAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SystemSettingDto(Guid.NewGuid(), "Mi negocio", "1234567", "Guatemala", "5555-5555", "https://example.com/logo.png", "GTQ", "Quetzal", "Q", 2, ".", ",", CurrencySymbolPosition.Before, 12m, true, "Gracias por su compra", true, true, true, true, true, 10m));
        public Task<IReadOnlyList<UserDiscountSettingDto>> GetUserDiscountSettingsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UserDiscountSettingDto>>([]);
        public Task SaveRoleDiscountSettingAsync(SaveRoleDiscountSettingRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveUserDiscountSettingAsync(SaveUserDiscountSettingRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateBankAccountAsync(Guid id, SaveBankAccountRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateSystemSettingAsync(UpdateSystemSettingRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeSaleService(Guid productId, Guid customerId, Guid sessionId) : ISaleService
    {
        public Task<OfflinePosTicket> CreateOfflineTicketAsync(Guid id, Guid? terminalId = null, CancellationToken ct = default) =>
            Task.FromResult(new OfflinePosTicket(new OfflinePosContext(Guid.NewGuid(), "tester", Guid.NewGuid(), Guid.NewGuid(),
                terminalId ?? Guid.NewGuid(), id, DateTimeOffset.UtcNow, "GTQ"), "test-ticket"));
        public Task<IReadOnlyDictionary<Guid, decimal>> GetOfflineStocksAsync(string ticket, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, decimal>>(new Dictionary<Guid, decimal> { [productId] = 100 });
        private readonly Dictionary<string, Guid> _operations = [];
        private readonly Dictionary<Guid, SaleDto> _sales = [];

        public bool ThrowConflict { get; set; }
        public int CreateCalls { get; private set; }
        public CreateSaleRequest? LastRequest { get; private set; }

        public Task CancelSaleAsync(Guid saleId, CancelSaleRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RefundSaleAsync(Guid saleId, RefundSaleRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RejectTransferAsync(Guid saleId, string? observation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ReturnSaleAsync(Guid saleId, ReturnSaleRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task VerifyTransferAsync(Guid saleId, string? observation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Guid> RequestDiscountApprovalAsync(Guid cashSessionId, IReadOnlyList<SaleItemRequest> items, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DiscountApprovalDto?> GetDiscountApprovalAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<DiscountApprovalDto?>(null);
        public Task<IReadOnlyList<DiscountApprovalDto>> GetPendingDiscountApprovalsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DiscountApprovalDto>>([]);
        public Task DecideDiscountApprovalAsync(Guid id, bool approve, string? comment, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<SaleDto>> GetSalesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SaleDto>>(_sales.Values.ToList());

        public Task<SaleDto?> GetSaleAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_sales.TryGetValue(id, out var sale) ? sale : null);

        public Task<Guid> CreateSaleAsync(CreateSaleRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            if (ThrowConflict)
            {
                throw new DomainRuleException("No hay existencia suficiente para confirmar la venta.");
            }

            if (_operations.TryGetValue(request.ClientOperationId!, out var existingId))
            {
                return Task.FromResult(existingId);
            }

            CreateCalls++;
            var id = Guid.NewGuid();
            _operations[request.ClientOperationId!] = id;
            _sales[id] = new SaleDto(
                id,
                "V-000001",
                sessionId,
                customerId,
                "Cliente Uno",
                request.LocalCreatedAtUtc ?? new DateTimeOffset(2026, 8, 30, 15, 0, 0, TimeSpan.Zero),
                "tester",
                15m,
                0m,
                13.39m,
                1.61m,
                15m,
                null,
                null,
                null,
                null,
                null,
                "GTQ",
                "Quetzal",
                "Q",
                2,
                ".",
                ",",
                "Before",
                SaleStatus.Confirmed,
                new SalePaymentDto(PaymentMethodType.Cash, 15m, 20m, 5m, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null),
                [new SaleItemDto(productId, "Café", 1m, 15m, 0m, 0m, 13.39m, 12m, 1.61m, 15m, false)],
                CustomerNit: null,
                ClientOperationId: request.ClientOperationId,
                SynchronizedAtUtc: new DateTimeOffset(2026, 8, 30, 15, 5, 0, TimeSpan.Zero));

            return Task.FromResult(id);
        }
    }
}
