using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Cash;
using SistemiPos.Application.Organizations;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Orders;
using SistemiPos.Domain.Organizations;
using SistemiPos.Infrastructure.Cash;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.IntegrationTests.Infrastructure;

public sealed class PostgresCashFactAttribute : FactAttribute
{
    public PostgresCashFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SISTEMI_TEST_CONNECTION")))
            Skip = "Requiere SISTEMI_TEST_CONNECTION y PostgreSQL con permiso para crear una base de prueba aislada.";
    }
}

public sealed class PosCashDatabase : IAsyncLifetime
{
    private readonly string name = "sistemi_pos_cash_test_" + Guid.NewGuid().ToString("N");
    private string? admin;
    public DbContextOptions<AppDbContext> Options { get; private set; } = null!;
    public async Task InitializeAsync()
    {
        var source = Environment.GetEnvironmentVariable("SISTEMI_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(source)) return;
        admin = new NpgsqlConnectionStringBuilder(source) { Database = "postgres", Pooling = false }.ConnectionString;
        await using var connection = new NpgsqlConnection(admin); await connection.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection)) await create.ExecuteNonQueryAsync();
        Options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(source) { Database = name, Pooling = false }.ConnectionString).Options;
        await using var db = new AppDbContext(Options); await db.Database.MigrateAsync();
    }
    public async Task DisposeAsync()
    {
        if (admin is null) return;
        await using var connection = new NpgsqlConnection(admin); await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", connection); await drop.ExecuteNonQueryAsync();
    }
}

