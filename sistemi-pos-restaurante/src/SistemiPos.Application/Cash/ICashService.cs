namespace SistemiPos.Application.Cash;

public interface ICashService
{
    Task<IReadOnlyList<CashSessionDto>> GetSessionsAsync(CancellationToken cancellationToken = default);
    Task<CashSessionDto?> GetSessionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CashCutDto> GetCutAsync(Guid id, bool final, CancellationToken ct = default) => throw new NotSupportedException();
    Task<Guid> OpenSessionAsync(OpenCashSessionRequest request, CancellationToken cancellationToken = default);
    Task CloseSessionAsync(Guid id, CloseCashSessionRequest request, CancellationToken cancellationToken = default);
    Task RecordMovementAsync(Guid id, RecordCashMovementRequest request, CancellationToken cancellationToken = default);
    Task RecordAdjustmentAsync(Guid id, RecordCashMovementRequest request, CancellationToken cancellationToken = default);
    Task ReconcileSessionAsync(Guid id, string? observation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    Task<CashSessionDto?> GetCurrentSessionAsync(CancellationToken cancellationToken = default) => Task.FromResult<CashSessionDto?>(null);
    Task<CashCountDto> RecordCountAsync(Guid id, RecordCashCountRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    Task<IReadOnlyList<UsedPosTotalDto>> GetUsedPosTotalsAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<UsedPosTotalDto>>([]);
    Task RecordPosClosingAsync(Guid id, RecordPosClosingRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
