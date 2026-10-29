using SistemiPos.Domain.Customers;

namespace SistemiPos.Application.Customers;

public interface ICustomerRepository
{
    Task<List<Customer>> GetCustomersAsync(CancellationToken cancellationToken = default);
    Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Customer?> GetActiveCustomerByNormalizedNitAsync(string normalizedNit, CancellationToken cancellationToken = default);
    Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken = default);
    Task<List<CustomerHistoryDto>> GetCustomerHistoryAsync(Guid customerId, CancellationToken cancellationToken = default);
}
