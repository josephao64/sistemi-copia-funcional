namespace SistemiPos.Application.Settings;

public sealed record SaveBankAccountRequest(
    string Bank,
    string AccountName,
    string AccountNumber,
    bool IsActive);
