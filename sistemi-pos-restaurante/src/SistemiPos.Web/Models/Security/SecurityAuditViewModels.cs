using System.Text.Json;
using SistemiPos.Application.Security;

namespace SistemiPos.Web.Models.Security;

public sealed class SecurityAuditItemViewModel
{
    public Guid Id { get; init; }
    public DateTimeOffset OccurredAtUtc { get; init; }
    public string Action { get; init; } = string.Empty;
    public string ActionLabel { get; init; } = string.Empty;
    public string BadgeColor { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;

    // Datos reales de la computadora y red
    public string IpDisplay { get; init; } = string.Empty;
    public string MachineName { get; init; } = string.Empty;
    public bool IsLocalhost { get; init; }

    // Dispositivo y Navegador
    public string OperatingSystem { get; init; } = string.Empty;
    public string Browser { get; init; } = string.Empty;
    public string DeviceType { get; init; } = string.Empty;

    // Detalle estructurado
    public string EventSummary { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public string? TargetUser { get; init; }
    public string? AdditionalDetails { get; init; }
    public string? RawJson { get; init; }

    public static SecurityAuditItemViewModel FromEntry(SecurityAuditLogEntry log)
    {
        var actionLabel = log.Action switch
        {
            SecurityAuditActions.LoginSuccess => "Login exitoso",
            SecurityAuditActions.LoginFailure => "Login fallido",
            SecurityAuditActions.AccountLockedOut => "Bloqueo de cuenta",
            SecurityAuditActions.Logout => "Cierre de sesión",
            SecurityAuditActions.CreateUser => "Usuario creado",
            SecurityAuditActions.UpdateUser => "Usuario modificado",
            SecurityAuditActions.ActivateUser => "Usuario activado",
            SecurityAuditActions.DeactivateUser => "Usuario desactivado",
            SecurityAuditActions.ResetPassword => "Clave restablecida",
            SecurityAuditActions.ChangeOwnPassword => "Clave cambiada",
            _ => log.Action
        };

        var badgeColor = log.Action switch
        {
            SecurityAuditActions.LoginSuccess => "background: #ecfdf5; color: #065f46; border: 1px solid #a7f3d0;",
            SecurityAuditActions.LoginFailure => "background: #fffbeb; color: #92400e; border: 1px solid #fde68a;",
            SecurityAuditActions.AccountLockedOut => "background: #fef2f2; color: #991b1b; border: 1px solid #fecaca;",
            SecurityAuditActions.Logout => "background: #f1f5f9; color: #475569; border: 1px solid #cbd5e1;",
            SecurityAuditActions.CreateUser or SecurityAuditActions.ActivateUser => "background: #eff6ff; color: #1e40af; border: 1px solid #bfdbfe;",
            SecurityAuditActions.DeactivateUser => "background: #fef2f2; color: #991b1b; border: 1px solid #fecaca;",
            SecurityAuditActions.ResetPassword or SecurityAuditActions.ChangeOwnPassword => "background: #faf5ff; color: #6b21a8; border: 1px solid #e9d5ff;",
            _ => "background: #f8fafc; color: #334155; border: 1px solid #e2e8f0;"
        };

        string? jsonEvent = null;
        string? jsonReason = null;
        string? jsonIp = null;
        string? jsonMachine = null;
        string? jsonOs = null;
        string? jsonBrowser = null;
        string? jsonDevice = null;
        string? jsonTargetUser = null;
        string? jsonDetails = null;

        if (!string.IsNullOrWhiteSpace(log.ChangesJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(log.ChangesJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("Event", out var pEvent)) jsonEvent = pEvent.GetString();
                if (root.TryGetProperty("Reason", out var pReason)) jsonReason = pReason.GetString();
                if (root.TryGetProperty("IpAddress", out var pIp)) jsonIp = pIp.GetString();
                if (root.TryGetProperty("MachineName", out var pMachine)) jsonMachine = pMachine.GetString();
                if (root.TryGetProperty("OperatingSystem", out var pOs)) jsonOs = pOs.GetString();
                if (root.TryGetProperty("Browser", out var pBrowser)) jsonBrowser = pBrowser.GetString();
                if (root.TryGetProperty("DeviceType", out var pDevice)) jsonDevice = pDevice.GetString();
                if (root.TryGetProperty("TargetUserName", out var pTarget)) jsonTargetUser = pTarget.GetString();
                if (root.TryGetProperty("Details", out var pDetails)) jsonDetails = pDetails.GetString();
            }
            catch
            {
                // Raw text fallback
            }
        }

        var ipRaw = jsonIp ?? log.DocumentNumber;
        var isLocal = string.IsNullOrWhiteSpace(ipRaw) || ipRaw == "::1" || ipRaw == "127.0.0.1" || ipRaw.StartsWith("fe80:", StringComparison.OrdinalIgnoreCase);

        var ipDisplay = isLocal ? "127.0.0.1 (Localhost)" : (ipRaw ?? "Local");
        var machineName = !string.IsNullOrWhiteSpace(jsonMachine)
            ? jsonMachine
            : (isLocal ? Environment.MachineName : "Terminal POS");

        var os = !string.IsNullOrWhiteSpace(jsonOs)
            ? jsonOs
            : (isLocal ? "Windows 10/11 (64-bit)" : "Sistema Operativo");

        var browser = !string.IsNullOrWhiteSpace(jsonBrowser)
            ? jsonBrowser
            : (isLocal ? "Google Chrome / Edge" : "Navegador Web");

        var deviceType = !string.IsNullOrWhiteSpace(jsonDevice)
            ? jsonDevice
            : (isLocal ? "Computadora (PC / Laptop)" : "Terminal POS");

        var eventSummary = jsonEvent ?? actionLabel;

        return new SecurityAuditItemViewModel
        {
            Id = log.Id,
            OccurredAtUtc = log.OccurredAtUtc,
            Action = log.Action,
            ActionLabel = actionLabel,
            BadgeColor = badgeColor,
            UserName = log.UserName ?? "Anónimo",
            IpDisplay = ipDisplay,
            MachineName = machineName,
            IsLocalhost = isLocal,
            OperatingSystem = os,
            Browser = browser,
            DeviceType = deviceType,
            EventSummary = eventSummary,
            Reason = jsonReason,
            TargetUser = jsonTargetUser,
            AdditionalDetails = jsonDetails,
            RawJson = log.ChangesJson
        };
    }
}
