using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Customers;

public sealed class Customer : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Nit { get; private set; }
    public string? NormalizedNit { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public string? VisitOrigin { get; private set; }
    public int CreditDays { get; private set; } = 30;
    public int GraceDays { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Customer()
    {
    }

    public Customer(string name, string? nit, string? phone, string? email, string? address, int creditDays = 30, int graceDays = 0)
    {
        Update(name, nit, phone, email, address, true, creditDays, graceDays);
    }

    public void Update(string name, string? nit, string? phone, string? email, string? address, bool isActive, int creditDays = 30, int graceDays = 0, string? visitOrigin = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("El nombre es requerido.");
        }

        if (creditDays < 0) throw new DomainRuleException("Los días de crédito no pueden ser negativos.");
        if (graceDays < 0) throw new DomainRuleException("Los días de gracia no pueden ser negativos.");

        Name = NameNormalizer.Normalize(name);
        Nit = string.IsNullOrWhiteSpace(nit) ? null : nit.Trim();
        NormalizedNit = NitNormalizer.Normalize(nit);
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        VisitOrigin = string.IsNullOrWhiteSpace(visitOrigin) ? null : NameNormalizer.Normalize(visitOrigin);
        CreditDays = creditDays;
        GraceDays = graceDays;
        IsActive = isActive;
    }

    public void CompleteMissingSaleContact(string? phone, string? visitOrigin)
    {
        if (string.IsNullOrWhiteSpace(Phone) && !string.IsNullOrWhiteSpace(phone)) Phone = phone.Trim();
        if (string.IsNullOrWhiteSpace(VisitOrigin) && !string.IsNullOrWhiteSpace(visitOrigin)) VisitOrigin = NameNormalizer.Normalize(visitOrigin);
    }

    public void UpdateSaleContact(string? name, string? address)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = NameNormalizer.Normalize(name);
        }

        if (address is not null)
        {
            Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        }
    }
}
