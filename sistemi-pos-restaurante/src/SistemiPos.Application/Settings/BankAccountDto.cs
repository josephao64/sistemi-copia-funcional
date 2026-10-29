namespace SistemiPos.Application.Settings;

public sealed record BankAccountDto(
    Guid Id,
    string Bank,
    string AccountName,
    string AccountNumber,
    bool IsActive);
