using SistemiPos.Domain.Common;

namespace SistemiPos.Domain.Catalog;

public sealed class CatalogChangeLog : Entity
{
    public Guid ProductId { get; private set; }
    public string ProductInternalCode { get; private set; } = string.Empty;
    public Guid? BranchId { get; private set; }
    public string Field { get; private set; } = string.Empty;
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public string Actor { get; private set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; private set; }
    public string? Reason { get; private set; }

    private CatalogChangeLog()
    {
    }

    public CatalogChangeLog(
        Guid productId,
        string productInternalCode,
        string field,
        string? oldValue,
        string? newValue,
        string actor,
        DateTimeOffset timestampUtc,
        Guid? branchId = null,
        string? reason = null)
    {
        ProductId = productId;
        ProductInternalCode = productInternalCode;
        Field = field;
        OldValue = oldValue;
        NewValue = newValue;
        Actor = string.IsNullOrWhiteSpace(actor) ? "Sistema" : actor;
        TimestampUtc = timestampUtc;
        BranchId = branchId;
        Reason = reason;
    }
}
