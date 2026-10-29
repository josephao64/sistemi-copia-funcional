namespace SistemiPos.Application.Suppliers;

public interface ISupplierService
{
    Task<IReadOnlyList<SupplierDto>> GetSuppliersAsync(CancellationToken cancellationToken = default);
    Task<SupplierDto?> GetSupplierAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateSupplierAsync(SaveSupplierRequest request, CancellationToken cancellationToken = default);
    Task UpdateSupplierAsync(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken = default);
}
