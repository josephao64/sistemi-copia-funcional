using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Configuration;

public sealed class BankAccount : AuditableEntity
{
    public string Bank { get; private set; } = string.Empty;
    public string AccountName { get; private set; } = string.Empty;
    public string AccountNumber { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    private BankAccount()
    {
    }

    public BankAccount(string bank, string accountName, string accountNumber)
    {
        Update(bank, accountName, accountNumber, true);
    }

    public void Update(string bank, string accountName, string accountNumber, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(bank) || string.IsNullOrWhiteSpace(accountName) || string.IsNullOrWhiteSpace(accountNumber))
        {
            throw new DomainRuleException("Los datos de la cuenta bancaria son requeridos.");
        }

        Bank = NameNormalizer.Normalize(bank);
        AccountName = NameNormalizer.Normalize(accountName);
        AccountNumber = accountNumber.Trim();
        IsActive = isActive;
    }
}
