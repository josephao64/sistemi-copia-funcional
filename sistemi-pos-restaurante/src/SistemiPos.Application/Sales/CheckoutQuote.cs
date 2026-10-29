namespace SistemiPos.Application.Sales;

public sealed record CheckoutQuoteLine(Guid ProductId, Guid? OrderLineId, string Name, decimal Quantity,
    decimal Gross, decimal Discount, decimal Total);
public sealed record CheckoutQuote(IReadOnlyList<CheckoutQuoteLine> Lines, decimal Total, IReadOnlyList<decimal>? Shares = null);
public sealed record PaymentMethodOption(Guid Id, string Name);
