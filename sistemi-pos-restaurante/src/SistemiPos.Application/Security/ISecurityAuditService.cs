namespace SistemiPos.Application.Security;

public interface ISecurityAuditService
{
    Task LogLoginSuccessAsync(string userName, string? ipAddress = null, string? userAgent = null, string? machineName = null, CancellationToken cancellationToken = default);
    Task LogUserNotFoundAsync(string identifier, string? ipAddress = null, string? userAgent = null, string? machineName = null, CancellationToken cancellationToken = default);
    Task LogLoginFailureAsync(string identifier, string reason, string? ipAddress = null, string? userAgent = null, string? machineName = null, CancellationToken cancellationToken = default);
    Task LogAccountLockedOutAsync(string identifier, string? ipAddress = null, string? userAgent = null, string? machineName = null, CancellationToken cancellationToken = default);
    Task LogLogoutAsync(string userName, string? ipAddress = null, string? userAgent = null, string? machineName = null, CancellationToken cancellationToken = default);
    Task LogUserManagementAsync(string actorUserName, string action, string targetUserId, string targetUserName, string details, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SecurityAuditLogEntry>> GetRecentSecurityLogsAsync(int count = 100, CancellationToken cancellationToken = default);
}

public sealed record SecurityAuditLogEntry(
    Guid Id,
    string Module,
    string Action,
    string RecordType,
    string RecordId,
    string? DocumentNumber,
    string? UserName,
    DateTimeOffset OccurredAtUtc,
    string EventDetail,
    string? ChangesJson = null);
