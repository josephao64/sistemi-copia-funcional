using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Sales;

public sealed class SaleReturn : AuditableEntity
{
    private readonly List<SaleReturnItem> _items = [];

    public string Number { get; private set; } = string.Empty;
    public Guid SaleId { get; private set; }
    public string? OperationId { get; private set; }
    public string? RequestFingerprint { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string UserName { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string? AuthorizedByUserName { get; private set; }
    public IReadOnlyCollection<SaleReturnItem> Items => _items;

    private SaleReturn()
    {
    }

    public SaleReturn(string number, Guid saleId, string reason, string userName, DateTimeOffset occurredAtUtc, IEnumerable<SaleReturnItem> items,
        string? authorizedByUserName = null, string? operationId = null, string? requestFingerprint = null)
    {
        if (string.IsNullOrWhiteSpace(number) || saleId == Guid.Empty || string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleException("Los datos de devolución son requeridos.");
        }

        var values = items.ToList();
        if (values.Count == 0)
        {
            throw new DomainRuleException("La devolución debe incluir al menos una línea.");
        }

        Number = number.Trim();
        SaleId = saleId;
        OperationId = string.IsNullOrWhiteSpace(operationId) ? null : operationId.Trim();
        RequestFingerprint = requestFingerprint;
        Reason = reason.Trim();
        UserName = string.IsNullOrWhiteSpace(userName) ? "system" : userName.Trim();
        OccurredAtUtc = occurredAtUtc;
        AuthorizedByUserName = string.IsNullOrWhiteSpace(authorizedByUserName) ? null : authorizedByUserName.Trim();
        _items.AddRange(values);
    }
}
