using SistemiPos.Application.Security;

namespace SistemiPos.UnitTests.Security;

public sealed class SecurityAuditActionsTests
{
    [Fact]
    public void Login_And_Logout_Actions_Are_Stable_For_Audit_Queries()
    {
        Assert.Equal("Seguridad", SecurityAuditActions.Module);
        Assert.Equal("InicioSesionExitoso", SecurityAuditActions.LoginSuccess);
        Assert.Equal("InicioSesionFallido", SecurityAuditActions.LoginFailure);
        Assert.Equal("UsuarioInexistente", SecurityAuditActions.UserNotFound);
        Assert.Equal("CuentaBloqueada", SecurityAuditActions.AccountLockedOut);
        Assert.Equal("CierreSesion", SecurityAuditActions.Logout);
    }
}
