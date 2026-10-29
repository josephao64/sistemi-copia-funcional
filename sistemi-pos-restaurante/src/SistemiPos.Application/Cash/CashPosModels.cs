namespace SistemiPos.Application.Cash;
public sealed record UsedPosTotalDto(Guid PosDeviceId, string PosDeviceName, decimal SystemTotal);
public sealed record RecordPosClosingRequest(Guid PosDeviceId, decimal PhysicalTotal, string? BatchReference, string? ReceiptPath);
public sealed record CashPosClosingDto(Guid Id, Guid PosDeviceId, string PosDeviceName, decimal SystemTotal, decimal PhysicalTotal, decimal Difference, string? BatchReference, string? ReceiptPath);
