using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Cash;

public sealed class CashSessionPosDevice
{
    public Guid CashSessionId { get; private set; }
    public Guid PosDeviceId { get; private set; }
    public string PosDeviceName { get; private set; } = string.Empty;

    private CashSessionPosDevice() { }

    public CashSessionPosDevice(Guid cashSessionId, Guid posDeviceId, string posDeviceName)
    {
        if (cashSessionId == Guid.Empty || posDeviceId == Guid.Empty || string.IsNullOrWhiteSpace(posDeviceName))
            throw new DomainRuleException("El dispositivo POS asignado es requerido.");
        CashSessionId = cashSessionId;
        PosDeviceId = posDeviceId;
        PosDeviceName = posDeviceName.Trim();
    }
}
