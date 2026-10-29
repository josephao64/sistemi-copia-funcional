using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Cash;

public sealed class CashCountLine : Entity
{
    public Guid CashCountId { get; set; }
    public Guid DenominationId { get; private set; }
    public decimal DenominationValue { get; private set; }
    public int Quantity { get; private set; }
    public decimal Subtotal { get; private set; }

    private CashCountLine() { }

    public CashCountLine(Guid denominationId, decimal denominationValue, int quantity)
    {
        if (denominationId == Guid.Empty || denominationValue <= 0 || quantity < 0)
            throw new DomainRuleException("La denominación contada no es válida.");
        DenominationId = denominationId;
        DenominationValue = decimal.Round(denominationValue, 2);
        Quantity = quantity;
        Subtotal = decimal.Round(DenominationValue * quantity, 2);
    }
}
