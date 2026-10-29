using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Configuration;

public sealed class UserDiscountSetting : AuditableEntity
{
    public Guid UserId { get; private set; }
    public decimal MaxDiscountPercent { get; private set; }

    private UserDiscountSetting()
    {
    }

    public UserDiscountSetting(Guid userId, decimal maxDiscountPercent)
    {
        Update(userId, maxDiscountPercent);
    }

    public void Update(Guid userId, decimal maxDiscountPercent)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainRuleException("El usuario es requerido.");
        }

        if (maxDiscountPercent < 0)
        {
            throw new DomainRuleException("El porcentaje máximo no puede ser negativo.");
        }

        UserId = userId;
        MaxDiscountPercent = decimal.Round(maxDiscountPercent, 2);
    }
}
