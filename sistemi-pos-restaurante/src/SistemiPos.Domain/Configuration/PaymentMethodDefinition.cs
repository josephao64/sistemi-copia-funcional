using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Configuration;

public enum PaymentFinancialBehavior { Cash = 1, Transfer = 2, Pos = 3, Credit = 4, OtherNonCash = 5 }

public sealed class PaymentMethodDefinition : AuditableEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public PaymentFinancialBehavior Behavior { get; private set; }
    public bool IsActive { get; private set; }
    private PaymentMethodDefinition() { }
    public PaymentMethodDefinition(string code, string name, PaymentFinancialBehavior behavior, bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            throw new DomainRuleException("El código y nombre de la forma de pago son requeridos.");
        Code = code.Trim().ToUpperInvariant(); Name = NameNormalizer.Normalize(name); Behavior = behavior; IsActive = isActive;
    }
    public void Update(string name, PaymentFinancialBehavior behavior, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainRuleException("El nombre es requerido.");
        Name = NameNormalizer.Normalize(name); Behavior = behavior; IsActive = isActive;
    }
}
