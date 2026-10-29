using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Cash;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Configuration;

namespace SistemiPos.UnitTests.Cash;

public sealed class CashServiceTests
{
    [Fact]
    public async Task Development_Mode_Allows_Difference_Without_Additional_Credentials_But_Requires_Reason()
    {
        var session = new CashSession("POS-1", "tester", DateTimeOffset.UtcNow, 100m);
        var authorization = new FakeAuthorizationService();
        var service = new CashService(new FakeCashRepository(session), new FakeUnitOfWork(),
            new FakeCurrentUserAccessor(), new FakeDateTimeProvider(), authorization,
            cashAuthorizationPolicy: new CashAuthorizationPolicy(true));
        await Assert.ThrowsAsync<DomainRuleException>(() => service.CloseSessionAsync(session.Id, new CloseCashSessionRequest(95m)));
        await service.CloseSessionAsync(session.Id, new CloseCashSessionRequest(95m, "Faltante declarado", RequireAuthorization: true));
        Assert.Equal(-5m, session.Difference);
        Assert.Null(session.DifferenceAuthorizedByUserName);
        Assert.Empty(authorization.Validations);
    }

    [Fact]
    public async Task Development_Mode_Allows_Manual_Cash_Movement_Without_Additional_Credentials()
    {
        var session = new CashSession("POS-1", "tester", DateTimeOffset.UtcNow, 100m);
        var authorization = new FakeAuthorizationService();
        var service = new CashService(new FakeCashRepository(session), new FakeUnitOfWork(),
            new FakeCurrentUserAccessor(), new FakeDateTimeProvider(), authorization,
            cashAuthorizationPolicy: new CashAuthorizationPolicy(true));
        await service.RecordMovementAsync(session.Id, new RecordCashMovementRequest(CashMovementType.Expense, 5m, "Compra"));
        Assert.Single(session.Movements);
        Assert.Empty(authorization.Validations);
    }
    [Fact]
    public async Task CloseSessionAsync_Rejects_Unresolved_Payments_And_Missing_Difference_Reason()
    {
        var session = new CashSession("POS-1", "tester", DateTimeOffset.UtcNow, 100);
        var repository = new FakeCashRepository(session) { PendingPayments = true };
        var service = new CashService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());
        await Assert.ThrowsAsync<DomainRuleException>(() => service.CloseSessionAsync(session.Id, new(100)));
        repository.PendingPayments = false;
        await Assert.ThrowsAsync<DomainRuleException>(() => service.CloseSessionAsync(session.Id, new(95)));
        Assert.Equal(CashSessionStatus.Active, session.Status);
    }
    [Fact]
    public async Task OpenSessionAsync_Rejects_Second_Active_Session_For_User()
    {
        var existing = new CashSession("POS-1", "tester", new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero), 100m);
        var repository = new FakeCashRepository(existing);
        var service = new CashService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.OpenSessionAsync(new OpenCashSessionRequest("POS-1", 50m)));
    }

    [Fact]
    public async Task OpenSessionAsync_Allows_No_Pos_When_Pos_Payments_Are_Disabled()
    {
        var repository = new FakeCashRepository { PosPaymentsEnabled = false };
        var service = new CashService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        var sessionId = await service.OpenSessionAsync(new OpenCashSessionRequest("POS-1", 50m));

        var session = Assert.Single(repository.Sessions);
        Assert.Equal(session.Id, sessionId);
        Assert.Empty(session.AssignedPosDevices);
    }

    [Fact]
    public async Task OpenSessionAsync_Does_Not_Use_Pos_By_Default()
    {
        var repository = new FakeCashRepository { PosPaymentsEnabled = true };
        var service = new CashService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        await service.OpenSessionAsync(new OpenCashSessionRequest("POS-1", 50m));

        Assert.Empty(Assert.Single(repository.Sessions).AssignedPosDevices);
    }

    [Fact]
    public async Task OpenSessionAsync_Allows_User_To_Omit_Pos_For_One_Cash_Session()
    {
        var repository = new FakeCashRepository { PosPaymentsEnabled = true };
        var service = new CashService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        await service.OpenSessionAsync(new OpenCashSessionRequest("POS-1", 50m, UsePosDevices: false));

        Assert.Empty(Assert.Single(repository.Sessions).AssignedPosDevices);
    }

    [Fact]
    public async Task OpenSessionAsync_Assigns_An_Available_Pos_When_Selected()
    {
        var posDevice = new PosDevice("POS bancario 1");
        var repository = new FakeCashRepository { PosPaymentsEnabled = true };
        repository.PosDevices.Add(posDevice);
        var service = new CashService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        await service.OpenSessionAsync(new OpenCashSessionRequest(
            "POS-1", 50m, PosDeviceIds: [posDevice.Id], UsePosDevices: true));

        Assert.Equal(posDevice.Id, Assert.Single(Assert.Single(repository.Sessions).AssignedPosDevices).PosDeviceId);
    }

    [Fact]
    public async Task OpenSessionAsync_Rejects_A_Pos_Used_By_Another_Open_Cash_Session()
    {
        var posDevice = new PosDevice("POS bancario 1");
        var repository = new FakeCashRepository { PosPaymentsEnabled = true };
        repository.PosDevices.Add(posDevice);
        repository.AssignedPosDeviceIds.Add(posDevice.Id);
        var service = new CashService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        var exception = await Assert.ThrowsAsync<DomainRuleException>(() => service.OpenSessionAsync(
            new OpenCashSessionRequest("POS-1", 50m, PosDeviceIds: [posDevice.Id], UsePosDevices: true)));

        Assert.Equal("Uno o más POS seleccionados ya están siendo utilizados por otra caja abierta.", exception.Message);
        Assert.Empty(repository.Sessions);
    }

    [Fact]
    public async Task CloseSessionAsync_Calculates_Expected_Cash_And_Difference()
    {
        var session = new CashSession("POS-1", "tester", new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero), 100m);
        session.AddMovement(new CashMovement(CashMovementType.OtherIncome, 20m, "Fondo adicional", "tester", new DateTimeOffset(2026, 8, 30, 11, 0, 0, TimeSpan.Zero)));
        session.AddMovement(new CashMovement(CashMovementType.Expense, 5m, "Compra menor", "tester", new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero)));

        var repository = new FakeCashRepository(session);
        var service = new CashService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        await service.CloseSessionAsync(session.Id, new CloseCashSessionRequest(118m, "Sobrante declarado"));

        Assert.Equal(CashSessionStatus.Closed, session.Status);
        Assert.Equal(115m, session.ExpectedCash);
        Assert.Equal(3m, session.Difference);
        Assert.Equal(20m, session.OtherIncomeTotal);
        Assert.Equal(5m, session.ExpenseTotal);
    }

    [Fact]
    public async Task CloseSessionAsync_Automatically_Reconciles_When_There_Is_No_Difference()
    {
        var session = new CashSession("POS-1", "tester", new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero), 100m);
        var service = new CashService(new FakeCashRepository(session), new FakeUnitOfWork(),
            new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        await service.CloseSessionAsync(session.Id, new CloseCashSessionRequest(100m));

        Assert.Equal(0m, session.Difference);
        Assert.NotNull(session.ReconciledAtUtc);
        Assert.Equal("tester", session.ReconciledByUserName);
        Assert.Equal("Conciliación automática: cierre sin diferencia.", session.ReconciliationObservation);
    }

    [Fact]
    public async Task RecordAdjustmentAsync_Requires_Closed_Session()
    {
        var session = new CashSession("POS-1", "tester", new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero), 100m);
        var repository = new FakeCashRepository(session);
        var service = new CashService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.RecordAdjustmentAsync(session.Id, new RecordCashMovementRequest(CashMovementType.AdjustmentIncrease, 10m, "Corrección")));
    }

    [Fact]
    public async Task CloseSessionAsync_Uses_Authenticated_Owner_Without_Requesting_Password()
    {
        var session = new CashSession("POS-1", "tester", new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero), 100m);
        var authorization = new FakeAuthorizationService();
        var service = new CashService(new FakeCashRepository(session), new FakeUnitOfWork(),
            new FakeCurrentUserAccessor(), new FakeDateTimeProvider(), authorization);

        await service.CloseSessionAsync(session.Id, new CloseCashSessionRequest(100m));

        Assert.Equal(0m, session.Difference);
        Assert.Null(session.DifferenceAuthorizedByUserName);
        Assert.Empty(authorization.Validations);
    }

    [Fact]
    public async Task CloseSessionAsync_Difference_Requires_Authorization_And_Records_It()
    {
        var session = new CashSession("POS-1", "tester", DateTimeOffset.UtcNow, 100m);
        var authorization = new FakeAuthorizationService();
        var service = new CashService(new FakeCashRepository(session), new FakeUnitOfWork(),
            new FakeCurrentUserAccessor(), new FakeDateTimeProvider(), authorization);
        await Assert.ThrowsAsync<DomainRuleException>(() => service.CloseSessionAsync(session.Id, new CloseCashSessionRequest(95)));
        Assert.Equal(CashSessionStatus.Active, session.Status);
        await service.CloseSessionAsync(session.Id, new CloseCashSessionRequest(95, "Faltante declarado", AuthorizerUserName: "tester", AuthorizerPassword: "secret"));
        Assert.Equal(-5, session.Difference);
        Assert.Equal("tester", session.DifferenceAuthorizedByUserName);
        Assert.Equal(PermissionCodes.CashDifferenceAuthorize, authorization.Validations.Last().Permission);
    }

    [Fact]
    public async Task ReconcileSessionAsync_Preserves_Difference_And_Records_Review()
    {
        var session = new CashSession("POS-1", "tester", new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero), 100m);
        session.Close(new DateTimeOffset(2026, 8, 30, 13, 0, 0, TimeSpan.Zero), 95m);
        var service = new CashService(new FakeCashRepository(session), new FakeUnitOfWork(),
            new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        await service.ReconcileSessionAsync(session.Id, "Efectivo entregado");

        Assert.Equal(-5m, session.Difference);
        Assert.Equal("tester", session.ReconciledByUserName);
        Assert.Equal("Efectivo entregado", session.ReconciliationObservation);
        Assert.NotNull(session.ReconciledAtUtc);
    }

    [Fact]
    public async Task RecordCountAsync_Registers_New_Count_Explicitly_For_Persistence()
    {
        var session = new CashSession("POS-1", "tester", new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero), 100m);
        var repository = new FakeCashRepository(session);
        var denomination = new CashDenomination(DenominationKind.Banknote, 100m);
        repository.Denominations.Add(denomination);
        var service = new CashService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        var result = await service.RecordCountAsync(session.Id,
            new RecordCashCountRequest([new CashCountLineRequest(denomination.Id, 1)]));

        Assert.Same(session.Counts.Single(), repository.AddedCounts.Single());
        Assert.Equal(100m, result.CountedCash);
    }

    [Fact]
    public async Task RecordMovementAsync_Registers_Expense_Explicitly_For_Persistence()
    {
        var session = new CashSession("POS-1", "tester", new DateTimeOffset(2026, 9, 11, 20, 15, 0, TimeSpan.Zero), 500m);
        var repository = new FakeCashRepository(session);
        var service = new CashService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        await service.RecordMovementAsync(session.Id,
            new RecordCashMovementRequest(CashMovementType.Expense, 200m, "Sencillo"));

        var movement = Assert.Single(repository.AddedMovements);
        Assert.Same(Assert.Single(session.Movements), movement);
        Assert.Equal(CashMovementType.Expense, movement.Type);
        Assert.Equal(200m, movement.Amount);
    }

    [Fact]
    public async Task RecordPosClosingAsync_Registers_New_Closing_Explicitly_For_Persistence()
    {
        var session = new CashSession("POS-1", "tester", new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero), 100m);
        var posDeviceId = Guid.NewGuid();
        session.AssignPosDevice(posDeviceId, "POS bancario 1");
        var repository = new FakeCashRepository(session);
        var service = new CashService(repository, new FakeUnitOfWork(), new FakeCurrentUserAccessor(), new FakeDateTimeProvider());

        await service.RecordPosClosingAsync(session.Id, new RecordPosClosingRequest(posDeviceId, 75m, "LOTE-001", null));

        Assert.Same(session.PosClosings.Single(), repository.AddedPosClosings.Single());
        Assert.Equal(75m, repository.AddedPosClosings.Single().PhysicalTotal);
    }

    private sealed class FakeCashRepository(params CashSession[] sessions) : ICashRepository
    {
        public List<CashSession> Sessions { get; } = sessions.ToList();
        public List<CashDenomination> Denominations { get; } = [];
        public List<CashMovement> AddedMovements { get; } = [];
        public List<CashCount> AddedCounts { get; } = [];
        public List<CashPosClosing> AddedPosClosings { get; } = [];
        public List<PosDevice> PosDevices { get; } = [];
        public HashSet<Guid> AssignedPosDeviceIds { get; } = [];
        public bool PosPaymentsEnabled { get; set; } = true;
        public bool PendingPayments { get; set; }
        public Task<bool> HasPendingPaymentsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(PendingPayments);

        public Task AddCountAsync(CashCount count, CancellationToken cancellationToken = default)
        {
            AddedCounts.Add(count);
            return Task.CompletedTask;
        }

        public Task AddMovementAsync(CashMovement movement, CancellationToken cancellationToken = default)
        {
            AddedMovements.Add(movement);
            return Task.CompletedTask;
        }

        public Task AddPosClosingAsync(CashPosClosing closing, CancellationToken cancellationToken = default)
        {
            AddedPosClosings.Add(closing);
            return Task.CompletedTask;
        }

        public Task<bool> IsPosPaymentEnabledAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(PosPaymentsEnabled);

        public Task<List<PosDevice>> GetActivePosDevicesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
            Task.FromResult(PosDevices.Where(x => x.IsActive && ids.Contains(x.Id)).ToList());

        public Task<List<Guid>> GetPosDeviceIdsAssignedToActiveSessionsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
            Task.FromResult(AssignedPosDeviceIds.Where(ids.Contains).ToList());

        public Task<List<CashDenomination>> GetActiveDenominationsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Denominations.ToList());

        public Task AddSessionAsync(CashSession session, CancellationToken cancellationToken = default)
        {
            Sessions.Add(session);
            return Task.CompletedTask;
        }

        public Task<CashSession?> GetActiveSessionByRegisterAsync(string registerCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(Sessions.FirstOrDefault(x => x.RegisterCode == registerCode && x.Status == CashSessionStatus.Active));

        public Task<CashSession?> GetActiveSessionByUserAsync(Guid? userId, string userName, CancellationToken cancellationToken = default) =>
            Task.FromResult(Sessions.FirstOrDefault(x => x.CashierUserName == userName && x.Status == CashSessionStatus.Active));

        public Task<CashSession?> GetSessionAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Sessions.FirstOrDefault(x => x.Id == id));

        public Task<List<CashSession>> GetSessionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Sessions.ToList());
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
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

    private sealed class FakeAuthorizationService : IOperationAuthorizationService
    {
        public List<(string UserName, string Password, string? Permission)> Validations { get; } = [];

        public Task<bool> HasPermissionAsync(string userName, string permissionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<string> ValidateCredentialsAsync(string userName, string password, string? requiredPermission = null,
            CancellationToken cancellationToken = default)
        {
            Validations.Add((userName, password, requiredPermission));
            if (userName != "tester" || password != "secret") throw new DomainRuleException("Credenciales inválidas.");
            return Task.FromResult(userName);
        }
    }
}
