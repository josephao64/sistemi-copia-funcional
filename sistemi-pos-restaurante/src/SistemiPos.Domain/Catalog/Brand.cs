using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Catalog;

public sealed class Brand : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    private Brand()
    {
    }

    public Brand(string name)
    {
        Rename(name);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainRuleException("El nombre de la marca es requerido.");

        Name = NameNormalizer.Normalize(name);
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}
