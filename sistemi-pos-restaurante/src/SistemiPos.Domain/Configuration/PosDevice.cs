using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Configuration;

public sealed class PosDevice : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Provider { get; private set; }
    public string? TerminalId { get; private set; }
    public Guid? BankAccountId { get; private set; }
    public string? Location { get; private set; }
    public string? Observation { get; private set; }
    public bool IsActive { get; private set; }
    private PosDevice() { }
    public PosDevice(string name, bool isActive = true) { Update(name, isActive); }
    public PosDevice(string name, string provider, string terminalId, Guid bankAccountId, string location, string? observation, bool isActive = true) =>
        UpdateDetails(name, provider, terminalId, bankAccountId, location, observation, isActive);

    public void Update(string name, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainRuleException("El nombre del POS es requerido.");
        Name = NameNormalizer.Normalize(name); IsActive = isActive;
    }

    public void UpdateDetails(string name, string provider, string terminalId, Guid bankAccountId, string location, string? observation, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(terminalId)
            || bankAccountId == Guid.Empty || string.IsNullOrWhiteSpace(location))
            throw new DomainRuleException("Complete los datos requeridos del dispositivo POS.");

        Name = NameNormalizer.Normalize(name);
        Provider = NameNormalizer.Normalize(provider);
        TerminalId = terminalId.Trim();
        BankAccountId = bankAccountId;
        Location = NameNormalizer.Normalize(location);
        Observation = string.IsNullOrWhiteSpace(observation) ? null : observation.Trim();
        IsActive = isActive;
    }
}
