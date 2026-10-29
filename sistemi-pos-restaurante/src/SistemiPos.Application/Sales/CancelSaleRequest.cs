namespace SistemiPos.Application.Sales;

public sealed record CancelSaleRequest(string Reason, string? AuthorizerUserName = null, string? AuthorizerPassword = null);