public sealed class PosCashPersistenceTests(PosCashDatabase database) : IClassFixture<PosCashDatabase>
{
    private sealed record Seed(PosTerminal Terminal, BranchDto Branch, User User);
    private async Task<Seed> SeedAsync()
    {
        await using var db = new AppDbContext(database.Options);
        var code = Guid.NewGuid().ToString("N");
        var group = new BusinessGroup(code, "Prueba caja");
        var company = new Company(group.Id, code, "Prueba caja");
        var branch = new Branch(company.Id, code, "Sucursal de prueba");
        var warehouse = new Warehouse(branch.Id, "B1", "Bodega", isDefault: true);
        var terminal = new PosTerminal(branch.Id, warehouse, "T1", "C1");
        db.AddRange(group, company, branch, warehouse, terminal); await db.SaveChangesAsync();
        return new(terminal, new(branch.Id, company.Id, company.TradeName, branch.Code, branch.Name, null, null, branch.TimeZone, null, true, 1), new());
    }
    private static PosCashService Service(AppDbContext db, Seed seed, User? user = null, bool blind = false)
    {
        var actor = user ?? seed.User; var context = new Context(seed.Branch); var auth = new Authorization(blind); var clock = new Clock();
        return new(db, new CashService(new CashRepository(db, context), db, actor, clock, auth, context), context, actor, auth, clock, Options.Create(new PosCashOptions()));
    }
    [PostgresCashFact]
    public async Task OpeningIsPersistentAndConcurrentRetryCreatesOnlyOneTurnAndReceipt()
    {
        var seed = await SeedAsync(); var key = Guid.NewGuid().ToString();
        async Task<PosCashMutationResult> Open()
        {
            await using var db = new AppDbContext(database.Options);
            return await Service(db, seed).OpenAsync(new(seed.Terminal.Id, 0, "Inicio"), key);
        }
        var result = await Task.WhenAll(Open(), Open());
        Assert.Equal(result[0].SessionId, result[1].SessionId); Assert.Single(result, x => x.Replayed);
        await using var check = new AppDbContext(database.Options);
        var session = await check.CashSessions.SingleAsync(x => x.BranchId == seed.Branch.Id);
        Assert.Equal(seed.Terminal.Id, session.TerminalId); Assert.Equal(0, session.OpeningAmount);
        var reload = await Service(check, seed).StateAsync(seed.Terminal.Id);
        Assert.Equal(session.OpenedAtUtc, reload.Session!.OpenedAtUtc);
        Assert.Equal("America/Guatemala", reload.Timezone);
        Assert.Single(await check.Set<CashCommandReceipt>().Where(x => x.SessionId == session.Id).ToListAsync());
        var error = await Assert.ThrowsAsync<CashOperationException>(() => Service(check, seed).OpenAsync(new(seed.Terminal.Id, 1, "Inicio"), key));
        Assert.Equal("IDEMPOTENCY_CONFLICT", error.Code);
    }
    [PostgresCashFact]
    public async Task TwoCashiersCannotOpenSameTerminalOrOperateAnotherCashiersTurn()
    {
        var seed = await SeedAsync(); var other = new User();
        async Task<bool> Open(User actor)
        {
            await using var db = new AppDbContext(database.Options);
            try { await Service(db, seed, actor).OpenAsync(new(seed.Terminal.Id, 200), Guid.NewGuid().ToString()); return true; }
            catch (CashOperationException ex) when (ex.Code == "CASH_ALREADY_OPEN") { return false; }
        }
        var results = await Task.WhenAll(Open(seed.User), Open(other)); Assert.Single(results, x => x);
        var loser = results[0] ? other : seed.User;
        await using var check = new AppDbContext(database.Options);
        var session = await check.CashSessions.SingleAsync(x => x.BranchId == seed.Branch.Id);
        var error = await Assert.ThrowsAsync<CashOperationException>(() => Service(check, seed, loser).MovementAsync(new(seed.Terminal.Id, session.Id, true, 1, "No autorizado"), Guid.NewGuid().ToString()));
        Assert.Equal(403, error.Status);
        Assert.Empty(await check.CashMovements.Where(x => x.CashSessionId == session.Id).ToListAsync());
    }
    [PostgresCashFact]
    public async Task LedgerExampleMovementReplayReversalAndClosingAreConsistent()
    {
        var seed = await SeedAsync();
        Guid id;
        await using (var openDb = new AppDbContext(database.Options)) id = (await Service(openDb, seed).OpenAsync(new(seed.Terminal.Id, 200), "open")).SessionId;
        await using (var db = new AppDbContext(database.Options))
        {
            var session = await db.CashSessions.SingleAsync(x => x.Id == id);
            foreach (var (type, amount) in new[] { (CashMovementType.SaleCash, 70m), (CashMovementType.SalePos, 120m), (CashMovementType.SaleTransfer, 80m), (CashMovementType.RefundCash, 10m) })
            {
                var m = new CashMovement(type, amount, "Pago de prueba", seed.User.UserName, DateTimeOffset.UtcNow) { CashSessionId = id };
                session.AddMovement(m); db.Add(m);
            }
            await db.SaveChangesAsync();
        }
        await using (var db = new AppDbContext(database.Options))
        {
            var service = Service(db, seed);
            await service.MovementAsync(new(seed.Terminal.Id, id, true, 30, "Entrada"), "entry");
            await service.MovementAsync(new(seed.Terminal.Id, id, true, 30, "Entrada"), "entry");
            await service.MovementAsync(new(seed.Terminal.Id, id, false, 20, "Salida"), "expense");
            var state = await service.StateAsync(seed.Terminal.Id); Assert.Equal(270, state.Session!.Summary!.ExpectedCash);
            Assert.Equal(120, state.Session.Summary.CardSales); Assert.Equal(80, state.Session.Summary.TransferSales);
            var closed = await service.CloseAsync(new(seed.Terminal.Id, id, state.Session.Revision, 270), "close");
            Assert.Equal("CLOSED", closed.State.Status); Assert.Equal(0, closed.State.Session!.Summary!.Difference);
            Assert.True((await service.CloseAsync(new(seed.Terminal.Id, id, state.Session.Revision, 270), "close")).Replayed);
        }
    }
    [PostgresCashFact]
    public async Task BlindCountAndOtherBranchNeverExposeBalances()
    {
        var seed = await SeedAsync(); var otherBranch = await SeedAsync();
        await using var db = new AppDbContext(database.Options);
        await Service(db, seed).OpenAsync(new(seed.Terminal.Id, 200), "open");
        var state = await Service(db, seed, blind: true).StateAsync(seed.Terminal.Id);
        Assert.Null(state.Session!.OpeningAmount); Assert.Null(state.Session.Summary);
        var forbidden = await Assert.ThrowsAsync<CashOperationException>(() => Service(db, otherBranch).StateAsync(seed.Terminal.Id));
        Assert.Equal(403, forbidden.Status);
    }
    [PostgresCashFact]
    public async Task ConcurrentManualReversalsAreNotDuplicated()
    {
        var seed = await SeedAsync(); Guid id; Guid movement;
        await using (var db = new AppDbContext(database.Options))
        {
            var service = Service(db, seed); id = (await service.OpenAsync(new(seed.Terminal.Id, 0), "open")).SessionId;
            await service.MovementAsync(new(seed.Terminal.Id, id, true, 30, "Entrada"), "entry");
            movement = (await db.CashMovements.SingleAsync(x => x.CashSessionId == id)).Id;
        }
        async Task<bool> Reverse()
        {
            await using var db = new AppDbContext(database.Options);
            try { await Service(db, seed).MovementAsync(new(seed.Terminal.Id, id, false, 30, "Reverso", ReversalOfId: movement), Guid.NewGuid().ToString()); return true; }
            catch (CashOperationException ex) when (ex.Code == "INVALID_REVERSAL") { return false; }
        }
        Assert.Single(await Task.WhenAll(Reverse(), Reverse()), x => x);
        await using var check = new AppDbContext(database.Options);
        Assert.Equal(0, (await Service(check, seed).StateAsync(seed.Terminal.Id)).Session!.Summary!.ExpectedCash);
    }
    [PostgresCashFact]
    public async Task SaleAndClosingUseSameConcurrencyTokenAndRollbackLosingMovement()
    {
        foreach (var closeWins in new[] { true, false })
        {
            var seed = await SeedAsync(); Guid id;
            await using (var openDb = new AppDbContext(database.Options)) id = (await Service(openDb, seed).OpenAsync(new(seed.Terminal.Id, 200), "open")).SessionId;
            await using var saleDb = new AppDbContext(database.Options); await using var closeDb = new AppDbContext(database.Options);
            var saleSession = await saleDb.CashSessions.Include(x => x.Movements).SingleAsync(x => x.Id == id);
            var closeSession = await closeDb.CashSessions.Include(x => x.Movements).SingleAsync(x => x.Id == id);
            var movement = new CashMovement(CashMovementType.SaleCash, 70, "Venta", seed.User.UserName, DateTimeOffset.UtcNow) { CashSessionId = id };
            saleSession.AddMovement(movement); saleDb.Add(movement);
            closeSession.Close(DateTimeOffset.UtcNow, 200, reconciledByUserName: seed.User.UserName);
            if (closeWins) { await closeDb.SaveChangesAsync(); await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => saleDb.SaveChangesAsync()); }
            else { await saleDb.SaveChangesAsync(); await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => closeDb.SaveChangesAsync()); }
            await using var check = new AppDbContext(database.Options);
            var current = await check.CashSessions.Include(x => x.Movements).SingleAsync(x => x.Id == id);
            Assert.Equal(closeWins ? 200 : 270, current.CalculateExpectedCash());
            Assert.Equal(closeWins ? 0 : 1, current.Movements.Count);
            if (!closeWins)
            {
                var state = await Service(check, seed).StateAsync(seed.Terminal.Id);
                await Service(check, seed).CloseAsync(new(seed.Terminal.Id, id, state.Session!.Revision, 270), "retry-close");
            }
        }
    }
    private sealed class User : ICurrentUserAccessor
    {
        public string UserName { get; } = "cash-test-" + Guid.NewGuid().ToString("N");
        public Guid? UserId { get; } = Guid.NewGuid();
    }
    private sealed class Clock : IDateTimeProvider { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
    private sealed class Authorization(bool blind) : IOperationAuthorizationService
    {
        public Task<bool> HasPermissionAsync(string user, string code, CancellationToken ct = default) =>
            Task.FromResult(code == PermissionCodes.CashCountHideExpected ? blind : code is not PermissionCodes.CashOpenRequirePassword and not PermissionCodes.CashCloseRequirePassword);
        public Task<string> ValidateCredentialsAsync(string user, string password, string? permission = null, CancellationToken ct = default) => Task.FromResult(user);
    }
    private sealed class Context(BranchDto branch) : IOperationContextAccessor
    {
        public Guid? CurrentBranchId => branch.Id; public Guid? CurrentCompanyId => branch.CompanyId;
        public Guid? CurrentBusinessGroupId => null; public Guid? CurrentWarehouseId => null;
        public string? CurrentBranchName => branch.Name; public string? CurrentCompanyName => branch.CompanyTradeName;
        public Task<IReadOnlyList<BranchDto>> GetAuthorizedBranchesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BranchDto>>([branch]);
        public Task SetCurrentBranchAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }
}
