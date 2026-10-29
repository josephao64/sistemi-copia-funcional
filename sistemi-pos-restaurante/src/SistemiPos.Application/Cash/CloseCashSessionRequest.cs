namespace SistemiPos.Application.Cash;

public sealed record CloseCashSessionRequest(
    decimal CountedCash,
    string? Observation = null,
    string? AuthorizerUserName = null,
    string? AuthorizerPassword = null,
    bool RequireAuthorization = false);
