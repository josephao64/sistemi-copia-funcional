using SistemiPos.Application.Security;

namespace SistemiPos.UnitTests.Security;

public sealed class LoginSecurityDefaultsTests
{
    [Fact]
    public void Lockout_Uses_Five_Attempts_And_Fifteen_Minutes()
    {
        Assert.Equal(5, LoginSecurityDefaults.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), LoginSecurityDefaults.LockoutDuration);
    }

    [Fact]
    public void Authentication_Cookie_Defaults_To_Eight_Hours()
    {
        Assert.Equal(TimeSpan.FromHours(8), LoginSecurityDefaults.AuthenticationCookieLifetime);
    }

    [Fact]
    public void Messages_Remain_Generic_And_Do_Not_Reveal_Credential_Details()
    {
        Assert.Equal("Credenciales incorrectas.", LoginSecurityDefaults.InvalidCredentialsMessage);
        Assert.Contains("autorizacion", LoginSecurityDefaults.UnauthorizedAccountMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("temporalmente bloqueada", LoginSecurityDefaults.TemporaryLockoutMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contraseña", LoginSecurityDefaults.InvalidCredentialsMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("usuario", LoginSecurityDefaults.InvalidCredentialsMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Session_Expired_Message_Is_Clear_And_Query_Is_Stable()
    {
        Assert.Equal("Tu sesión ha expirado. Inicia sesión nuevamente.", LoginSecurityDefaults.SessionExpiredMessage);
        Assert.Equal("reason", LoginSecurityDefaults.SessionExpiredQueryKey);
        Assert.Equal("expired", LoginSecurityDefaults.SessionExpiredQueryValue);
    }
}
