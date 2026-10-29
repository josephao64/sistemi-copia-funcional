using SistemiPos.Domain.Cash;

namespace SistemiPos.Application.Cash;

public sealed record RecordCashMovementRequest(
    CashMovementType Type,
    decimal Amount,
    string Reason,
    string? AuthorizerUserName = null,
    string? AuthorizerPassword = null,
    string? SourceType = null,
    Guid? SourceId = null,
    string? SourceDocument = null);
