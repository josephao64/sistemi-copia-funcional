namespace SistemiPos.Application.Customers;

public sealed record CustomerHistoryDto(
    string DocumentNumber,
    DateTimeOffset OccurredAtUtc,
    string Description,
    decimal Total);
