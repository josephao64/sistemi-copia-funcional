using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Purchases;

public sealed class PurchasePresentation : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public int Units { get; private set; }
    public bool IsActive { get; private set; } = true;

    private PurchasePresentation() { }

    public PurchasePresentation(string name, int units)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainRuleException("El nombre de la presentación es requerido.");
        if (units <= 0)
            throw new DomainRuleException("La cantidad de unidades debe ser mayor a cero.");

        Name = NameNormalizer.Normalize(name);
        Units = units;
    }
}
