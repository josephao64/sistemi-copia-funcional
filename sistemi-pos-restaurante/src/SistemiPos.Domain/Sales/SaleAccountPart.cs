using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Sales;

public sealed class SaleAccountPart : Entity
{
    public Guid SaleId { get; private set; }
    public int Number { get; private set; }
    public decimal Amount { get; private set; }
    private SaleAccountPart() { }
    public SaleAccountPart(Guid saleId, int number, decimal amount)
    {
        if (saleId == Guid.Empty || number < 1 || amount <= 0 || decimal.Round(amount, 2) != amount)
            throw new DomainRuleException("La partición de cuenta requiere número e importe válidos.");
        SaleId = saleId; Number = number; Amount = amount;
    }

    public static IReadOnlyList<decimal> Divide(decimal total, int people)
    {
        if (people is < 1 or > 10 || total <= 0 || decimal.Round(total, 2) != total)
            throw new DomainRuleException("Indica entre una y diez personas y un total válido.");
        var share = decimal.Floor(total * 100 / people) / 100;
        if (share == 0) throw new DomainRuleException("El saldo es insuficiente para dividirlo entre esas personas.");
        return Enumerable.Range(1, people).Select(x => x == people ? total - share * (people - 1) : share).ToList();
    }
}
