using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Customers;
using SistemiPos.Domain.Customers;
using SistemiPos.Domain.Sales;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Customers;

public sealed class CustomerRepository(AppDbContext dbContext) : ICustomerRepository
{
    public Task<List<Customer>> GetCustomersAsync(CancellationToken cancellationToken = default) =>
        dbContext.Customers
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Customer?> GetActiveCustomerByNormalizedNitAsync(string normalizedNit, CancellationToken cancellationToken = default) =>
        dbContext.Customers.FirstOrDefaultAsync(
            x => x.IsActive && (x.NormalizedNit == normalizedNit
                || (x.Nit != null && x.Nit.Replace("-", "").Replace(" ", "").ToUpper() == normalizedNit)),
            cancellationToken);

    public Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken = default) =>
        dbContext.Customers.AddAsync(customer, cancellationToken).AsTask();

    public Task<List<CustomerHistoryDto>> GetCustomerHistoryAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        dbContext.Sales
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.SoldAtUtc)
            .Select(x => new CustomerHistoryDto(
                x.Number,
                x.SoldAtUtc,
                x.Status == SaleStatus.Cancelled ? "Venta anulada" : "Venta",
                x.Total))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
}
