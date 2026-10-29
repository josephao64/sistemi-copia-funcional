namespace SistemiPos.Application.Sales;

public interface ISaleService
{
    Task<IReadOnlyList<SaleDto>> GetSalesAsync(CancellationToken cancellationToken = default);
    Task<SaleDto?> GetSaleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateSaleAsync(CreateSaleRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentMethodOption>> GetOtherPaymentMethodsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PaymentMethodOption>>([]);
    Task<IReadOnlyList<SistemiPos.Application.Orders.TerminalDto>> GetPosTerminalsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SistemiPos.Application.Orders.TerminalDto>>([]);
    Task<OfflinePosTicket> CreateOfflineTicketAsync(Guid sessionId, Guid? terminalId = null, CancellationToken ct = default) => throw new NotSupportedException();
    Task<IReadOnlyDictionary<Guid, decimal>> GetOfflineStocksAsync(string ticket, CancellationToken ct = default) => throw new NotSupportedException();
    Task<CheckoutQuote> QuoteAsync(IReadOnlyList<SaleItemRequest> items, Guid? orderId = null, CancellationToken ct = default, int? people = null) => throw new NotSupportedException();
    Task<Guid> RequestOrderDiscountApprovalAsync(Guid cashSessionId, Guid orderId, IReadOnlyList<SaleItemRequest> items, CancellationToken ct = default) => throw new NotSupportedException();
    Task<Guid> RequestDiscountApprovalAsync(Guid cashSessionId, IReadOnlyList<SaleItemRequest> items, CancellationToken cancellationToken = default);
    Task<DiscountApprovalDto?> GetDiscountApprovalAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DiscountApprovalDto>> GetPendingDiscountApprovalsAsync(CancellationToken cancellationToken = default);
    Task DecideDiscountApprovalAsync(Guid id, bool approve, string? comment, CancellationToken cancellationToken = default);
    Task CancelSaleAsync(Guid saleId, CancelSaleRequest request, CancellationToken cancellationToken = default);
    Task ReturnSaleAsync(Guid saleId, ReturnSaleRequest request, CancellationToken cancellationToken = default);
    Task RefundSaleAsync(Guid saleId, RefundSaleRequest request, CancellationToken cancellationToken = default);
    Task VerifyTransferAsync(Guid saleId, string? observation, CancellationToken cancellationToken = default);
    Task RejectTransferAsync(Guid saleId, string? observation, CancellationToken cancellationToken = default);
}
