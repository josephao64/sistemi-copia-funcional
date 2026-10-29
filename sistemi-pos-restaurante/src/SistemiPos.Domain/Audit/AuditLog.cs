using SistemiPos.Domain.Common;

namespace SistemiPos.Domain.Audit;

public sealed class AuditLog : Entity
{
    public Guid? BusinessGroupId { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? TerminalId { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string RecordType { get; set; } = string.Empty;
    public string RecordId { get; set; } = string.Empty;
    public string? DocumentNumber { get; set; }
    public string? UserName { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string? ChangesJson { get; set; }
}
