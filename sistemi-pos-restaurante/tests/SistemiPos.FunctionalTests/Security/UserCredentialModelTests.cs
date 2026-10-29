using System.ComponentModel.DataAnnotations;
using SistemiPos.Application.Security;
using SistemiPos.Web.Models.Security;

namespace SistemiPos.FunctionalTests.Security;

public sealed class UserCredentialModelTests
{
    [Fact]
    public void Edit_Allows_Keeping_Current_Password()
    {
        var model = BuildValidEditModel();

        var errors = Validate(model);

        Assert.Empty(errors);
    }

    [Fact]
    public void Edit_Rejects_Mismatched_New_Password_Confirmation()
    {
        var model = BuildValidEditModel();
        model.Password = "NuevaClave123";
        model.ConfirmPassword = "OtraClave123";

        var errors = Validate(model);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(UserFormViewModel.ConfirmPassword)));
    }

    private static UserFormViewModel BuildValidEditModel() => new()
    {
        Id = Guid.NewGuid().ToString(),
        UserName = "cajero",
        FullName = "Usuario de caja",
        Email = "cajero@sistemi.local",
        RoleName = SecurityRoleNames.PointOfSale,
        IsActive = true
    };

    private static IReadOnlyList<ValidationResult> Validate(UserFormViewModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
