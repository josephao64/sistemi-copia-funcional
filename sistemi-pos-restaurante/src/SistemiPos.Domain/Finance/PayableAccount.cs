using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Finance;

public sealed class PayableAccount : AuditableEntity
{
    private readonly List<PayablePayment> _payments = [];
    public Guid PurchaseId { get; private set; }
    public Guid SupplierId { get; private set; }
    public string InvoiceNumber { get; private set; } = string.Empty;
    public DateTimeOffset InvoiceDateUtc { get; private set; }
    public DateTimeOffset DueDateUtc { get; private set; }
    public decimal Total { get; private set; }
    public decimal Paid { get; private set; }
    public decimal Balance { get; private set; }
    public AccountStatus Status { get; private set; }
    public uint Version { get; private set; }
    public IReadOnlyCollection<PayablePayment> Payments => _payments;
    private PayableAccount() { }
    public PayableAccount(Guid purchaseId, Guid supplierId, string invoiceNumber, DateTimeOffset invoiceDateUtc, DateTimeOffset dueDateUtc, decimal total)
    {
        if (purchaseId == Guid.Empty || supplierId == Guid.Empty || string.IsNullOrWhiteSpace(invoiceNumber) || total <= 0)
            throw new DomainRuleException("Los datos de la cuenta por pagar no son válidos.");
        if (dueDateUtc < invoiceDateUtc) throw new DomainRuleException("El vencimiento no puede ser anterior a la factura.");
        PurchaseId = purchaseId; SupplierId = supplierId; InvoiceNumber = invoiceNumber.Trim(); InvoiceDateUtc = invoiceDateUtc; DueDateUtc = dueDateUtc;
        Total = decimal.Round(total, 2); Balance = Total; Status = AccountStatus.Pending;
    }
    public void AddPayment(PayablePayment payment)
    {
        if (Status is AccountStatus.Cancelled or AccountStatus.Paid) throw new DomainRuleException("La cuenta no admite más pagos.");
        if (payment.Amount > Balance) throw new DomainRuleException("El pago no puede superar el saldo pendiente.");
        _payments.Add(payment); Paid = decimal.Round(Paid + payment.Amount, 2); Balance = decimal.Round(Total - Paid, 2);
        Status = Balance == 0 ? AccountStatus.Paid : AccountStatus.PartiallyPaid;
    }
    public void Cancel() { if (Paid > 0) throw new DomainRuleException("No se puede anular una cuenta con pagos."); Status = AccountStatus.Cancelled; Balance = 0; }
}

public sealed class PayablePayment : AuditableEntity
{
    public Guid PayableAccountId { get; set; }
    public DateTimeOffset PaidAtUtc { get; private set; }
    public decimal Amount { get; private set; }
    public FinancialPaymentMethod PaymentMethod { get; private set; }
    public MoneySourceType MoneySource { get; private set; }
    public Guid? SourceAccountId { get; private set; }
    public string? Reference { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string? AuthorizedByUserName { get; private set; }
    public Guid? CashMovementId { get; set; }
    public Guid? PettyCashMovementId { get; set; }
    private PayablePayment() { }
    public PayablePayment(DateTimeOffset paidAtUtc, decimal amount, FinancialPaymentMethod paymentMethod, MoneySourceType moneySource,
        Guid? sourceAccountId, string? reference, string userName, string? authorizedByUserName)
    {
        if (amount <= 0) throw new DomainRuleException("El pago debe ser mayor a cero.");
        Amount = decimal.Round(amount, 2); PaidAtUtc = paidAtUtc; PaymentMethod = paymentMethod; MoneySource = moneySource; SourceAccountId = sourceAccountId;
        Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(); UserName = string.IsNullOrWhiteSpace(userName) ? "system" : userName.Trim();
        AuthorizedByUserName = string.IsNullOrWhiteSpace(authorizedByUserName) ? null : authorizedByUserName.Trim();
    }
}
