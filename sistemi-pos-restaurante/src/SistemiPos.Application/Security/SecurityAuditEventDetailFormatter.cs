using System.Text;
using System.Text.Json;

namespace SistemiPos.Application.Security;

public static class SecurityAuditEventDetailFormatter
{
    public static string Format(
        string action,
        string recordId,
        string? actorUserName,
        string? changesJson)
    {
        var identifier = FirstValue(recordId, actorUserName, "desconocido");
        var actor = FirstValue(actorUserName, "Sistema");
        var targetUserName = ReadValue(changesJson, "TargetUserName") ?? identifier;
        var reason = ReadValue(changesJson, "Reason");
        var details = ReadValue(changesJson, "Details");

        return action switch
        {
            SecurityAuditActions.LoginSuccess =>
                $"El usuario {identifier} inició sesión correctamente.",
            SecurityAuditActions.UserNotFound =>
                $"Se intentó iniciar sesión con el usuario {identifier}, pero el usuario no existe.",
            SecurityAuditActions.LoginFailure when IsUserNotFound(reason) =>
                $"Se intentó iniciar sesión con el usuario {identifier}, pero el usuario no existe.",
            SecurityAuditActions.LoginFailure when IsIncorrectPassword(reason) =>
                $"El usuario {identifier} intentó iniciar sesión, pero la contraseña ingresada es incorrecta.",
            SecurityAuditActions.LoginFailure when IsInactiveAccount(reason) =>
                $"El usuario {identifier} intentó iniciar sesión, pero su cuenta está desactivada.",
            SecurityAuditActions.LoginFailure when IsMissingAuthorization(reason) =>
                $"El usuario {identifier} intentó iniciar sesión, pero no tiene completa su configuración de acceso.",
            SecurityAuditActions.LoginFailure =>
                $"El usuario {identifier} intentó iniciar sesión, pero no fue posible validar sus credenciales.",
            SecurityAuditActions.AccountLockedOut =>
                $"La cuenta del usuario {identifier} fue bloqueada temporalmente por varios intentos fallidos.",
            SecurityAuditActions.Logout =>
                $"El usuario {identifier} cerró sesión correctamente.",
            SecurityAuditActions.CreateUser =>
                AppendDetails($"El usuario {actor} creó al usuario {targetUserName}.", details),
            SecurityAuditActions.UpdateUser =>
                AppendDetails($"El usuario {actor} modificó al usuario {targetUserName}.", details),
            SecurityAuditActions.ActivateUser =>
                AppendDetails($"El usuario {actor} activó al usuario {targetUserName}.", details),
            SecurityAuditActions.DeactivateUser =>
                AppendDetails($"El usuario {actor} desactivó al usuario {targetUserName}.", details),
            SecurityAuditActions.ResetPassword =>
                $"El usuario {actor} restableció la contraseña del usuario {targetUserName}.",
            SecurityAuditActions.ChangeOwnPassword =>
                $"El usuario {actor} cambió su contraseña correctamente.",
            _ => FirstValue(details, ReadValue(changesJson, "Event"), "Evento de seguridad registrado.")
        };
    }

    private static string AppendDetails(string message, string? details) =>
        string.IsNullOrWhiteSpace(details) ? message : $"{message} {details}";

    private static bool IsUserNotFound(string? reason) =>
        Contains(reason, "no encontrado") || Contains(reason, "no existe");

    private static bool IsIncorrectPassword(string? reason) =>
        Contains(reason, "contraseña incorrecta") || Contains(reason, "contrasena incorrecta");

    private static bool IsInactiveAccount(string? reason) => Contains(reason, "desactivada");

    private static bool IsMissingAuthorization(string? reason) =>
        Contains(reason, "autorizacion operativa") || Contains(reason, "autorización operativa");

    private static bool Contains(string? value, string expected) =>
        value?.Contains(expected, StringComparison.OrdinalIgnoreCase) == true;

    private static string? ReadValue(string? json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String)
            {
                return RepairLegacyEncoding(property.GetString());
            }
        }
        catch (JsonException)
        {
            // Los registros históricos pueden no contener JSON válido.
        }

        return null;
    }

    private static string FirstValue(params string?[] values) =>
        values.First(value => !string.IsNullOrWhiteSpace(value))!;

    private static string? RepairLegacyEncoding(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            (!value.Contains('Ã') && !value.Contains('Â')))
        {
            return value;
        }

        return Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(value));
    }
}
