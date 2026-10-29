namespace SistemiPos.Application.Customers;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerDto>> GetCustomersAsync(CancellationToken cancellationToken = default);
    Task<CustomerDto?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateCustomerAsync(SaveCustomerRequest request, CancellationToken cancellationToken = default);
    Task UpdateCustomerAsync(Guid id, SaveCustomerRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerHistoryDto>> GetHistoryAsync(Guid customerId, CancellationToken cancellationToken = default);
}
