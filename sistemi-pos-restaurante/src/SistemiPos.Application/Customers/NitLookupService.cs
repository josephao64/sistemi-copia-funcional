using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Customers;

namespace SistemiPos.Application.Customers;

public sealed class NitLookupService(
    ICustomerRepository customerRepository,
    INitLookupConfigurationProvider configurationProvider,
    IEnumerable<INitLookupProvider> providers) : INitLookupService
{
    public async Task<NitLookupResult> LookupAsync(string nit, CancellationToken cancellationToken = default)
    {
        var normalizedNit = NitNormalizer.Normalize(nit);
        if (normalizedNit is null)
        {
            return Manual(string.Empty, "Ingresa un NIT válido y completa los datos manualmente.");
        }

        var localCustomer = await customerRepository.GetActiveCustomerByNormalizedNitAsync(normalizedNit, cancellationToken);
        if (localCustomer is not null)
        {
            return new NitLookupResult(
                localCustomer.Nit ?? normalizedNit,
                localCustomer.Name,
                localCustomer.Address,
                localCustomer.Id,
                NitLookupSource.Local,
                "Cliente encontrado en el sistema.");
        }

        var providerType = await configurationProvider.GetProviderTypeAsync(cancellationToken);
        if (providerType == NitLookupProviderType.None)
        {
            return Manual(normalizedNit, "No existe un proveedor de consulta configurado. Ingresa la razón social manualmente.");
        }

        var provider = providers.FirstOrDefault(x => x.ProviderType == providerType);
        if (provider is null || !provider.IsConfigured)
        {
            return Manual(normalizedNit, "El proveedor seleccionado todavía no tiene credenciales configuradas. Ingresa la razón social manualmente.");
        }

        try
        {
            var providerResult = await provider.LookupAsync(normalizedNit, cancellationToken);
            if (providerResult is not null && !string.IsNullOrWhiteSpace(providerResult.Name))
            {
                return new NitLookupResult(
                    NitNormalizer.Normalize(providerResult.Nit) ?? normalizedNit,
                    providerResult.Name.Trim(),
                    providerResult.Address?.Trim(),
                    null,
                    NitLookupSource.Provider,
                    "Datos obtenidos del proveedor configurado.");
            }
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Manual(normalizedNit, "La consulta al proveedor falló. Ingresa la razón social manualmente.");
        }

        return Manual(normalizedNit, "El proveedor no encontró el NIT. Ingresa la razón social manualmente.");
    }

    private static NitLookupResult Manual(string nit, string message) =>
        new(nit, null, null, null, NitLookupSource.Manual, message);
}
