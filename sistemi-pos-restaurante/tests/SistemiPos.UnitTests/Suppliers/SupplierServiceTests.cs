using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Suppliers;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Suppliers;

namespace SistemiPos.UnitTests.Suppliers;

public sealed class SupplierServiceTests
{
    [Fact]
    public async Task CreateSupplierAsync_Rejects_Duplicate_Nit()
    {
        var repository = new FakeSupplierRepository
        {
            ExistsNitResult = true
        };

        var service = new SupplierService(repository, new FakeUnitOfWork());

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.CreateSupplierAsync(new SaveSupplierRequest(
                "Proveedor Uno",
                "1234567",
                null,
                null,
                null,
                null,
                null,
                true)));
    }

    private sealed class FakeSupplierRepository : ISupplierRepository
    {
        public bool ExistsNitResult { get; init; }

        public Task AddSupplierAsync(Supplier supplier, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> ExistsNitAsync(string nit, Guid? excludeId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(ExistsNitResult);

        public Task<Supplier?> GetSupplierAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Supplier?>(null);

        public Task<List<Supplier>> GetSuppliersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<Supplier>());
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}
