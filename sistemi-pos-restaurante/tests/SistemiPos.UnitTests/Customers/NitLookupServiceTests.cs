using SistemiPos.Application.Customers;
using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Customers;

namespace SistemiPos.UnitTests.Customers;

public sealed class NitLookupServiceTests
{
    [Fact]
    public async Task LookupAsync_Returns_Local_Customer_Before_Configured_Provider()
    {
        var customer = new Customer("Cliente local", "1234-5", null, null, null);
        var provider = new FakeProvider(NitLookupProviderType.Infile, true, new NitProviderLookupResult("12345", "Nombre externo"));
        var service = new NitLookupService(
            new FakeCustomerRepository(customer),
            new FakeConfigurationProvider(NitLookupProviderType.Infile),
            [provider]);

        var result = await service.LookupAsync("1234-5");

        Assert.True(result.Found);
        Assert.Equal(NitLookupSource.Local, result.Source);
        Assert.Equal(customer.Id, result.CustomerId);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task LookupAsync_Uses_Configured_Provider_When_Customer_Does_Not_Exist()
    {
        var provider = new FakeProvider(NitLookupProviderType.Other, true, new NitProviderLookupResult("9876-K", "Razón social externa"));
        var service = new NitLookupService(
            new FakeCustomerRepository(),
            new FakeConfigurationProvider(NitLookupProviderType.Other),
            [provider]);

        var result = await service.LookupAsync("9876-k");

        Assert.True(result.Found);
        Assert.Equal(NitLookupSource.Provider, result.Source);
        Assert.Null(result.CustomerId);
        Assert.Equal("Razón social externa", result.Name);
    }

    [Fact]
    public async Task LookupAsync_Allows_Manual_Entry_When_Provider_Has_No_Credentials()
    {
        var service = new NitLookupService(
            new FakeCustomerRepository(),
            new FakeConfigurationProvider(NitLookupProviderType.Infile),
            [new FakeProvider(NitLookupProviderType.Infile, false, null)]);

        var result = await service.LookupAsync("4567-8");

        Assert.False(result.Found);
        Assert.Equal(NitLookupSource.Manual, result.Source);
        Assert.Contains("credenciales", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeConfigurationProvider(NitLookupProviderType providerType) : INitLookupConfigurationProvider
    {
        public Task<NitLookupProviderType> GetProviderTypeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(providerType);
    }

    private sealed class FakeProvider(
        NitLookupProviderType providerType,
        bool isConfigured,
        NitProviderLookupResult? result) : INitLookupProvider
    {
        public NitLookupProviderType ProviderType => providerType;
        public bool IsConfigured => isConfigured;
        public int Calls { get; private set; }

        public Task<NitProviderLookupResult?> LookupAsync(string normalizedNit, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeCustomerRepository(params Customer[] customers) : ICustomerRepository
    {
        private readonly List<Customer> _customers = [.. customers];

        public Task<List<Customer>> GetCustomersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_customers.ToList());

        public Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_customers.FirstOrDefault(x => x.Id == id));

        public Task<Customer?> GetActiveCustomerByNormalizedNitAsync(string normalizedNit, CancellationToken cancellationToken = default) =>
            Task.FromResult(_customers.FirstOrDefault(x => x.IsActive && x.NormalizedNit == normalizedNit));

        public Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken = default)
        {
            _customers.Add(customer);
            return Task.CompletedTask;
        }

        public Task<List<CustomerHistoryDto>> GetCustomerHistoryAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<CustomerHistoryDto>());
    }
}
