using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Cash;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Audit;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Orders;
using SistemiPos.Domain.Sales;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Cash;

public sealed class PosCashService(AppDbContext db, ICashService cash, IOperationContextAccessor context,
    ICurrentUserAccessor user, IOperationAuthorizationService authorization, IDateTimeProvider clock,
    IOptions<PosCashOptions> options, CashAuthorizationPolicy? cashAuthorizationPolicy = null) : IPosCashService
{
    private Task<bool> Has(string code, CancellationToken ct) => authorization.HasPermissionAsync(user.UserName, code, ct);
    private bool Owns(CashSession s) => user.UserId.HasValue && s.CashierUserId == user.UserId
        || string.Equals(s.CashierUserName, user.UserName, StringComparison.OrdinalIgnoreCase);
    private static long Revision(CashSession s) => (long)s.OperationRevision * 2 + (s.Status == CashSessionStatus.Closed ? 1 : 0);
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static void Money(decimal amount, bool zero = true)
    {
        if (amount < 0 || !zero && amount == 0 || decimal.Round(amount, 2) != amount || amount > 9999999999999999.99m)
            throw new CashOperationException("INVALID_AMOUNT", "El importe debe ser válido y tener como máximo dos decimales.", 422);
    }
    private async Task Require(string code, CancellationToken ct)
    {
        if (!await Has(code, ct)) throw new CashOperationException("CASH_FORBIDDEN", "No tienes permiso para esta operación de caja.", 403);
    }
    private async Task<PosTerminal> Terminal(Guid id, CancellationToken ct, bool historical = false)
    {
        if (context.CurrentBranchId is not Guid branch || !(await context.GetAuthorizedBranchesAsync(ct)).Any(x => x.Id == branch))
            throw new CashOperationException("CASH_FORBIDDEN", "Selecciona una sucursal autorizada.", 403);
        return await db.Set<PosTerminal>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.BranchId == branch
                && (historical || x.IsActive && db.Warehouses.Any(w => w.Id == x.WarehouseId && w.BranchId == branch && w.IsActive)), ct)
            ?? throw new CashOperationException("TERMINAL_FORBIDDEN", "La terminal no está disponible en tu sucursal.", 403);
    }
    private IQueryable<CashSession> Sessions(PosTerminal terminal) => db.CashSessions
        .Where(x => x.BranchId == terminal.BranchId && (x.TerminalId == terminal.Id
            || x.TerminalId == null && x.RegisterCode == terminal.RegisterCode));
    private IQueryable<CashSession> Details(IQueryable<CashSession> query) => query.Include(x => x.Movements)
        .Include(x => x.Counts).ThenInclude(x => x.Lines).Include(x => x.AssignedPosDevices).Include(x => x.PosClosings);
    private async Task<CashSession> Owned(Guid id, PosTerminal terminal, CancellationToken ct)
    {
        var s = await Details(Sessions(terminal)).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new CashOperationException("CASH_SESSION_CHANGED", "La caja no corresponde a la terminal seleccionada.");
        if (!Owns(s)) throw new CashOperationException("CASH_FORBIDDEN", "Solo el cajero responsable puede operar el turno.", 403);
        if (s.Status != CashSessionStatus.Active) throw new CashOperationException("CASH_NOT_OPEN", "La caja ya está cerrada.");
        return s;
    }
    private async Task Password(string permission, string? password, CancellationToken ct)
    {
        if (cashAuthorizationPolicy?.SkipAdditionalCredentials != true && await Has(permission, ct))
            await authorization.ValidateCredentialsAsync(user.UserName, password ?? "", PermissionCodes.SalesManage, ct);
    }
    private async Task<PosCashState> StateFor(Guid? terminalId, Guid? resultSession, CancellationToken ct, bool historical = false)
    {
        var branchId = context.CurrentBranchId ?? throw new CashOperationException("CASH_FORBIDDEN", "Selecciona una sucursal.", 403);
        var branch = (await context.GetAuthorizedBranchesAsync(ct)).SingleOrDefault(x => x.Id == branchId)
            ?? throw new CashOperationException("CASH_FORBIDDEN", "La sucursal no está autorizada.", 403);
        var setting = await db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? SystemSetting.CreateDefault();
        var currency = await CashCurrency.ResolveAsync(db, branchId, ct);
        var openingCurrency = currency;
        var policy = options.Value.For(branch.CompanyId, branch.Id);
        PosTerminal? terminal = terminalId.HasValue ? await Terminal(terminalId.Value, ct, historical) : null;
        var sessionState = terminal is null ? null : await Sessions(terminal).Include(x => x.Movements).AsNoTracking()
            .Where(x => !resultSession.HasValue || x.Id == resultSession)
            .OrderByDescending(x => x.Status == CashSessionStatus.Active).ThenByDescending(x => x.OpenedAtUtc)
            .Select(x => new { Session = x, HasCount = db.CashCounts.Any(count => count.CashSessionId == x.Id) })
            .FirstOrDefaultAsync(ct);
        var session = sessionState?.Session;
        var owns = session is not null && Owns(session);
        var blind = session?.Status == CashSessionStatus.Active && await Has(PermissionCodes.CashCountHideExpected, ct) && sessionState?.HasCount != true;
        var canSee = session is not null && (owns || await Has(PermissionCodes.CashControlViewOtherUsers, ct))
            && !blind && await Has(PermissionCodes.CashView, ct);
        var canOperate = owns && session?.Status == CashSessionStatus.Active && await Has(PermissionCodes.SalesManage, ct);
        var permissions = new PosCashPermissions(terminal is not null && session?.Status != CashSessionStatus.Active && await Has(PermissionCodes.CashOpen, ct),
            canOperate, canOperate && await Has(PermissionCodes.CashCloseOwn, ct),
            canOperate && await Has(PermissionCodes.CashMovementsAddIncome, ct),
            canOperate && await Has(PermissionCodes.CashMovementsAddExpense, ct), canSee,
            cashAuthorizationPolicy?.SkipAdditionalCredentials != true && !await Has(PermissionCodes.CashMovementsWithoutAuthorization, ct),
            cashAuthorizationPolicy?.SkipAdditionalCredentials != true && await Has(PermissionCodes.CashOpenRequirePassword, ct), cashAuthorizationPolicy?.SkipAdditionalCredentials != true && await Has(PermissionCodes.CashCloseRequirePassword, ct), canSee && await Has(PermissionCodes.CashClosingReport, ct));
        PosCashSession? dto = null;
        if (session is not null)
        {
            if (session.CurrencySnapshotJson is not null) currency = JsonSerializer.Deserialize<CurrencySnapshot>(session.CurrencySnapshotJson) ?? currency;
            decimal Sum(CashMovementType type) => session.Movements.Where(x => x.Type == type).Sum(x => x.Amount);
            var reversed = session.Movements.Where(x => x.SourceType == "CashMovementReversal").Select(x => x.SourceId).ToHashSet();
            var summary = canSee ? new PosCashSummary(Sum(CashMovementType.SaleCash), Sum(CashMovementType.SalePos) - Sum(CashMovementType.RefundPos),
                Sum(CashMovementType.SaleTransfer) - Sum(CashMovementType.RefundTransfer), Sum(CashMovementType.SaleOther) - Sum(CashMovementType.RefundOther), Sum(CashMovementType.OtherIncome), Sum(CashMovementType.Expense),
                Sum(CashMovementType.RefundCash), Sum(CashMovementType.SupplierPayment) + Sum(CashMovementType.CashWithdrawal) + Sum(CashMovementType.CashTransfer),
                session.ExpectedCash ?? session.CalculateExpectedCash(), session.CountedCash, session.Difference, Sum(CashMovementType.CollectionCash),
                session.Movements.OrderByDescending(x => x.OccurredAtUtc).Select(x => new PosCashMovementLine(x.Id, x.Type.ToString(), x.Amount,
                    x.Reason, x.UserName, x.OccurredAtUtc, x.SourceDocument, canOperate && !reversed.Contains(x.Id)
                        && x.SourceType != "CashMovementReversal" && x.Type is CashMovementType.OtherIncome or CashMovementType.Expense)).ToList()) : null;
            dto = new(session.Id, session.Status == CashSessionStatus.Active ? "OPEN" : "CLOSED",
                owns || await Has(PermissionCodes.CashControlViewOtherUsers, ct) ? session.CashierUserName : "Otro cajero",
                session.OpenedAtUtc, canSee ? session.OpeningAmount : null, Revision(session), summary, session.ClosedAtUtc, session.ClosedByUserName);
        }
        return new(clock.UtcNow, branch.Id, branch.Name, terminal?.Id, terminal?.Code,
            CanonicalZone(resultSession.HasValue ? session?.TimeZoneSnapshot ?? branch.TimeZone : branch.TimeZone), currency, terminal is null ? "NO_TERMINAL" : dto?.Status ?? "CLOSED", dto,
            permissions, policy.MaximumOpeningAmount, policy.LongShiftHours, user.UserName, openingCurrency);
    }
    private static string CanonicalZone(string zone) => TimeZoneInfo.TryConvertWindowsIdToIanaId(zone, out var iana) ? iana : zone;
    public Task<PosCashState> StateAsync(Guid? terminalId, CancellationToken ct = default) => StateFor(terminalId, null, ct);

    public async Task<PosCashMutationResult?> ResultAsync(Guid terminalId, string kind, string key, CancellationToken ct = default)
    {
        var terminal = await Terminal(terminalId, ct, historical: true);
        var operation = kind switch { "abrir" => "open", "movimiento" => "movement", "cerrar" => "close", _ => throw new CashOperationException("INVALID_OPERATION", "Operación no válida.", 422) };
        var scope = Hash($"{terminal.BranchId}:{user.UserId}:{user.UserName}:{operation}:{key}");
        var receipt = await db.Set<CashCommandReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.ScopeKey == scope, ct);
        if (receipt is not null && !await Sessions(terminal).AnyAsync(x => x.Id == receipt.SessionId, ct))
            throw new CashOperationException("IDEMPOTENCY_CONFLICT", "La operación pertenece a otra terminal.");
        return receipt is null ? null : new(receipt.SessionId, true, await StateFor(terminal.Id, receipt.SessionId, ct, historical: true));
    }
    public async Task<PosCashState> ReceiptAsync(Guid sessionId, CancellationToken ct = default)
    {
        await Require(PermissionCodes.CashClosingReport, ct);
        var session = await db.CashSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sessionId && x.BranchId == context.CurrentBranchId, ct)
            ?? throw new CashOperationException("CASH_FORBIDDEN", "La caja no está disponible en tu sucursal.", 403);
        if (session.Status != CashSessionStatus.Closed || !session.TerminalId.HasValue)
            throw new CashOperationException("CASH_NOT_CLOSED", "El comprobante requiere una caja cerrada vinculada a una terminal.");
        var result = await StateFor(session.TerminalId, sessionId, ct, historical: true);
        if (!result.Permissions.CanSeeTotals) throw new CashOperationException("CASH_FORBIDDEN", "No tienes permiso para consultar los totales.", 403);
        return result;
    }

    private async Task<PosCashMutationResult> Execute<T>(string kind, Guid terminalId, string key, T fingerprintData,
        Func<PosTerminal, CancellationToken, Task<Guid>> operation, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 80)
            throw new CashOperationException("INVALID_IDEMPOTENCY_KEY", "La operación requiere una clave de idempotencia válida.", 422);
        var terminal = await Terminal(terminalId, ct);
        var scope = Hash($"{terminal.BranchId}:{user.UserId}:{user.UserName}:{kind}:{key}");
        var fingerprint = Hash(JsonSerializer.Serialize(fingerprintData));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"pos-cash-command:" + scope}, 0))", ct);
        // Serialize POS cash commands; the cash xmin token also arbitrates sales/refunds and legacy cash screens.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"pos-cash:" + terminal.BranchId + ":" + terminal.RegisterCode}, 0))", ct);
        db.ChangeTracker.Clear();
        terminal = await Terminal(terminalId, ct);
        var receipt = await db.Set<CashCommandReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.ScopeKey == scope, ct);
        if (receipt is not null)
        {
            if (receipt.Fingerprint != fingerprint) throw new CashOperationException("IDEMPOTENCY_CONFLICT", "La clave ya fue utilizada con otros datos.");
            await tx.CommitAsync(ct);
            return new(receipt.SessionId, true, await StateFor(terminal.Id, receipt.SessionId, ct));
        }
        Guid id;
        try { id = await operation(terminal, ct); }
        catch (DomainRuleException ex) when (kind == "open" && (ex.Message.Contains("sesión activa") || ex.Message.Contains("caja abierta")))
        { throw new CashOperationException("CASH_ALREADY_OPEN", ex.Message); }
        db.Add(new CashCommandReceipt { ScopeKey = scope, Fingerprint = fingerprint, SessionId = id, CompletedAtUtc = clock.UtcNow });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(id, false, await StateFor(terminal.Id, id, ct));
    }
    public async Task<PosCashMutationResult> OpenAsync(PosCashOpen r, string key, CancellationToken ct = default)
    {
        await Require(PermissionCodes.CashOpen, ct);
        Money(r.OpeningAmount);
        return await Execute("open", r.TerminalId, key, new { r.TerminalId, r.OpeningAmount, r.Note, Pos = (r.PosDeviceIds ?? []).Order().ToArray() }, async (t, token) =>
        {
            var branch = await db.Branches.AsNoTracking().SingleAsync(x => x.Id == t.BranchId, token);
            var maximum = options.Value.For(branch.CompanyId, branch.Id).MaximumOpeningAmount;
            if (maximum.HasValue && r.OpeningAmount > maximum.Value)
                throw new CashOperationException("OPENING_LIMIT", "El fondo supera el límite configurado.", 422);
            if (await Sessions(t).AnyAsync(x => x.Status == CashSessionStatus.Active, token))
                throw new CashOperationException("CASH_ALREADY_OPEN", "La terminal ya tiene una caja abierta.");
            await Password(PermissionCodes.CashOpenRequirePassword, r.Password, token);
            return await cash.OpenSessionAsync(new(t.RegisterCode, r.OpeningAmount, r.Note, r.PosDeviceIds,
                r.PosDeviceIds?.Count > 0, t.Id, branch.TimeZone), token);
        }, ct);
    }
    public async Task<PosCashMutationResult> MovementAsync(PosCashMovement r, string key, CancellationToken ct = default)
    {
        Money(r.Amount, false);
        if (string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Length > 500 || r.Reference?.Length > 100)
            throw new CashOperationException("INVALID_REASON", "Indica un motivo de hasta 500 caracteres y una referencia de hasta 100.", 422);
        await Require(r.IsIncome ? PermissionCodes.CashMovementsAddIncome : PermissionCodes.CashMovementsAddExpense, ct);
        return await Execute("movement", r.TerminalId, key, new { r.TerminalId, r.SessionId, r.IsIncome, r.Amount, r.Reason, r.Reference, r.ReversalOfId }, async (t, token) =>
        {
            var session = await Owned(r.SessionId, t, token);
            if (r.ReversalOfId.HasValue)
            {
                var original = session.Movements.SingleOrDefault(x => x.Id == r.ReversalOfId && x.SourceType != "CashMovementReversal");
                if (original is null || original.Type is not CashMovementType.OtherIncome and not CashMovementType.Expense
                    || original.Amount != r.Amount || r.IsIncome != (original.Type == CashMovementType.Expense)
                    || session.Movements.Any(x => x.SourceType == "CashMovementReversal" && x.SourceId == original.Id))
                    throw new CashOperationException("INVALID_REVERSAL", "El movimiento ya fue reversado o no admite este reverso.");
            }
            await cash.RecordMovementAsync(session.Id, new(r.IsIncome ? CashMovementType.OtherIncome : CashMovementType.Expense,
                r.Amount, r.Reason, r.AuthorizerUserName, r.AuthorizerPassword,
                r.ReversalOfId.HasValue ? "CashMovementReversal" : "PosCashManual", r.ReversalOfId, r.Reference), token);
            return session.Id;
        }, ct);
    }
    public async Task<PosCashMutationResult> CloseAsync(PosCashClose r, string key, CancellationToken ct = default)
    {
        await Require(PermissionCodes.CashCloseOwn, ct);
        Money(r.CountedCash);
        if (r.Note?.Length > 500) throw new CashOperationException("INVALID_REASON", "La observación admite hasta 500 caracteres.", 422);
        return await Execute("close", r.TerminalId, key, new { r.TerminalId, r.SessionId, r.ExpectedRevision, r.CountedCash, r.Note }, async (t, token) =>
        {
            var session = await Owned(r.SessionId, t, token);
            if (Revision(session) != r.ExpectedRevision) throw new CashOperationException("CASH_SESSION_CHANGED", "La caja cambió. Actualiza el resumen antes de cerrar.");
            await Password(PermissionCodes.CashCloseRequirePassword, r.Password, token);
            var branch = await db.Branches.AsNoTracking().SingleAsync(x => x.Id == t.BranchId, token);
            var difference = Math.Abs(r.CountedCash - session.CalculateExpectedCash());
            var threshold = options.Value.For(branch.CompanyId, branch.Id).DifferenceApprovalThreshold;
            await cash.CloseSessionAsync(session.Id, new(r.CountedCash, r.Note, r.AuthorizerUserName, r.AuthorizerPassword,
                threshold.HasValue && difference > threshold.Value), token);
            return session.Id;
        }, ct);
    }
}
