using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Common.Exceptions;
using SistemiPos.Application.Orders;
using SistemiPos.Application.Organizations;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Orders;
using SistemiPos.Domain.Organizations;

namespace SistemiPos.UnitTests.Orders;

public sealed class TerminalEditingTests
{
    [Fact]
    public async Task Edit_Preserves_Identity_And_Normalizes_Values()
    {
        var f = new Fixture();
        var id = f.Repository.Terminal.Id;
        await f.Service.UpdateTerminalAsync(id, new(f.Repository.Target.Id, " nueva ", " caja-2 "));
        Assert.Equal(id, f.Repository.Terminal.Id);
        Assert.Equal(f.Branch, f.Repository.Terminal.BranchId);
        Assert.Equal("NUEVA", f.Repository.Terminal.Code);
        Assert.Equal("CAJA-2", f.Repository.Terminal.RegisterCode);
        Assert.Equal(f.Repository.Target.Id, f.Repository.Terminal.WarehouseId);
        Assert.Equal(1, f.Unit.Saves);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Active_Operations_Block_Changing_Register_Or_Warehouse(bool changeRegister)
    {
        var f = new Fixture(); f.Repository.HasOperations = true;
        var request = new SaveTerminalRequest(changeRegister ? f.Repository.Original.Id : f.Repository.Target.Id,
            "NUEVA", changeRegister ? "CAJA-2" : "CAJA-1");
        await Assert.ThrowsAsync<DomainRuleException>(() => f.Service.UpdateTerminalAsync(f.Repository.Terminal.Id, request));
        AssertUnchanged(f);
    }

    [Fact]
    public async Task Renaming_An_Active_Terminal_Preserves_Operational_Associations()
    {
        var f = new Fixture(); f.Repository.HasOperations = true;
        await f.Service.UpdateTerminalAsync(f.Repository.Terminal.Id, new(f.Repository.Original.Id, "NUEVA", "CAJA-1"));
        Assert.Equal("NUEVA", f.Repository.Terminal.Code);
        Assert.Equal(f.Repository.Original.Id, f.Repository.Terminal.WarehouseId);
        Assert.Equal("CAJA-1", f.Repository.Terminal.RegisterCode);
    }

    [Fact]
    public async Task Duplicate_Code_Is_Rejected_Without_Changes()
    {
        var f = new Fixture(); f.Repository.Duplicate = true;
        await Assert.ThrowsAsync<DomainRuleException>(() => f.Service.UpdateTerminalAsync(f.Repository.Terminal.Id,
            new(f.Repository.Target.Id, "EXISTENTE", "CAJA-2")));
        AssertUnchanged(f);
    }

    [Fact]
    public async Task Edit_And_Form_Require_Management_Permission()
    {
        var f = new Fixture(); f.Authorization.Allowed = false;
        await Assert.ThrowsAsync<DomainRuleException>(() => f.Service.GetTerminalForEditAsync(f.Repository.Terminal.Id));
        await Assert.ThrowsAsync<DomainRuleException>(() => f.Service.UpdateTerminalAsync(f.Repository.Terminal.Id,
            new(f.Repository.Target.Id, "NUEVA", "CAJA-2")));
        AssertUnchanged(f);
    }

    [Fact]
    public async Task Edit_And_Form_Reject_Another_Branch()
    {
        var f = new Fixture(); f.Context.Branch = Guid.NewGuid();
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.GetTerminalForEditAsync(f.Repository.Terminal.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.UpdateTerminalAsync(f.Repository.Terminal.Id,
            new(f.Repository.Target.Id, "NUEVA", "CAJA-2")));
        AssertUnchanged(f);
    }

    [Fact]
    public void Domain_Rejects_Warehouse_From_Another_Branch_Without_Partial_Changes()
    {
        var f = new Fixture();
        Assert.Throws<DomainRuleException>(() => f.Repository.Terminal.Update(new Warehouse(Guid.NewGuid(), "X", "Otra"), "NUEVA", "CAJA-2"));
        AssertUnchanged(f);
    }

    private static void AssertUnchanged(Fixture f)
    {
        Assert.Equal("ORIGINAL", f.Repository.Terminal.Code);
        Assert.Equal("CAJA-1", f.Repository.Terminal.RegisterCode);
        Assert.Equal(f.Repository.Original.Id, f.Repository.Terminal.WarehouseId);
        Assert.Equal(0, f.Unit.Saves);
    }

    private sealed class Fixture
    {
        public Guid Branch { get; } = Guid.NewGuid();
        public Repo Repository { get; }
        public Unit Unit { get; } = new();
        public Authorization Authorization { get; } = new();
        public Context Context { get; }
        public OrderService Service { get; }
        public Fixture()
        {
            Repository = new(Branch); Context = new(Branch);
            Service = new(Repository, null!, null!, Unit, new User(), Context, Authorization, null!, null!);
        }
    }
    private sealed class Unit : IUnitOfWork
    {
        public int Saves { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { Saves++; return Task.FromResult(1); }
    }
    private sealed class User : ICurrentUserAccessor
    {
        public string UserName => "admin";
        public Guid? UserId { get; } = Guid.NewGuid();
    }
    private sealed class Authorization : IOperationAuthorizationService
    {
        public bool Allowed { get; set; } = true;
        public Task<bool> HasPermissionAsync(string userName, string permissionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(Allowed && permissionCode == PermissionCodes.PosTerminalsManage);
        public Task<string> ValidateCredentialsAsync(string userName, string password, string? requiredPermission = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Context(Guid branch) : IOperationContextAccessor
    {
        public Guid Branch { get; set; } = branch;
        public Guid? CurrentBranchId => Branch;
        public Guid? CurrentCompanyId => null;
        public Guid? CurrentBusinessGroupId => null;
        public Guid? CurrentWarehouseId => null;
        public string? CurrentBranchName => null;
        public string? CurrentCompanyName => null;
        public Task<IReadOnlyList<BranchDto>> GetAuthorizedBranchesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<BranchDto>>([]);
        public Task SetCurrentBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Repo : IOrderRepository
    {
        public Warehouse Original { get; }
        public Warehouse Target { get; }
        public PosTerminal Terminal { get; }
        public bool Duplicate { get; set; }
        public bool HasOperations { get; set; }
        public Repo(Guid branch)
        {
            Original = new(branch, "W1", "Original"); Target = new(branch, "W2", "Destino");
            Terminal = new(branch, Original, "ORIGINAL", "CAJA-1");
        }
        public Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct) => action();
        public Task<PosTerminal?> GetTerminalAsync(Guid id, CancellationToken ct) => Task.FromResult<PosTerminal?>(id == Terminal.Id ? Terminal : null);
        public Task<Warehouse?> GetWarehouseAsync(Guid id, CancellationToken ct) => Task.FromResult<Warehouse?>(id == Original.Id ? Original : id == Target.Id ? Target : null);
        public Task<bool> TerminalCodeExistsAsync(Guid branchId, string code, Guid? excludingId, CancellationToken ct) => Task.FromResult(Duplicate);
        public Task<bool> HasTerminalOperationsAsync(Guid terminalId, Guid branchId, string currentRegister, string targetRegister, CancellationToken ct) => Task.FromResult(HasOperations);
        public Task<List<PosTerminal>> GetTerminalsAsync(Guid branchId, CancellationToken ct) => throw new NotSupportedException();
        public Task AddTerminalAsync(PosTerminal terminal, CancellationToken ct) => throw new NotSupportedException();
        public Task<List<RestaurantOrder>> GetOrdersAsync(Guid branchId, CancellationToken ct) => throw new NotSupportedException();
        public Task<RestaurantOrder?> GetOrderAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task AddOrderAsync(RestaurantOrder order, CancellationToken ct) => throw new NotSupportedException();
        public Task TrackNewDetailsAsync(RestaurantOrder order, CancellationToken ct) => throw new NotSupportedException();
        public Task<Product?> GetProductAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<InventoryStock?> GetStockAsync(Guid productId, Guid warehouseId, CancellationToken ct) => throw new NotSupportedException();
        public Task AddMovementAsync(InventoryMovement movement, CancellationToken ct) => throw new NotSupportedException();
    }
}
