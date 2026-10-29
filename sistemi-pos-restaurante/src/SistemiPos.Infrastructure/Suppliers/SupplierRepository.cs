using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Suppliers;
using SistemiPos.Domain.Common;
using SistemiPos.Domain.Suppliers;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Suppliers;

public sealed class SupplierRepository(AppDbContext dbContext) : ISupplierRepository
{
    public Task<List<Supplier>> GetSuppliersAsync(CancellationToken cancellationToken = default) =>
        dbContext.Suppliers.AsNoTracking().ToListAsync(cancellationToken);

    public Task<Supplier?> GetSupplierAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Suppliers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<bool> ExistsNitAsync(string nit, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalizedNit = Supplier.NormalizeNit(nit);
        if (string.IsNullOrWhiteSpace(normalizedNit)) return false;
        var storedNits = await dbContext.Suppliers
            .Where(x => !excludeId.HasValue || x.Id != excludeId.Value)
            .Select(x => x.Nit)
            .ToListAsync(cancellationToken);
        return storedNits.Any(value => string.Equals(Supplier.NormalizeNit(value), normalizedNit, StringComparison.Ordinal));
    }

    public async Task<bool> ExistsLegalNameAsync(string legalName, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalizedName = NameNormalizer.Normalize(legalName);
        var storedNames = await dbContext.Suppliers
            .Where(x => !excludeId.HasValue || x.Id != excludeId.Value)
            .Select(x => x.LegalName)
            .ToListAsync(cancellationToken);
        return storedNames.Any(value => string.Equals(NameNormalizer.Normalize(value), normalizedName, StringComparison.OrdinalIgnoreCase));
    }

    public Task AddSupplierAsync(Supplier supplier, CancellationToken cancellationToken = default) =>
        dbContext.Suppliers.AddAsync(supplier, cancellationToken).AsTask();
}
