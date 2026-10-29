using SistemiPos.Application.Customers;
using SistemiPos.Domain.Configuration;

namespace SistemiPos.Infrastructure.Customers;

public sealed class UnconfiguredNitLookupProvider(NitLookupProviderType providerType) : INitLookupProvider
{
    public NitLookupProviderType ProviderType { get; } = providerType;
    public bool IsConfigured => false;

    public Task<NitProviderLookupResult?> LookupAsync(string normalizedNit, CancellationToken cancellationToken = default) =>
        Task.FromResult<NitProviderLookupResult?>(null);
}
