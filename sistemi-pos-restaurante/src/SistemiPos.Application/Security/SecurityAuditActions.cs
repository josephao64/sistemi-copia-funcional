namespace SistemiPos.Application.Security;

public static class SecurityAuditActions
{
    public const string Module = "Seguridad";
    public const string LoginSuccess = "InicioSesionExitoso";
    public const string LoginFailure = "InicioSesionFallido";
    public const string UserNotFound = "UsuarioInexistente";
    public const string AccountLockedOut = "CuentaBloqueada";
    public const string Logout = "CierreSesion";
    public const string CreateUser = "CrearUsuario";
    public const string UpdateUser = "ModificarUsuario";
    public const string ActivateUser = "ActivarUsuario";
    public const string DeactivateUser = "DesactivarUsuario";
    public const string ResetPassword = "RestablecerContrasena";
    public const string ChangeOwnPassword = "CambioContrasenaPropia";
}
