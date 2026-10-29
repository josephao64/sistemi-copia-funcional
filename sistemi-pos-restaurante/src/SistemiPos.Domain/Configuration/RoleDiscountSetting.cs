using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Configuration;

public sealed class RoleDiscountSetting : AuditableEntity
{
    public Guid RoleId { get; private set; }
    public decimal MaxDiscountPercent { get; private set; }

    private RoleDiscountSetting()
    {
    }

    public RoleDiscountSetting(Guid roleId, decimal maxDiscountPercent)
    {
        Update(roleId, maxDiscountPercent);
    }

    public void Update(Guid roleId, decimal maxDiscountPercent)
    {
        if (roleId == Guid.Empty)
        {
            throw new DomainRuleException("El rol es requerido.");
        }

        if (maxDiscountPercent < 0)
        {
            throw new DomainRuleException("El porcentaje máximo no puede ser negativo.");
        }

        RoleId = roleId;
        MaxDiscountPercent = decimal.Round(maxDiscountPercent, 2);
    }
}
