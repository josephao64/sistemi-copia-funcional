using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Cash;

public sealed class CashMovement : AuditableEntity
{
    public Guid CashSessionId { get; set; }
    public CashMovementType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string UserName { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string? AuthorizedByUserName { get; private set; }
    public string? SourceType { get; private set; }
    public Guid? SourceId { get; private set; }
    public string? SourceDocument { get; private set; }

    private CashMovement()
    {
    }

    public CashMovement(
        CashMovementType type,
        decimal amount,
        string reason,
        string userName,
        DateTimeOffset occurredAtUtc,
        string? authorizedByUserName = null,
        string? sourceType = null,
        Guid? sourceId = null,
        string? sourceDocument = null)
    {
        if (amount <= 0)
        {
            throw new DomainRuleException("El monto debe ser mayor a cero.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleException("El motivo es requerido.");
        }

        Type = type;
        Amount = decimal.Round(amount, 2);
        Reason = reason.Trim();
        UserName = string.IsNullOrWhiteSpace(userName) ? "system" : userName.Trim();
        OccurredAtUtc = occurredAtUtc;
        AuthorizedByUserName = Normalize(authorizedByUserName);
        SourceType = Normalize(sourceType);
        SourceId = sourceId;
        SourceDocument = Normalize(sourceDocument);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
