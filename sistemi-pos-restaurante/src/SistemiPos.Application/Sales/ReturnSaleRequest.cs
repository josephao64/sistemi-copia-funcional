namespace SistemiPos.Application.Sales;

public sealed record ReturnSaleRequest(
    string Reason,
    IReadOnlyList<ReturnSaleItemRequest> Items,
    string? AuthorizerUserName = null,
    string? AuthorizerPassword = null,
    string? OperationId = null);
