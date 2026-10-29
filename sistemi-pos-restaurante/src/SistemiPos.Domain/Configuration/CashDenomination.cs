using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Configuration;

public enum DenominationKind { Banknote = 1, Coin = 2 }

public sealed class CashDenomination : AuditableEntity
{
    public DenominationKind Kind { get; private set; }
    public decimal Value { get; private set; }
    public bool IsActive { get; private set; }

    private CashDenomination() { }
    public CashDenomination(DenominationKind kind, decimal value, bool isActive = true)
    {
        if (value <= 0) throw new DomainRuleException("La denominación debe ser mayor a cero.");
        Kind = kind;
        Value = decimal.Round(value, 2);
        IsActive = isActive;
    }
    public void Update(DenominationKind kind, decimal value, bool isActive)
    {
        if (value <= 0) throw new DomainRuleException("La denominación debe ser mayor a cero.");
        Kind = kind; Value = decimal.Round(value, 2); IsActive = isActive;
    }
}
