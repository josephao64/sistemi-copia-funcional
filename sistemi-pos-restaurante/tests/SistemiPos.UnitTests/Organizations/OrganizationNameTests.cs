using SistemiPos.Domain.Organizations;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.UnitTests.Organizations;

public sealed class OrganizationNameTests
{
    [Fact]
    public void Creation_Normalizes_Mixed_Case_Accents_And_Whitespace()
    {
        var group = new BusinessGroup(" grp ", "  gRUPO   áRBOL ", " rAZÓN  sOCIAL ");
        var company = new Company(group.Id, " emp ", " cAFÉ  cENTRAL ", " rAZÓN sOCIAL ");
        var branch = new Branch(company.Id, " suc ", " sUCURSAL  cENTRAL ");
        var warehouse = new Warehouse(branch.Id, " bod ", " bODEGA  fRÍA ");

        Assert.Equal("Grupo Árbol", group.Name);
        Assert.Equal("Razón Social", group.LegalName);
        Assert.Equal("Café Central", company.TradeName);
        Assert.Equal("Razón Social", company.LegalName);
        Assert.Equal("Sucursal Central", branch.Name);
        Assert.Equal("Bodega Fría", warehouse.Name);
        Assert.Equal("GRP", group.Code);
        Assert.Equal("EMP", company.Code);
    }

    [Fact]
    public void Editing_Normalizes_Names_And_Preserves_Unrelated_Fields()
    {
        var group = new BusinessGroup("G", "Original");
        var company = new Company(group.Id, "E", "Original");
        var branch = new Branch(company.Id, "S", "Original");
        var warehouse = new Warehouse(branch.Id, "B", "Original");

        group.Update("G", " nUEVO  gRUPO ", " ");
        company.Update("E", " nUEVA eMPRESA ", email: "Oficina@example.com");
        branch.Update("S", " nUEVA sUCURSAL ", timeZone: "America/Guatemala");
        warehouse.Update("B", " nUEVA bODEGA ", "Texto LIBRE");

        Assert.Equal("Nuevo Grupo", group.Name);
        Assert.Null(group.LegalName);
        Assert.Equal("Nueva Empresa", company.TradeName);
        Assert.Equal("Oficina@example.com", company.Email);
        Assert.Equal("Nueva Sucursal", branch.Name);
        Assert.Equal("America/Guatemala", branch.TimeZone);
        Assert.Equal("Nueva Bodega", warehouse.Name);
        Assert.Equal("Texto LIBRE", warehouse.Description);
    }

    [Fact]
    public void Branch_Rejects_Empty_Company_And_Required_Fields()
    {
        Assert.Throws<DomainRuleException>(() => new Branch(Guid.Empty, "SUC", "Central"));
        Assert.Throws<DomainRuleException>(() => new Branch(Guid.NewGuid(), " ", "Central"));
        Assert.Throws<DomainRuleException>(() => new Branch(Guid.NewGuid(), "SUC", " "));
        Assert.Throws<DomainRuleException>(() => new Branch(Guid.NewGuid(), "SUC", "Central", timeZone: " "));
    }

    [Fact]
    public void Branch_Rejects_Values_That_Exceed_Persistence_Limits()
    {
        var companyId = Guid.NewGuid();

        Assert.Throws<DomainRuleException>(() =>
            new Branch(companyId, new string('A', Branch.CodeMaxLength + 1), "Central"));
        Assert.Throws<DomainRuleException>(() =>
            new Branch(companyId, "SUC", new string('A', Branch.NameMaxLength + 1)));
        Assert.Throws<DomainRuleException>(() =>
            new Branch(companyId, "SUC", "Central", address: new string('A', Branch.AddressMaxLength + 1)));
        Assert.Throws<DomainRuleException>(() =>
            new Branch(companyId, "SUC", "Central", phone: new string('1', Branch.PhoneMaxLength + 1)));
        Assert.Throws<DomainRuleException>(() =>
            new Branch(companyId, "SUC", "Central", establishmentCode: new string('1', Branch.EstablishmentCodeMaxLength + 1)));
    }

    [Fact]
    public void Branch_Requires_A_Valid_TimeZone()
    {
        var exception = Assert.Throws<DomainRuleException>(() =>
            new Branch(Guid.NewGuid(), "SUC", "Central", timeZone: "Zona/Inexistente"));

        Assert.Contains("zona horaria", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(Branch.IsValidTimeZone("America/Guatemala"));
    }
}
