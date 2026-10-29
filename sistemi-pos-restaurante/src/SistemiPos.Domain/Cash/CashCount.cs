using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Cash;

public sealed class CashCount : AuditableEntity
{
    private readonly List<CashCountLine> _lines = [];

    public Guid CashSessionId { get; private set; }
    public int Sequence { get; private set; }
    public DateTimeOffset CountedAtUtc { get; private set; }
    public string CountedByUserName { get; private set; } = string.Empty;
    public decimal ExpectedCash { get; private set; }
    public decimal CountedCash { get; private set; }
    public decimal Difference { get; private set; }
    public IReadOnlyCollection<CashCountLine> Lines => _lines;

    private CashCount() { }

    public CashCount(Guid cashSessionId, int sequence, DateTimeOffset countedAtUtc, string userName,
        decimal expectedCash, IEnumerable<CashCountLine> lines)
    {
        if (cashSessionId == Guid.Empty || sequence <= 0) throw new DomainRuleException("El arqueo no es válido.");
        var values = lines.Where(x => x.Quantity > 0).ToList();
        if (values.Count == 0) throw new DomainRuleException("Ingrese al menos una denominación para finalizar el arqueo.");
        CashSessionId = cashSessionId;
        Sequence = sequence;
        CountedAtUtc = countedAtUtc;
        CountedByUserName = string.IsNullOrWhiteSpace(userName) ? "system" : userName.Trim();
        ExpectedCash = decimal.Round(expectedCash, 2);
        _lines.AddRange(values);
        CountedCash = decimal.Round(values.Sum(x => x.Subtotal), 2);
        Difference = decimal.Round(CountedCash - ExpectedCash, 2);
    }
}
