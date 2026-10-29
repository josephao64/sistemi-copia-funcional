using SistemiPos.Domain.Cash;

namespace SistemiPos.Application.Cash;

public sealed record CashMovementDto(
    Guid Id,
    CashMovementType Type,
    decimal Amount,
    string Reason,
    string UserName,
    DateTimeOffset OccurredAtUtc,
    string? AuthorizedByUserName = null,
    string? SourceType = null,
    Guid? SourceId = null,
    string? SourceDocument = null);
