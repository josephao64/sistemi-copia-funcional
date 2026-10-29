using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Configuration;

public sealed class OriginBank : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    private OriginBank() { }

    public OriginBank(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainRuleException("El nombre del banco es requerido.");
        Name = NameNormalizer.Normalize(name);
    }
}
