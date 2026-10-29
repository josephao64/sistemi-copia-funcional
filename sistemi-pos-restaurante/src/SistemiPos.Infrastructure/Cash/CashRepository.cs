using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Cash;
using SistemiPos.Domain.Cash;
using SistemiPos.Infrastructure.Persistence;
using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Audit;

namespace SistemiPos.Infrastructure.Cash;

public sealed class CashRepository(AppDbContext dbContext, SistemiPos.Application.Abstractions.IOperationContextAccessor? context = null) : ICashRepository
{
    public Task<bool> HasPendingPaymentsAsync(Guid sessionId, CancellationToken ct = default) =>
        (from p in dbContext.SalePayments join s in dbContext.Sales on p.SaleId equals s.Id
         where s.CashSessionId == sessionId && (p.CardStatus == SistemiPos.Domain.Sales.CardPaymentStatus.Pending
             || p.TransferStatus == SistemiPos.Domain.Sales.TransferPaymentStatus.Pending)
         select p.Id).AnyAsync(ct);
    public async Task<SystemSetting?> GetSystemSettingAsync(CancellationToken ct = default) =>
        (await dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? SystemSetting.CreateDefault())
            .WithOperationalCurrency(await CashCurrency.ResolveAsync(dbContext, context?.CurrentBranchId, ct));
    public Task<List<CashSession>> GetSessionsAsync(CancellationToken cancellationToken = default) =>
        dbContext.CashSessions
            .Where(x => context == null || !context.CurrentBranchId.HasValue || x.BranchId == context.CurrentBranchId)
            .Include(x => x.Movements)
            .Include(x => x.Counts).ThenInclude(x => x.Lines)
            .Include(x => x.PosClosings)
            .Include(x => x.AssignedPosDevices)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<CashSession?> GetSessionAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.CashSessions
            .Include(x => x.Movements)
            .Include(x => x.Counts).ThenInclude(x => x.Lines)
            .Include(x => x.PosClosings)
            .Include(x => x.AssignedPosDevices)
            .FirstOrDefaultAsync(x => x.Id == id && (context == null || !context.CurrentBranchId.HasValue || x.BranchId == context.CurrentBranchId), cancellationToken);

    public Task<CashSession?> GetActiveSessionByRegisterAsync(string registerCode, CancellationToken cancellationToken = default) =>
        dbContext.CashSessions
            .FirstOrDefaultAsync(
                x => x.RegisterCode.ToUpper() == registerCode.Trim().ToUpper() && x.Status == CashSessionStatus.Active
                    && (context == null || !context.CurrentBranchId.HasValue || x.BranchId == context.CurrentBranchId),
                cancellationToken);

    public Task<CashSession?> GetActiveSessionByUserAsync(Guid? userId, string userName, CancellationToken cancellationToken = default) =>
        dbContext.CashSessions.Include(x => x.Movements).Include(x => x.Counts).ThenInclude(x => x.Lines).Include(x => x.AssignedPosDevices)
            .FirstOrDefaultAsync(x => x.Status == CashSessionStatus.Active &&
                ((userId != null && x.CashierUserId == userId) || x.CashierUserName == userName)
                && (context == null || !context.CurrentBranchId.HasValue || x.BranchId == context.CurrentBranchId), cancellationToken);

    public Task<List<CashDenomination>> GetActiveDenominationsAsync(CancellationToken cancellationToken = default) =>
        dbContext.CashDenominations.Where(x => x.IsActive).OrderByDescending(x => x.Value).ToListAsync(cancellationToken);

    public Task AddFinancialAuditAsync(FinancialAuditEntry entry, CancellationToken cancellationToken = default) =>
        dbContext.FinancialAuditEntries.AddAsync(entry, cancellationToken).AsTask();

    public Task AddMovementAsync(CashMovement movement, CancellationToken cancellationToken = default) =>
        dbContext.CashMovements.AddAsync(movement, cancellationToken).AsTask();

    public Task AddCountAsync(CashCount count, CancellationToken cancellationToken = default) =>
        dbContext.CashCounts.AddAsync(count, cancellationToken).AsTask();

    public Task AddPosClosingAsync(CashPosClosing closing, CancellationToken cancellationToken = default) =>
        dbContext.CashPosClosings.AddAsync(closing, cancellationToken).AsTask();

    public async Task<bool> IsPosPaymentEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SystemSettings
            .Select(x => x.PosPaymentsEnabled)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<UsedPosTotalDto>> GetUsedPosTotalsAsync(Guid cashSessionId, CancellationToken cancellationToken = default) =>
        (from sale in dbContext.Sales
         join payment in dbContext.SalePayments on sale.Id equals payment.SaleId
         join device in dbContext.PosDevices on payment.PosDeviceId equals device.Id
         where sale.CashSessionId == cashSessionId && payment.PosDeviceId != null && sale.Status != SistemiPos.Domain.Sales.SaleStatus.Cancelled
         group payment by new { device.Id, device.Name } into values
         select new UsedPosTotalDto(values.Key.Id, values.Key.Name, values.Sum(x => x.CardAmount ?? x.TotalAmount))).ToListAsync(cancellationToken);

    public Task<List<PosDevice>> GetActivePosDevicesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        dbContext.PosDevices.Where(x => x.IsActive && ids.Contains(x.Id)).ToListAsync(cancellationToken);

    public Task<List<Guid>> GetPosDeviceIdsAssignedToActiveSessionsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        (from assignment in dbContext.CashSessionPosDevices
         join session in dbContext.CashSessions on assignment.CashSessionId equals session.Id
         where session.Status == CashSessionStatus.Active && ids.Contains(assignment.PosDeviceId)
         select assignment.PosDeviceId).Distinct().ToListAsync(cancellationToken);

    public Task AddSessionAsync(CashSession session, CancellationToken cancellationToken = default) =>
        dbContext.CashSessions.AddAsync(session, cancellationToken).AsTask();
}
