using SistemiPos.Domain.Configuration;

namespace SistemiPos.Application.Customers;

public interface INitLookupService
{
    Task<NitLookupResult> LookupAsync(string nit, CancellationToken cancellationToken = default);
}

public interface INitLookupProvider
{
    NitLookupProviderType ProviderType { get; }
    bool IsConfigured { get; }
    Task<NitProviderLookupResult?> LookupAsync(string normalizedNit, CancellationToken cancellationToken = default);
}

public interface INitLookupConfigurationProvider
{
    Task<NitLookupProviderType> GetProviderTypeAsync(CancellationToken cancellationToken = default);
}
