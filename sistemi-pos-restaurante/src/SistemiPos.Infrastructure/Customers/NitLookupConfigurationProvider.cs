using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Customers;
using SistemiPos.Domain.Configuration;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Customers;

public sealed class NitLookupConfigurationProvider(AppDbContext dbContext,
    SistemiPos.Application.Abstractions.IOperationContextAccessor? context = null) : INitLookupConfigurationProvider
{
    public async Task<NitLookupProviderType> GetProviderTypeAsync(CancellationToken cancellationToken = default)
    {
        if (context?.CurrentCompanyId is Guid company)
            return await dbContext.CompanySettings.AsNoTracking().Where(x => x.CompanyId == company)
                .Select(x => (NitLookupProviderType?)x.NitLookupProvider).SingleOrDefaultAsync(cancellationToken)
                ?? NitLookupProviderType.None;
        if (context is not null) return NitLookupProviderType.None;
        return await dbContext.SystemSettings
            .AsNoTracking()
            .Select(x => (NitLookupProviderType?)x.NitLookupProvider)
            .FirstOrDefaultAsync(cancellationToken)
        ?? NitLookupProviderType.None;
    }
}
