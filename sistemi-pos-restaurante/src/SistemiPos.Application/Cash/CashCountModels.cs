namespace SistemiPos.Application.Cash;

public sealed record CashCountLineRequest(Guid DenominationId, int Quantity);
public sealed record RecordCashCountRequest(IReadOnlyList<CashCountLineRequest> Lines);
public sealed record CashCountLineDto(Guid DenominationId, decimal Value, int Quantity, decimal Subtotal);
public sealed record CashCountDto(Guid Id, int Sequence, DateTimeOffset CountedAtUtc, string UserName,
    decimal ExpectedCash, decimal CountedCash, decimal Difference, IReadOnlyList<CashCountLineDto> Lines);
