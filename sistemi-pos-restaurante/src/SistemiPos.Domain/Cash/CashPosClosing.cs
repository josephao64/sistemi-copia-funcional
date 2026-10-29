using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Cash;

public sealed class CashPosClosing : AuditableEntity
{
    public Guid CashSessionId { get; private set; }
    public Guid PosDeviceId { get; private set; }
    public string PosDeviceName { get; private set; } = string.Empty;
    public decimal SystemTotal { get; private set; }
    public decimal PhysicalTotal { get; private set; }
    public decimal Difference { get; private set; }
    public string? BatchReference { get; private set; }
    public string? ReceiptPath { get; private set; }

    private CashPosClosing() { }

    public CashPosClosing(Guid cashSessionId, Guid posDeviceId, string posDeviceName, decimal systemTotal,
        decimal physicalTotal, string? batchReference, string? receiptPath)
    {
        if (cashSessionId == Guid.Empty || posDeviceId == Guid.Empty || string.IsNullOrWhiteSpace(posDeviceName))
            throw new DomainRuleException("El dispositivo POS es requerido.");
        if (systemTotal < 0 || physicalTotal < 0) throw new DomainRuleException("Los totales POS no pueden ser negativos.");
        if (string.IsNullOrWhiteSpace(batchReference)) throw new DomainRuleException("El código de cierre del POS es requerido.");
        CashSessionId = cashSessionId;
        PosDeviceId = posDeviceId;
        PosDeviceName = posDeviceName.Trim();
        SystemTotal = decimal.Round(systemTotal, 2);
        PhysicalTotal = decimal.Round(physicalTotal, 2);
        Difference = decimal.Round(PhysicalTotal - SystemTotal, 2);
        BatchReference = batchReference.Trim();
        ReceiptPath = string.IsNullOrWhiteSpace(receiptPath) ? null : receiptPath.Trim();
    }
}
