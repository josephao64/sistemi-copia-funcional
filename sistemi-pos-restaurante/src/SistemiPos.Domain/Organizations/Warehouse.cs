using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Organizations;

public sealed class Warehouse : AuditableEntity
{
    public Guid BranchId { get; private set; }
    public Branch Branch { get; set; } = null!;

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsKitchen { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Warehouse()
    {
    }

    public Warehouse(
        Guid branchId,
        string code,
        string name,
        string? description = null,
        bool isDefault = false,
        bool isKitchen = false)
    {
        if (branchId == Guid.Empty)
        {
            throw new DomainRuleException("La sucursal es requerida para la bodega.");
        }

        BranchId = branchId;
        Update(code, name, description, isDefault, isKitchen);
    }

    public void Update(
        string code,
        string name,
        string? description = null,
        bool isDefault = false,
        bool isKitchen = false)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainRuleException("El código de la bodega es requerido.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("El nombre de la bodega es requerido.");
        }

        Code = code.Trim().ToUpperInvariant();
        Name = NameNormalizer.Normalize(name);
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IsDefault = isDefault;
        IsKitchen = isKitchen;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }

    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
    }
}
