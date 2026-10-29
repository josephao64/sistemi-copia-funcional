using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Customers;
using SistemiPos.Domain.Customers;

namespace SistemiPos.UnitTests.Customers;

public sealed class CustomerServiceTests
{
    [Fact]
    public async Task CreateCustomerAsync_Persists_New_Customer()
    {
        var repository = new FakeCustomerRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new CustomerService(repository, unitOfWork);

        var customerId = await service.CreateCustomerAsync(new SaveCustomerRequest(
            "Cliente Uno",
            "CF",
            "5555-5555",
            "cliente@example.com",
            "Zona 1",
            true));

        Assert.Equal(1, unitOfWork.SaveChangesCalls);
        Assert.Single(repository.Customers);
        Assert.Equal(customerId, repository.Customers[0].Id);
        Assert.Equal("Cliente Uno", repository.Customers[0].Name);
    }

    [Fact]
    public async Task GetHistoryAsync_Returns_Empty_List_When_No_Sales_Exist()
    {
        var customer = new Customer("Cliente Uno", "CF", null, null, null);
        var repository = new FakeCustomerRepository(customer);
        var service = new CustomerService(repository, new FakeUnitOfWork());

        var history = await service.GetHistoryAsync(customer.Id);

        Assert.Empty(history);
    }

    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        public FakeCustomerRepository(Customer? customer = null)
        {
            if (customer is not null)
            {
                Customers.Add(customer);
            }
        }

        public List<Customer> Customers { get; } = [];

        public Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken = default)
        {
            Customers.Add(customer);
            return Task.CompletedTask;
        }

        public Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Customers.FirstOrDefault(x => x.Id == id));

        public Task<Customer?> GetActiveCustomerByNormalizedNitAsync(string normalizedNit, CancellationToken cancellationToken = default) =>
            Task.FromResult(Customers.FirstOrDefault(x => x.IsActive && x.NormalizedNit == normalizedNit));

        public Task<List<CustomerHistoryDto>> GetCustomerHistoryAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<CustomerHistoryDto>());

        public Task<List<Customer>> GetCustomersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Customers.ToList());
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return Task.FromResult(1);
        }
    }
}
