using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Catalog;

public sealed class VariantType : AuditableEntity
{
    private readonly List<VariantOption> _options = [];

    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public IReadOnlyCollection<VariantOption> Options => _options;

    private VariantType() { }

    public VariantType(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainRuleException("El tipo de variante es requerido.");
        Name = NameNormalizer.Normalize(name);
    }

    public VariantOption GetOrAddOption(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new DomainRuleException("El valor de variante es requerido.");
        var normalized = NameNormalizer.Normalize(value);
        var existing = _options.FirstOrDefault(x => NameNormalizer.Equals(x.Value, normalized));
        if (existing is not null) return existing;
        var option = new VariantOption(Id, normalized);
        _options.Add(option);
        return option;
    }
}

public sealed class VariantOption : AuditableEntity
{
    public Guid VariantTypeId { get; private set; }
    public VariantType VariantType { get; set; } = null!;
    public string Value { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    private VariantOption() { }

    internal VariantOption(Guid variantTypeId, string value)
    {
        VariantTypeId = variantTypeId;
        Value = value;
    }
}

public sealed class ProductVariantOption : Entity
{
    public Guid ProductId { get; private set; }
    public Product Product { get; set; } = null!;
    public Guid VariantOptionId { get; private set; }
    public VariantOption VariantOption { get; set; } = null!;
    public int SortOrder { get; private set; }

    private ProductVariantOption() { }

    internal ProductVariantOption(Guid productId, Guid variantOptionId, int sortOrder)
    {
        ProductId = productId;
        VariantOptionId = variantOptionId;
        SortOrder = sortOrder;
    }

    internal void SetSortOrder(int sortOrder) => SortOrder = sortOrder;
}
