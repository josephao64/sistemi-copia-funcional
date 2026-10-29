using SistemiPos.Domain.Suppliers;

namespace SistemiPos.Application.Suppliers;

public interface ISupplierRepository
{
    Task<List<Supplier>> GetSuppliersAsync(CancellationToken cancellationToken = default);
    Task<Supplier?> GetSupplierAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsNitAsync(string nit, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> ExistsLegalNameAsync(string legalName, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
    Task AddSupplierAsync(Supplier supplier, CancellationToken cancellationToken = default);
}
