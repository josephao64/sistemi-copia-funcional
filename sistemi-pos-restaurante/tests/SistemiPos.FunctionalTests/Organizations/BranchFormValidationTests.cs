using System.ComponentModel.DataAnnotations;
using SistemiPos.Domain.Organizations;
using SistemiPos.Web.Models.Organizations;

namespace SistemiPos.FunctionalTests.Organizations;

public sealed class BranchFormValidationTests
{
    [Fact]
    public void BranchForm_Rejects_Empty_Company_And_Invalid_TimeZone()
    {
        var model = ValidModel();
        model.CompanyId = Guid.Empty;
        model.TimeZone = "Zona/Inexistente";

        var errors = Validate(model);

        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(model.CompanyId)));
        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(model.TimeZone)));
    }

    [Fact]
    public void BranchForm_Rejects_Values_Above_Database_Limits()
    {
        var model = ValidModel();
        model.Code = new string('A', Branch.CodeMaxLength + 1);
        model.Name = new string('A', Branch.NameMaxLength + 1);
        model.Address = new string('A', Branch.AddressMaxLength + 1);
        model.Phone = new string('1', Branch.PhoneMaxLength + 1);
        model.EstablishmentCode = new string('1', Branch.EstablishmentCodeMaxLength + 1);

        var errors = Validate(model);

        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(model.Code)));
        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(model.Name)));
        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(model.Address)));
        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(model.Phone)));
        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(model.EstablishmentCode)));
    }

    private static BranchFormViewModel ValidModel() => new()
    {
        CompanyId = Guid.NewGuid(),
        Code = "SUC-01",
        Name = "Sucursal Central",
        TimeZone = "America/Guatemala"
    };

    private static List<ValidationResult> Validate(BranchFormViewModel model)
    {
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), errors, validateAllProperties: true);
        return errors;
    }
}
