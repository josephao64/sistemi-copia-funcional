namespace SistemiPos.Application.Security;

public static class LoginSecurityDefaults
{
    public const int MaxFailedAccessAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan AuthenticationCookieLifetime = TimeSpan.FromHours(8);
    public const string InvalidCredentialsMessage = "Credenciales incorrectas.";
    public const string UnauthorizedAccountMessage =
        "El usuario no tiene autorizacion operativa configurada.";
    public const string TemporaryLockoutMessage =
        "La cuenta está temporalmente bloqueada por múltiples intentos fallidos. Intente de nuevo más tarde.";
    public const string SessionExpiredMessage = "Tu sesión ha expirado. Inicia sesión nuevamente.";
    public const string SessionExpiredQueryKey = "reason";
    public const string SessionExpiredQueryValue = "expired";
}
