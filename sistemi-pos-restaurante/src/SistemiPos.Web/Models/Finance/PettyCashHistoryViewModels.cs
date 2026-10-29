using SistemiPos.Application.Finance;

namespace SistemiPos.Web.Models.Finance;

public sealed record PettyCashHistoryRow(Guid Id, string FundName, string ResponsibleUserName, DateTimeOffset OpenedAtUtc,
    DateTimeOffset? ClosedAtUtc, decimal InitialBalance, decimal? ExpectedBalance, decimal? CountedBalance, decimal? Difference);

public sealed record PettyCashCycleHistoryViewModel(PettyCashHistoryRow Cycle, IReadOnlyList<PettyCashMovementDto> Movements);
