using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Finance;

public sealed class ReceivableAccount : AuditableEntity
{
    private readonly List<ReceivablePayment> _payments = [];
    public Guid SaleId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string SaleNumber { get; private set; } = string.Empty;
    public DateTimeOffset SaleDateUtc { get; private set; }
    public DateTimeOffset DueDateUtc { get; private set; }
    public int CreditDays { get; private set; }
    public int GraceDays { get; private set; }
    public decimal Total { get; private set; }
    public decimal Paid { get; private set; }
    public decimal Balance { get; private set; }
    public AccountStatus Status { get; private set; }
    public IReadOnlyCollection<ReceivablePayment> Payments => _payments;
    private ReceivableAccount() { }
    public ReceivableAccount(Guid saleId, Guid customerId, string saleNumber, DateTimeOffset saleDateUtc, DateTimeOffset dueDateUtc,
        decimal total, int? creditDays = null, int graceDays = 0)
    {
        if (saleId == Guid.Empty || customerId == Guid.Empty || string.IsNullOrWhiteSpace(saleNumber) || total <= 0)
            throw new DomainRuleException("Los datos de la cuenta por cobrar no son válidos.");
        if (dueDateUtc < saleDateUtc) throw new DomainRuleException("El vencimiento no puede ser anterior a la venta.");
        if (creditDays < 0 || graceDays < 0) throw new DomainRuleException("Los días de crédito y de gracia no pueden ser negativos.");
        SaleId = saleId; CustomerId = customerId; SaleNumber = saleNumber.Trim(); SaleDateUtc = saleDateUtc; DueDateUtc = dueDateUtc;
        CreditDays = creditDays ?? Math.Max(0, (dueDateUtc.Date - saleDateUtc.Date).Days); GraceDays = graceDays;
        Total = decimal.Round(total, 2); Balance = Total; Status = AccountStatus.Pending;
    }
    public void AddPayment(ReceivablePayment payment)
    {
        if (Status is AccountStatus.Cancelled or AccountStatus.Paid) throw new DomainRuleException("La cuenta no admite más abonos.");
        if (payment.Amount > Balance) throw new DomainRuleException("El abono no puede superar el saldo pendiente.");
        _payments.Add(payment); Paid = decimal.Round(Paid + payment.Amount, 2); Balance = decimal.Round(Total - Paid, 2);
        Status = Balance == 0 ? AccountStatus.Paid : AccountStatus.PartiallyPaid;
    }
    public void ApplyCredit(decimal amount)
    {
        if (amount <= 0 || amount > Balance) throw new DomainRuleException("El ajuste de crédito no es válido.");
        Paid = decimal.Round(Paid + amount, 2); Balance = decimal.Round(Total - Paid, 2); Status = Balance == 0 ? AccountStatus.Paid : AccountStatus.PartiallyPaid;
    }
    public void Cancel() { if (Paid > 0) throw new DomainRuleException("No se puede anular una cuenta con abonos."); Status = AccountStatus.Cancelled; }
}

public sealed class ReceivablePayment : AuditableEntity
{
    public Guid ReceivableAccountId { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; private set; }
    public decimal Amount { get; private set; }
    public FinancialPaymentMethod PaymentMethod { get; private set; }
    public string? Reference { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string? AuthorizedByUserName { get; private set; }
    public Guid? CashSessionId { get; private set; }
    public Guid? CashMovementId { get; set; }
    private ReceivablePayment() { }
    public ReceivablePayment(DateTimeOffset at, decimal amount, FinancialPaymentMethod method, string? reference, string userName, string? authorizedBy, Guid? cashSessionId)
    {
        if (amount <= 0) throw new DomainRuleException("El abono debe ser mayor a cero.");
        if (method == FinancialPaymentMethod.Cash && cashSessionId is null) throw new DomainRuleException("Una caja abierta es requerida para recibir efectivo.");
        ReceivedAtUtc = at; Amount = decimal.Round(amount, 2); PaymentMethod = method; Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        UserName = string.IsNullOrWhiteSpace(userName) ? "system" : userName.Trim(); AuthorizedByUserName = string.IsNullOrWhiteSpace(authorizedBy) ? null : authorizedBy.Trim(); CashSessionId = cashSessionId;
    }
}
