using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Organizations;

public sealed class BusinessGroup : AuditableEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? LegalName { get; private set; }
    public string? TaxNumber { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    private readonly List<Company> _companies = [];
    public IReadOnlyCollection<Company> Companies => _companies.AsReadOnly();

    private BusinessGroup()
    {
    }

    public BusinessGroup(
        string code,
        string name,
        string? legalName = null,
        string? taxNumber = null,
        string? description = null)
    {
        Update(code, name, legalName, taxNumber, description);
    }

    public void Update(
        string code,
        string name,
        string? legalName = null,
        string? taxNumber = null,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainRuleException("El código del grupo empresarial es requerido.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("El nombre del grupo empresarial es requerido.");
        }

        Code = code.Trim().ToUpperInvariant();
        Name = NameNormalizer.Normalize(name);
        LegalName = NameNormalizer.NormalizeOptional(legalName);
        TaxNumber = string.IsNullOrWhiteSpace(taxNumber) ? null : taxNumber.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }
}
