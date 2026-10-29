using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Common.Exceptions;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Customers;

namespace SistemiPos.Application.Customers;

public sealed class CustomerService(ICustomerRepository repository, IUnitOfWork unitOfWork) : ICustomerService
{
    public async Task<IReadOnlyList<CustomerDto>> GetCustomersAsync(CancellationToken cancellationToken = default)
    {
        var customers = await repository.GetCustomersAsync(cancellationToken);
        return customers.OrderBy(x => x.Name).Select(Map).ToList();
    }

    public async Task<CustomerDto?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await repository.GetCustomerAsync(id, cancellationToken);
        return customer is null ? null : Map(customer);
    }

    public async Task<Guid> CreateCustomerAsync(SaveCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedNit = NitNormalizer.Normalize(request.Nit);
        if (normalizedNit is not null &&
            await repository.GetActiveCustomerByNormalizedNitAsync(normalizedNit, cancellationToken) is not null)
        {
            throw new DomainRuleException("Ya existe un cliente activo con este NIT.");
        }

        var customer = new Customer(request.Name, request.Nit, request.Phone, request.Email, request.Address, request.CreditDays, request.GraceDays);
        customer.Update(request.Name, request.Nit, request.Phone, request.Email, request.Address, request.IsActive, request.CreditDays, request.GraceDays, request.VisitOrigin);

        await repository.AddCustomerAsync(customer, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return customer.Id;
    }

    public async Task UpdateCustomerAsync(Guid id, SaveCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await repository.GetCustomerAsync(id, cancellationToken)
            ?? throw new NotFoundException("Cliente no encontrado.");

        customer.Update(request.Name, request.Nit, request.Phone, request.Email, request.Address, request.IsActive, request.CreditDays, request.GraceDays, request.VisitOrigin);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerHistoryDto>> GetHistoryAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customer = await repository.GetCustomerAsync(customerId, cancellationToken)
            ?? throw new NotFoundException("Cliente no encontrado.");

        var history = await repository.GetCustomerHistoryAsync(customer.Id, cancellationToken);
        return history.OrderByDescending(x => x.OccurredAtUtc).ToList();
    }

    private static CustomerDto Map(Customer customer) =>
        new(
            customer.Id,
            customer.Name,
            customer.Nit,
            customer.Phone,
            customer.Email,
            customer.Address,
            customer.IsActive,
            customer.CreditDays,
            customer.GraceDays,
            customer.VisitOrigin);
}
