using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Catalog;
using SistemiPos.Application.Inventory;
using SistemiPos.Application.Organizations;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Organizations;

namespace SistemiPos.UnitTests.Inventory;

public sealed class InventoryTransferTests
{
    private sealed class FakeTransferRepository : IInventoryTransferRepository
    {
        public readonly Dictionary<Guid, InventoryTransfer> Transfers = new();
        public readonly Dictionary<(Guid ProductId, Guid WarehouseId), InventoryStock> Stocks = new();
        public readonly List<InventoryMovement> Movements = new();

        public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default)
        {
            Movements.Add(movement);
            return Task.CompletedTask;
        }

        public Task AddStockAsync(InventoryStock stock, CancellationToken cancellationToken = default)
        {
            if (stock.WarehouseId.HasValue)
            {
                Stocks[(stock.ProductId, stock.WarehouseId.Value)] = stock;
            }
            return Task.CompletedTask;
        }

        public Task AddTransferAsync(InventoryTransfer transfer, CancellationToken cancellationToken = default)
        {
            Transfers[transfer.Id] = transfer;
            return Task.CompletedTask;
        }

        public Task<string> GenerateTransferNumberAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult($"TR-{Transfers.Count + 1:000000}");
        }

        public Task<InventoryStock?> GetStockAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default)
        {
            Stocks.TryGetValue((productId, warehouseId), out var stock);
            return Task.FromResult(stock);
        }

        public Task<InventoryTransfer?> GetTransferByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Transfers.TryGetValue(id, out var transfer);
            return Task.FromResult(transfer);
        }

        public Task<InventoryTransfer?> GetTransferByNumberAsync(string number, CancellationToken cancellationToken = default)
        {
            var transfer = Transfers.Values.FirstOrDefault(x => x.Number == number);
            return Task.FromResult(transfer);
        }

        public Task<List<InventoryTransfer>> GetTransfersAsync(Guid? originWarehouseId = null, Guid? destinationWarehouseId = null, InventoryTransferStatus? status = null, CancellationToken cancellationToken = default)
        {
            var list = Transfers.Values.AsEnumerable();
            if (originWarehouseId.HasValue) list = list.Where(x => x.OriginWarehouseId == originWarehouseId.Value);
            if (destinationWarehouseId.HasValue) list = list.Where(x => x.DestinationWarehouseId == destinationWarehouseId.Value);
            if (status.HasValue) list = list.Where(x => x.Status == status.Value);
            return Task.FromResult(list.ToList());
        }
    }

    private sealed class FakeOrganizationRepository : IOrganizationRepository
    {
        public readonly Dictionary<Guid, Warehouse> Warehouses = new();

        public Task AddBranchAsync(Branch branch, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddBusinessGroupAsync(BusinessGroup group, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddCompanyAsync(Company company, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddWarehouseAsync(Warehouse warehouse, CancellationToken cancellationToken = default)
        {
            Warehouses[warehouse.Id] = warehouse;
            return Task.CompletedTask;
        }
        public Task<List<Branch>> GetAuthorizedBranchesForUserAsync(Guid userId, bool isSuperAdmin, CancellationToken cancellationToken = default) => Task.FromResult(new List<Branch>());
        public Task<Branch?> GetBranchByCodeAsync(Guid companyId, string code, CancellationToken cancellationToken = default) => Task.FromResult<Branch?>(null);
        public Task<Branch?> GetBranchByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Branch?>(null);
        public Task<List<Branch>> GetBranchesAsync(Guid? companyId = null, bool onlyActive = false, CancellationToken cancellationToken = default) => Task.FromResult(new List<Branch>());
        public Task<BusinessGroup?> GetBusinessGroupByCodeAsync(string code, CancellationToken cancellationToken = default) => Task.FromResult<BusinessGroup?>(null);
        public Task<BusinessGroup?> GetBusinessGroupByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<BusinessGroup?>(null);
        public Task<List<BusinessGroup>> GetBusinessGroupsAsync(bool onlyActive = false, CancellationToken cancellationToken = default) => Task.FromResult(new List<BusinessGroup>());
        public Task<List<Company>> GetCompaniesAsync(Guid? businessGroupId = null, bool onlyActive = false, CancellationToken cancellationToken = default) => Task.FromResult(new List<Company>());
        public Task<Company?> GetCompanyByCodeAsync(Guid businessGroupId, string code, CancellationToken cancellationToken = default) => Task.FromResult<Company?>(null);
        public Task<Company?> GetCompanyByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Company?>(null);
        public Task<Warehouse?> GetWarehouseByCodeAsync(Guid branchId, string code, CancellationToken cancellationToken = default) => Task.FromResult<Warehouse?>(null);
        public Task<Warehouse?> GetWarehouseByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Warehouses.TryGetValue(id, out var wh);
            return Task.FromResult(wh);
        }
        public Task<List<Warehouse>> GetWarehousesAsync(Guid? branchId = null, bool onlyActive = false, CancellationToken cancellationToken = default) => Task.FromResult(Warehouses.Values.ToList());
        public Task<List<UserScopeAssignment>> GetUserScopesAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(new List<UserScopeAssignment>());
        public Task SaveUserScopesAsync(Guid userId, IEnumerable<UserScopeAssignment> assignments, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeCatalogRepository : ICatalogRepository
    {
        public readonly Dictionary<Guid, Product> Products = new();

        public Task AddBrandAsync(Brand brand, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddCatalogChangeLogAsync(CatalogChangeLog log, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddCategoryAsync(Category category, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddInventoryStockAsync(InventoryStock stock, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddProductAsync(Product product, CancellationToken cancellationToken = default)
        {
            Products[product.Id] = product;
            return Task.CompletedTask;
        }
        public Task AddProductBranchSettingAsync(ProductBranchSetting setting, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddProductVariantOptionsAsync(IEnumerable<ProductVariantOption> options, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddPurchasePresentationAsync(ProductPurchasePresentation presentation, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddUnitConversionAsync(ProductUnitConversion conversion, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddUnitOfMeasureAsync(UnitOfMeasure unit, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddVariantOptionAsync(VariantOption option, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddVariantTypeAsync(VariantType variantType, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> ExistsBarcodeAsync(string barcode, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ExistsBrandByNameAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ExistsCategoryByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ExistsInternalCodeAsync(string internalCode, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<List<CatalogChangeLog>> GetCatalogChangeLogsAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult(new List<CatalogChangeLog>());
        public Task<List<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<Category>());
        public Task<Category?> GetCategoryAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Category?>(null);
        public Task<InventoryStock?> GetInventoryStockAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<InventoryStock?>(null);
        public Task<string> GetNextInternalCodeAsync(CancellationToken cancellationToken = default) => Task.FromResult("P-001");
        public Task<Product?> GetProductAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Products.TryGetValue(id, out var prod);
            return Task.FromResult(prod);
        }
        public Task<ProductBranchSetting?> GetProductBranchSettingAsync(Guid productId, Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult<ProductBranchSetting?>(null);
        public Task<List<ProductBranchSetting>> GetProductBranchSettingsAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult(new List<ProductBranchSetting>());
        public Task<List<string>> GetProductBrandsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<string>());
        public Task<List<Product>> GetProductsByNameAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(new List<Product>());
        public Task<List<ProductPurchasePresentation>> GetPurchasePresentationsAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult(new List<ProductPurchasePresentation>());
        public Task<List<ProductUnitConversion>> GetUnitConversionsAsync(Guid? productId, CancellationToken cancellationToken = default) => Task.FromResult(new List<ProductUnitConversion>());
        public Task<UnitOfMeasure?> GetUnitOfMeasureAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<UnitOfMeasure?>(null);
        public Task<UnitOfMeasure?> GetUnitOfMeasureByCodeAsync(string code, CancellationToken cancellationToken = default) => Task.FromResult<UnitOfMeasure?>(null);
        public Task<List<UnitOfMeasure>> GetUnitsOfMeasureAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<UnitOfMeasure>());
        public Task<VariantOption?> GetVariantOptionByValueAsync(string value, CancellationToken cancellationToken = default) => Task.FromResult<VariantOption?>(null);
        public Task<VariantType?> GetVariantTypeByNameAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult<VariantType?>(null);
        public Task<List<VariantType>> GetVariantTypesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<VariantType>());
        public Task<bool> HasProductUsageAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task RemoveUnusedProductAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<Product>> SearchProductsAsync(string? query, CancellationToken cancellationToken = default) => Task.FromResult(Products.Values.ToList());
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FakeUserAccessor : ICurrentUserAccessor
    {
        public string UserName => "test-admin";
        public Guid? UserId => Guid.NewGuid();
    }

    private sealed class FakeDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    [Fact]
    public async Task Transfer_EndToEnd_Draft_Dispatch_Receive_AssignedCost_And_Isolation()
    {
        // 1. Setup
        var transferRepo = new FakeTransferRepository();
        var orgRepo = new FakeOrganizationRepository();
        var catalogRepo = new FakeCatalogRepository();
        var uow = new FakeUnitOfWork();
        var userAccessor = new FakeUserAccessor();
        var dateTimeProvider = new FakeDateTimeProvider();

        var service = new InventoryTransferService(transferRepo, orgRepo, catalogRepo, uow, userAccessor, dateTimeProvider);

        var centralBranchId = Guid.NewGuid();
        var jalapaBranchId = Guid.NewGuid();
        var centralWh = new Warehouse(centralBranchId, "CENTRAL", "Bodega Central", isDefault: true);
        var jalapaWh = new Warehouse(jalapaBranchId, "JALAPA", "Bodega Jalapa", isDefault: true);
        await orgRepo.AddWarehouseAsync(centralWh);
        await orgRepo.AddWarehouseAsync(jalapaWh);

        var product = new Product("PROD-01", "P001", "Harina Especial", null, Guid.NewGuid(), "MarcaX", "KG", 10.00m, 10.00m, 15.00m, 5, appliesTax: true);
        await catalogRepo.AddProductAsync(product);

        // Wholesale purchase enters Central: 100 units @ Q10
        var centralStock = new InventoryStock(product.Id, centralWh.Id);
        centralStock.ReplaceForSystem(100m);
        await transferRepo.AddStockAsync(centralStock);

        var jalapaStock = new InventoryStock(product.Id, jalapaWh.Id);
        jalapaStock.ReplaceForSystem(0m);
        await transferRepo.AddStockAsync(jalapaStock);

        // 2. Create Draft Transfer: 30 units from Central to Jalapa with assigned cost Q12 (differs from purchase cost Q10)
        var createRequest = new CreateInventoryTransferRequest(
            centralWh.Id,
            jalapaWh.Id,
            [new CreateInventoryTransferItemRequest(product.Id, 30m, AssignedUnitCost: 12.00m)],
            "Abastecimiento a Jalapa");

        var draft = await service.CreateTransferAsync(createRequest);

        Assert.Equal(InventoryTransferStatus.Draft, draft.Status);
        // In draft status, stock must NOT move yet!
        Assert.Equal(100m, (await transferRepo.GetStockAsync(product.Id, centralWh.Id))!.CurrentStock);
        Assert.Equal(0m, (await transferRepo.GetStockAsync(product.Id, jalapaWh.Id))!.CurrentStock);

        // 3. Dispatch Transfer
        var dispatched = await service.DispatchTransferAsync(draft.Id);

        Assert.Equal(InventoryTransferStatus.Dispatched, dispatched.Status);
        // Central stock reduced by 30 -> 70
        Assert.Equal(70m, (await transferRepo.GetStockAsync(product.Id, centralWh.Id))!.CurrentStock);
        // Destination stock still 0 (in transit)
        Assert.Equal(0m, (await transferRepo.GetStockAsync(product.Id, jalapaWh.Id))!.CurrentStock);

        // Verify TransferExit movement was recorded
        var exitMovement = Assert.Single(transferRepo.Movements, m => m.Type == InventoryMovementType.TransferExit);
        Assert.Equal(30m, exitMovement.Quantity);
        Assert.Equal(100m, exitMovement.PreviousStock);
        Assert.Equal(70m, exitMovement.NewStock);
        Assert.Equal(centralWh.Id, exitMovement.WarehouseId);
        Assert.Equal(10m, exitMovement.UnitCost); // Central cost origin

        // 4. Receive Transfer
        var received = await service.ReceiveTransferAsync(draft.Id);

        Assert.Equal(InventoryTransferStatus.Received, received.Status);
        // Jalapa received 30
        Assert.Equal(70m, (await transferRepo.GetStockAsync(product.Id, centralWh.Id))!.CurrentStock);
        Assert.Equal(30m, (await transferRepo.GetStockAsync(product.Id, jalapaWh.Id))!.CurrentStock);

        // Verify TransferEntry movement was recorded with ASSIGNED cost Q12
        var entryMovement = Assert.Single(transferRepo.Movements, m => m.Type == InventoryMovementType.TransferEntry);
        Assert.Equal(30m, entryMovement.Quantity);
        Assert.Equal(0m, entryMovement.PreviousStock);
        Assert.Equal(30m, entryMovement.NewStock);
        Assert.Equal(jalapaWh.Id, entryMovement.WarehouseId);
        Assert.Equal(12m, entryMovement.UnitCost); // Assigned distribution cost

        // 5. Concurrency / Idempotence: Double dispatch or receive must fail
        await Assert.ThrowsAsync<DomainRuleException>(() => service.DispatchTransferAsync(draft.Id));
        await Assert.ThrowsAsync<DomainRuleException>(() => service.ReceiveTransferAsync(draft.Id));
    }

    [Fact]
    public async Task Dispatch_Fails_When_OriginWarehouse_Has_Insufficient_Stock()
    {
        var transferRepo = new FakeTransferRepository();
        var orgRepo = new FakeOrganizationRepository();
        var catalogRepo = new FakeCatalogRepository();
        var service = new InventoryTransferService(transferRepo, orgRepo, catalogRepo, new FakeUnitOfWork(), new FakeUserAccessor(), new FakeDateTimeProvider());

        var centralWh = new Warehouse(Guid.NewGuid(), "CENTRAL", "Central");
        var jalapaWh = new Warehouse(Guid.NewGuid(), "JALAPA", "Jalapa");
        await orgRepo.AddWarehouseAsync(centralWh);
        await orgRepo.AddWarehouseAsync(jalapaWh);

        var product = new Product("PROD-02", "P002", "Queso Mozzarella", null, Guid.NewGuid(), "MarcaY", "LB", 25m, 25m, 35m, 2, appliesTax: true);
        await catalogRepo.AddProductAsync(product);

        // Only 10 in Central stock
        var centralStock = new InventoryStock(product.Id, centralWh.Id);
        centralStock.ReplaceForSystem(10m);
        await transferRepo.AddStockAsync(centralStock);

        // Attempt transfer of 20
        var draft = await service.CreateTransferAsync(new CreateInventoryTransferRequest(
            centralWh.Id,
            jalapaWh.Id,
            [new CreateInventoryTransferItemRequest(product.Id, 20m)]));

        var ex = await Assert.ThrowsAsync<DomainRuleException>(() => service.DispatchTransferAsync(draft.Id));
        Assert.Contains("insuficiente", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Stock remains untouched
        Assert.Equal(10m, (await transferRepo.GetStockAsync(product.Id, centralWh.Id))!.CurrentStock);
    }
}
