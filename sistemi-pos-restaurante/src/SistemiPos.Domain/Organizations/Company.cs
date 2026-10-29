using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Organizations;

public sealed class Company : AuditableEntity
{
    public Guid BusinessGroupId { get; private set; }
    public BusinessGroup BusinessGroup { get; set; } = null!;

    public string Code { get; private set; } = string.Empty;
    public string TradeName { get; private set; } = string.Empty;
    public string? LegalName { get; private set; }
    public string? Nit { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Address { get; private set; }
    public string? TaxRegime { get; private set; }
    public bool IsActive { get; private set; } = true;

    private readonly List<Branch> _branches = [];
    public IReadOnlyCollection<Branch> Branches => _branches.AsReadOnly();
    public CompanySetting? Setting { get; set; }

    private Company()
    {
    }

    public Company(
        Guid businessGroupId,
        string code,
        string tradeName,
        string? legalName = null,
        string? nit = null,
        string? email = null,
        string? phone = null,
        string? address = null,
        string? taxRegime = null)
    {
        if (businessGroupId == Guid.Empty)
        {
            throw new DomainRuleException("El grupo empresarial es requerido para la empresa.");
        }

        BusinessGroupId = businessGroupId;
        Update(code, tradeName, legalName, nit, email, phone, address, taxRegime);
    }

    public void Update(
        string code,
        string tradeName,
        string? legalName = null,
        string? nit = null,
        string? email = null,
        string? phone = null,
        string? address = null,
        string? taxRegime = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainRuleException("El código de la empresa es requerido.");
        }

        if (string.IsNullOrWhiteSpace(tradeName))
        {
            throw new DomainRuleException("El nombre comercial de la empresa es requerido.");
        }

        Code = code.Trim().ToUpperInvariant();
        TradeName = NameNormalizer.Normalize(tradeName);
        LegalName = NameNormalizer.NormalizeOptional(legalName);
        Nit = string.IsNullOrWhiteSpace(nit) ? null : nit.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        TaxRegime = string.IsNullOrWhiteSpace(taxRegime) ? null : taxRegime.Trim();
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }
}
