using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Audit;

namespace SistemiPos.Application.Cash;

public interface ICashRepository
{
    Task<bool> HasPendingPaymentsAsync(Guid sessionId, CancellationToken ct = default) => Task.FromResult(false);
    Task<SystemSetting?> GetSystemSettingAsync(CancellationToken ct = default) => Task.FromResult<SystemSetting?>(null);
    Task<List<CashSession>> GetSessionsAsync(CancellationToken cancellationToken = default);
    Task<CashSession?> GetSessionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CashSession?> GetActiveSessionByRegisterAsync(string registerCode, CancellationToken cancellationToken = default);
    Task<CashSession?> GetActiveSessionByUserAsync(Guid? userId, string userName, CancellationToken cancellationToken = default) => Task.FromResult<CashSession?>(null);
    Task<List<CashDenomination>> GetActiveDenominationsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<CashDenomination>());
    Task AddFinancialAuditAsync(FinancialAuditEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task AddMovementAsync(CashMovement movement, CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task AddCountAsync(CashCount count, CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task AddPosClosingAsync(CashPosClosing closing, CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task<bool> IsPosPaymentEnabledAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    Task<List<UsedPosTotalDto>> GetUsedPosTotalsAsync(Guid cashSessionId, CancellationToken cancellationToken = default) => Task.FromResult(new List<UsedPosTotalDto>());
    Task<List<PosDevice>> GetActivePosDevicesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => Task.FromResult(new List<PosDevice>());
    Task<List<Guid>> GetPosDeviceIdsAssignedToActiveSessionsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => Task.FromResult(new List<Guid>());
    Task AddSessionAsync(CashSession session, CancellationToken cancellationToken = default);
}
