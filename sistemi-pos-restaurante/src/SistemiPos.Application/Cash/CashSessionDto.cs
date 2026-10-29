using SistemiPos.Domain.Cash;

namespace SistemiPos.Application.Cash;

public sealed record CashSessionDto(
    Guid Id,
    string RegisterCode,
    string CashierUserName,
    DateTimeOffset OpenedAtUtc,
    decimal OpeningAmount,
    CashSessionStatus Status,
    DateTimeOffset? ClosedAtUtc,
    decimal? CountedCash,
    decimal? TotalSold,
    decimal? TotalCashSales,
    decimal? TotalTransferSales,
    decimal? TotalPosSales,
    decimal? OtherIncomeTotal,
    decimal? ExpenseTotal,
    decimal? ExpectedCash,
    decimal? Difference,
    IReadOnlyList<CashMovementDto> Movements,
    IReadOnlyList<CashCountDto>? Counts = null,
    string? OpeningObservation = null,
    string? ClosingObservation = null,
    string? DifferenceAuthorizedByUserName = null,
    IReadOnlyList<CashPosClosingDto>? PosClosings = null,
    DateTimeOffset? ReconciledAtUtc = null,
    string? ReconciledByUserName = null,
    string? ReconciliationObservation = null,
    IReadOnlyList<CashSessionPosDeviceDto>? AssignedPosDevices = null,
    Guid? BranchId = null,
    decimal? TotalOtherSales = null,
    decimal? TotalOtherRefunds = null,
    string? CurrencySnapshotJson = null);

public sealed record CashSessionPosDeviceDto(Guid PosDeviceId, string PosDeviceName);
