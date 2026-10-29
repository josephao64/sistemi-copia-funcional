using SistemiPos.Application.Security;

namespace SistemiPos.UnitTests.Security;

public sealed class UserRoleSelectionTests
{
    [Fact]
    public void Rejects_Multiple_Roles_Including_Legacy_Form_Fields()
    {
        var roles = UserRoleSelection.Resolve(["Administrador"], "Punto de Venta");
        Assert.False(UserRoleSelection.IsValid(roles));
    }

    [Fact]
    public void Rejects_Empty_Selection()
    {
        Assert.False(UserRoleSelection.IsValid(UserRoleSelection.Resolve([" "], null)));
    }

    [Fact]
    public void Accepts_One_Role_Without_Duplicating_Legacy_Selection()
    {
        var roles = UserRoleSelection.Resolve([" administrador "], "Administrador");
        Assert.True(UserRoleSelection.IsValid(roles));
        Assert.Single(roles);
    }
}
