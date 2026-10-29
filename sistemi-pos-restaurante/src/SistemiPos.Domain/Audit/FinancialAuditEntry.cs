using SistemiPos.Domain.Common;

namespace SistemiPos.Domain.Audit;

public sealed class FinancialAuditEntry : Entity
{
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public Guid? CashSessionId { get; set; }
    public string? DocumentType { get; set; }
    public Guid? DocumentId { get; set; }
    public string? DocumentReference { get; set; }
    public decimal? Amount { get; set; }
    public string? AuthorizedByUserName { get; set; }
    public string? Comment { get; set; }
}
