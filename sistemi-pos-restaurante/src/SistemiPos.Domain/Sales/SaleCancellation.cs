using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Sales;

public sealed class SaleCancellation : AuditableEntity
{
    public string Number { get; private set; } = string.Empty;
    public Guid SaleId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string UserName { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string? AuthorizedByUserName { get; private set; }

    private SaleCancellation()
    {
    }

    public SaleCancellation(string number, Guid saleId, string reason, string userName, DateTimeOffset occurredAtUtc, string? authorizedByUserName = null)
    {
        if (string.IsNullOrWhiteSpace(number) || saleId == Guid.Empty || string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleException("Los datos de anulación son requeridos.");
        }

        Number = number.Trim();
        SaleId = saleId;
        Reason = reason.Trim();
        UserName = string.IsNullOrWhiteSpace(userName) ? "system" : userName.Trim();
        OccurredAtUtc = occurredAtUtc;
        AuthorizedByUserName = string.IsNullOrWhiteSpace(authorizedByUserName) ? null : authorizedByUserName.Trim();
    }
}
