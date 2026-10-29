using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Audit;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Security;

public sealed class SecurityAuditService(AppDbContext dbContext, IDateTimeProvider dateTimeProvider) : ISecurityAuditService
{
    public async Task LogLoginSuccessAsync(string userName, string? ipAddress = null, string? userAgent = null, string? machineName = null, CancellationToken cancellationToken = default)
    {
        var client = ClientDeviceDetector.Detect(ipAddress, userAgent, machineName);
        var log = new AuditLog
        {
            Module = SecurityAuditActions.Module,
            Action = SecurityAuditActions.LoginSuccess,
            RecordType = "Usuario",
            RecordId = userName,
            DocumentNumber = client.IpDisplay,
            UserName = userName,
            OccurredAtUtc = dateTimeProvider.UtcNow,
            ChangesJson = JsonSerializer.Serialize(new
            {
                Event = "Inicio de sesión correcto",
                IpAddress = client.IpDisplay,
                MachineName = client.MachineName,
                OperatingSystem = client.OperatingSystem,
                Browser = client.Browser,
                DeviceType = client.DeviceType,
                UserAgent = userAgent
            })
        };

        dbContext.AuditLogs.Add(log);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task LogUserNotFoundAsync(string identifier, string? ipAddress = null, string? userAgent = null, string? machineName = null, CancellationToken cancellationToken = default)
    {
        var client = ClientDeviceDetector.Detect(ipAddress, userAgent, machineName);
        var log = new AuditLog
        {
            Module = SecurityAuditActions.Module,
            Action = SecurityAuditActions.UserNotFound,
            RecordType = "Usuario",
            RecordId = identifier,
            DocumentNumber = client.IpDisplay,
            UserName = identifier,
            OccurredAtUtc = dateTimeProvider.UtcNow,
            ChangesJson = JsonSerializer.Serialize(new
            {
                Event = "Intento de inicio de sesión con usuario inexistente",
                IpAddress = client.IpDisplay,
                MachineName = client.MachineName,
                OperatingSystem = client.OperatingSystem,
                Browser = client.Browser,
                DeviceType = client.DeviceType,
                UserAgent = userAgent
            })
        };

        dbContext.AuditLogs.Add(log);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task LogLoginFailureAsync(string identifier, string reason, string? ipAddress = null, string? userAgent = null, string? machineName = null, CancellationToken cancellationToken = default)
    {
        var client = ClientDeviceDetector.Detect(ipAddress, userAgent, machineName);
        var log = new AuditLog
        {
            Module = SecurityAuditActions.Module,
            Action = SecurityAuditActions.LoginFailure,
            RecordType = "Usuario",
            RecordId = identifier,
            DocumentNumber = client.IpDisplay,
            UserName = identifier,
            OccurredAtUtc = dateTimeProvider.UtcNow,
            ChangesJson = JsonSerializer.Serialize(new
            {
                Event = "Intento de inicio de sesión fallido",
                Reason = reason,
                IpAddress = client.IpDisplay,
                MachineName = client.MachineName,
                OperatingSystem = client.OperatingSystem,
                Browser = client.Browser,
                DeviceType = client.DeviceType,
                UserAgent = userAgent
            })
        };

        dbContext.AuditLogs.Add(log);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task LogAccountLockedOutAsync(string identifier, string? ipAddress = null, string? userAgent = null, string? machineName = null, CancellationToken cancellationToken = default)
    {
        var client = ClientDeviceDetector.Detect(ipAddress, userAgent, machineName);
        var log = new AuditLog
        {
            Module = SecurityAuditActions.Module,
            Action = SecurityAuditActions.AccountLockedOut,
            RecordType = "Usuario",
            RecordId = identifier,
            DocumentNumber = client.IpDisplay,
            UserName = identifier,
            OccurredAtUtc = dateTimeProvider.UtcNow,
            ChangesJson = JsonSerializer.Serialize(new
            {
                Event = "Cuenta bloqueada temporalmente por intentos fallidos",
                IpAddress = client.IpDisplay,
                MachineName = client.MachineName,
                OperatingSystem = client.OperatingSystem,
                Browser = client.Browser,
                DeviceType = client.DeviceType,
                UserAgent = userAgent
            })
        };

        dbContext.AuditLogs.Add(log);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task LogLogoutAsync(string userName, string? ipAddress = null, string? userAgent = null, string? machineName = null, CancellationToken cancellationToken = default)
    {
        var client = ClientDeviceDetector.Detect(ipAddress, userAgent, machineName);
        var log = new AuditLog
        {
            Module = SecurityAuditActions.Module,
            Action = SecurityAuditActions.Logout,
            RecordType = "Usuario",
            RecordId = userName,
            DocumentNumber = client.IpDisplay,
            UserName = userName,
            OccurredAtUtc = dateTimeProvider.UtcNow,
            ChangesJson = JsonSerializer.Serialize(new
            {
                Event = "Cierre de sesión",
                IpAddress = client.IpDisplay,
                MachineName = client.MachineName,
                OperatingSystem = client.OperatingSystem,
                Browser = client.Browser,
                DeviceType = client.DeviceType,
                UserAgent = userAgent
            })
        };

        dbContext.AuditLogs.Add(log);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task LogUserManagementAsync(string actorUserName, string action, string targetUserId, string targetUserName, string details, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default)
    {
        var client = ClientDeviceDetector.Detect(ipAddress, userAgent);
        var log = new AuditLog
        {
            Module = SecurityAuditActions.Module,
            Action = action,
            RecordType = "Usuario",
            RecordId = targetUserId,
            DocumentNumber = targetUserName,
            UserName = actorUserName,
            OccurredAtUtc = dateTimeProvider.UtcNow,
            ChangesJson = JsonSerializer.Serialize(new
            {
                TargetUserName = targetUserName,
                Details = details,
                IpAddress = client.IpDisplay,
                MachineName = client.MachineName,
                OperatingSystem = client.OperatingSystem,
                Browser = client.Browser,
                DeviceType = client.DeviceType
            })
        };

        dbContext.AuditLogs.Add(log);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SecurityAuditLogEntry>> GetRecentSecurityLogsAsync(int count = 100, CancellationToken cancellationToken = default)
    {
        var logs = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(x => x.Module == SecurityAuditActions.Module)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(count)
            .ToListAsync(cancellationToken);

        return logs
            .Select(x => new SecurityAuditLogEntry(
                x.Id,
                x.Module,
                x.Action,
                x.RecordType,
                x.RecordId,
                x.DocumentNumber,
                x.UserName,
                x.OccurredAtUtc,
                SecurityAuditEventDetailFormatter.Format(
                    x.Action,
                    x.RecordId,
                    x.UserName,
                    x.ChangesJson),
                x.ChangesJson))
            .ToList();
    }
}
