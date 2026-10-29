using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Catalog;

public sealed class Category : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public ICollection<Product> Products { get; set; } = new List<Product>();

    private Category()
    {
    }

    public Category(string name, string? description)
    {
        Rename(name, description);
    }

    public void Rename(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("El nombre de la categoría es requerido.");
        }

        Name = NameNormalizer.Normalize(name);
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }
}
