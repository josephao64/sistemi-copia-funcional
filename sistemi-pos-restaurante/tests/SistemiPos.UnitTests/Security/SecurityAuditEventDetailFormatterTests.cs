using System.Text.Json;
using SistemiPos.Application.Security;

namespace SistemiPos.UnitTests.Security;

public sealed class SecurityAuditEventDetailFormatterTests
{
    [Fact]
    public void LoginSuccess_Returns_Clear_Message()
    {
        var detail = SecurityAuditEventDetailFormatter.Format(
            SecurityAuditActions.LoginSuccess,
            "admin",
            "admin",
            JsonSerializer.Serialize(new { Event = "Inicio de sesión correcto" }));

        Assert.Equal("El usuario admin inició sesión correctamente.", detail);
    }

    [Fact]
    public void IncorrectPassword_Returns_Clear_Message_Without_Json_Properties()
    {
        var detail = SecurityAuditEventDetailFormatter.Format(
            SecurityAuditActions.LoginFailure,
            "admin",
            "admin",
            JsonSerializer.Serialize(new { Event = "Intento fallido", Reason = "Contraseña incorrecta" }));

        Assert.Equal(
            "El usuario admin intentó iniciar sesión, pero la contraseña ingresada es incorrecta.",
            detail);
        Assert.DoesNotContain("Reason", detail);
        Assert.DoesNotContain("{", detail);
    }

    [Theory]
    [InlineData(SecurityAuditActions.UserNotFound, null)]
    [InlineData(SecurityAuditActions.LoginFailure, "Usuario o correo no encontrado")]
    public void MissingUser_Returns_Clear_Message_For_New_And_Historical_Records(
        string action,
        string? reason)
    {
        var json = reason is null ? null : JsonSerializer.Serialize(new { Reason = reason });

        var detail = SecurityAuditEventDetailFormatter.Format(action, "jperez", "jperez", json);

        Assert.Equal(
            "Se intentó iniciar sesión con el usuario jperez, pero el usuario no existe.",
            detail);
    }

    [Fact]
    public void UserManagement_Returns_Readable_Message_And_Decodes_Historical_Text()
    {
        const string json = "{\"TargetUserName\":\"jperez\",\"Details\":\"ContraseÃ±a restablecida.\"}";

        var detail = SecurityAuditEventDetailFormatter.Format(
            SecurityAuditActions.UpdateUser,
            Guid.NewGuid().ToString(),
            "admin",
            json);

        Assert.Equal(
            "El usuario admin modificó al usuario jperez. Contraseña restablecida.",
            detail);
    }

    [Fact]
    public void MalformedHistoricalDetail_Uses_A_Readable_Fallback()
    {
        var detail = SecurityAuditEventDetailFormatter.Format(
            SecurityAuditActions.LoginFailure,
            "admin",
            "admin",
            "{invalid-json");

        Assert.Equal(
            "El usuario admin intentó iniciar sesión, pero no fue posible validar sus credenciales.",
            detail);
    }
}
