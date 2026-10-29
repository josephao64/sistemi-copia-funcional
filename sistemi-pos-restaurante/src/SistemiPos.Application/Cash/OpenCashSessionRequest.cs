namespace SistemiPos.Application.Cash;

public sealed record OpenCashSessionRequest(
    string RegisterCode,
    decimal OpeningAmount,
    string? Observation = null,
    IReadOnlyList<Guid>? PosDeviceIds = null,
    bool UsePosDevices = false,
    Guid? TerminalId = null,
    string? Timezone = null);
