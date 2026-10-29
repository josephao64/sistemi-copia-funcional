using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Cash;

namespace SistemiPos.Domain.Configuration;

public sealed class CashMovementConcept : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public CashMovementType MovementType { get; private set; }
    public bool IsActive { get; private set; }
    private CashMovementConcept() { }
    public CashMovementConcept(string name, CashMovementType type, bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainRuleException("El concepto es requerido.");
        Name = NameNormalizer.Normalize(name); MovementType = type; IsActive = isActive;
    }
}
