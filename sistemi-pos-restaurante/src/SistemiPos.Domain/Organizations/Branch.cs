using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Organizations;

public sealed class Branch : AuditableEntity
{
    public const int CodeMaxLength = 32;
    public const int NameMaxLength = 128;
    public const int AddressMaxLength = 256;
    public const int PhoneMaxLength = 32;
    public const int TimeZoneMaxLength = 64;
    public const int EstablishmentCodeMaxLength = 32;

    public Guid CompanyId { get; private set; }
    public Company Company { get; set; } = null!;

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public string TimeZone { get; private set; } = "America/Guatemala";
    public string? EstablishmentCode { get; private set; }
    public bool IsActive { get; private set; } = true;
    public BranchSetting? Setting { get; set; }

    private readonly List<Warehouse> _warehouses = [];
    public IReadOnlyCollection<Warehouse> Warehouses => _warehouses.AsReadOnly();

    private Branch()
    {
    }

    public Branch(
        Guid companyId,
        string code,
        string name,
        string? address = null,
        string? phone = null,
        string? timeZone = "America/Guatemala",
        string? establishmentCode = null)
    {
        if (companyId == Guid.Empty)
        {
            throw new DomainRuleException("La empresa es requerida para la sucursal.");
        }

        CompanyId = companyId;
        Update(code, name, address, phone, timeZone, establishmentCode);
    }

    public void Update(
        string code,
        string name,
        string? address = null,
        string? phone = null,
        string? timeZone = "America/Guatemala",
        string? establishmentCode = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainRuleException("El código de la sucursal es requerido.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("El nombre de la sucursal es requerido.");
        }

        if (string.IsNullOrWhiteSpace(timeZone))
        {
            throw new DomainRuleException("La zona horaria de la sucursal es requerida.");
        }

        var normalizedCode = code.Trim().ToUpperInvariant();
        var normalizedName = NameNormalizer.Normalize(name);
        var normalizedAddress = NormalizeOptional(address);
        var normalizedPhone = NormalizeOptional(phone);
        var normalizedTimeZone = timeZone.Trim();
        var normalizedEstablishmentCode = NormalizeOptional(establishmentCode);

        EnsureMaximumLength(normalizedCode, CodeMaxLength, "El código de la sucursal");
        EnsureMaximumLength(normalizedName, NameMaxLength, "El nombre de la sucursal");
        EnsureMaximumLength(normalizedAddress, AddressMaxLength, "La dirección de la sucursal");
        EnsureMaximumLength(normalizedPhone, PhoneMaxLength, "El teléfono de la sucursal");
        EnsureMaximumLength(normalizedTimeZone, TimeZoneMaxLength, "La zona horaria de la sucursal");
        EnsureMaximumLength(normalizedEstablishmentCode, EstablishmentCodeMaxLength, "El código de establecimiento");

        if (!IsValidTimeZone(normalizedTimeZone))
        {
            throw new DomainRuleException($"La zona horaria '{normalizedTimeZone}' no es válida.");
        }

        Code = normalizedCode;
        Name = normalizedName;
        Address = normalizedAddress;
        Phone = normalizedPhone;
        TimeZone = normalizedTimeZone;
        EstablishmentCode = normalizedEstablishmentCode;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }

    public static bool IsValidTimeZone(string? timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone) || timeZone.Trim().Length > TimeZoneMaxLength)
        {
            return false;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZone.Trim());
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void EnsureMaximumLength(string? value, int maximumLength, string fieldName)
    {
        if (value?.Length > maximumLength)
        {
            throw new DomainRuleException($"{fieldName} no puede superar {maximumLength} caracteres.");
        }
    }
}
