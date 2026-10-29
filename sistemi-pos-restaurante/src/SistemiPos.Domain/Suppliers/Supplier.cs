using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Purchases;

namespace SistemiPos.Domain.Suppliers;

public sealed class Supplier : AuditableEntity
{
    public string LegalName { get; private set; } = string.Empty;
    public string Nit { get; private set; } = string.Empty;
    public string? TradeName { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public string? ContactName { get; private set; }
    public string? ContactPosition { get; private set; }
    public string? ContactPhone { get; private set; }
    public string? Notes { get; private set; }
    public PurchaseTermsType DefaultTerms { get; private set; } = PurchaseTermsType.Cash;
    public int? DefaultCreditDays { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Supplier()
    {
    }

    public static string NormalizeNit(string? nit) =>
        string.Concat((nit ?? string.Empty).Where(character => character != '-' && !char.IsWhiteSpace(character)));

    public Supplier(string legalName, string nit, string? tradeName, string? phone, string? email, string? address, string? contactName)
    {
        Update(legalName, nit, tradeName, phone, email, address, contactName, true);
    }

    public void Update(string legalName, string nit, string? tradeName, string? phone, string? email,
        string? address, string? contactName, bool isActive, int? defaultCreditDays = null,
        PurchaseTermsType defaultTerms = PurchaseTermsType.Cash, string? contactPosition = null,
        string? contactPhone = null, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(legalName))
        {
            throw new DomainRuleException("El nombre o razón social es requerido.");
        }

        var normalizedNit = NormalizeNit(nit);
        LegalName = NameNormalizer.Normalize(legalName);
        Nit = normalizedNit;
        TradeName = NameNormalizer.NormalizeOptional(tradeName);
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        ContactName = NameNormalizer.NormalizeOptional(contactName);
        if (defaultTerms is not (PurchaseTermsType.Cash or PurchaseTermsType.Credit))
            throw new DomainRuleException("La condición habitual del proveedor no es válida.");
        if (defaultTerms == PurchaseTermsType.Credit && defaultCreditDays is null or <= 0)
            throw new DomainRuleException("Los días de crédito deben ser mayores a cero cuando la condición habitual es crédito.");
        ContactPosition = NameNormalizer.NormalizeOptional(contactPosition);
        ContactPhone = string.IsNullOrWhiteSpace(contactPhone) ? null : contactPhone.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        DefaultTerms = defaultTerms;
        DefaultCreditDays = defaultTerms == PurchaseTermsType.Credit ? defaultCreditDays : null;
        IsActive = isActive;
    }
}
