using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Common.Exceptions;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Suppliers;

namespace SistemiPos.Application.Suppliers;

public sealed class SupplierService(ISupplierRepository repository, IUnitOfWork unitOfWork) : ISupplierService
{
    public async Task<IReadOnlyList<SupplierDto>> GetSuppliersAsync(CancellationToken cancellationToken = default)
    {
        var suppliers = await repository.GetSuppliersAsync(cancellationToken);
        return suppliers.OrderBy(x => x.LegalName).Select(Map).ToList();
    }

    public async Task<SupplierDto?> GetSupplierAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var supplier = await repository.GetSupplierAsync(id, cancellationToken);
        return supplier is null ? null : Map(supplier);
    }

    public async Task<Guid> CreateSupplierAsync(SaveSupplierRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedNit = Supplier.NormalizeNit(request.Nit);
        if (!string.IsNullOrWhiteSpace(normalizedNit)
            && await repository.ExistsNitAsync(normalizedNit, null, cancellationToken))
        {
            throw new DomainRuleException("Ya existe un proveedor registrado con el mismo NIT.");
        }
        if (await repository.ExistsLegalNameAsync(request.LegalName, null, cancellationToken))
        {
            throw new DomainRuleException("Ya existe un proveedor registrado con la misma razón social.");
        }

        var supplier = new Supplier(
            request.LegalName,
            normalizedNit,
            request.TradeName,
            request.Phone,
            request.Email,
            request.Address,
            request.ContactName);

        supplier.Update(request.LegalName, normalizedNit, request.TradeName, request.Phone, request.Email,
            request.Address, request.ContactName, request.IsActive, request.DefaultCreditDays,
            request.DefaultTerms, request.ContactPosition, request.ContactPhone, request.Notes);
        await repository.AddSupplierAsync(supplier, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return supplier.Id;
    }

    public async Task UpdateSupplierAsync(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken = default)
    {
        var supplier = await repository.GetSupplierAsync(id, cancellationToken)
            ?? throw new NotFoundException("Proveedor no encontrado.");

        var normalizedNit = Supplier.NormalizeNit(request.Nit);
        if (!string.IsNullOrWhiteSpace(normalizedNit)
            && await repository.ExistsNitAsync(normalizedNit, id, cancellationToken))
        {
            throw new DomainRuleException("Ya existe un proveedor registrado con el mismo NIT.");
        }
        if (await repository.ExistsLegalNameAsync(request.LegalName, id, cancellationToken))
        {
            throw new DomainRuleException("Ya existe un proveedor registrado con la misma razón social.");
        }

        supplier.Update(request.LegalName, normalizedNit, request.TradeName, request.Phone, request.Email,
            request.Address, request.ContactName, request.IsActive, request.DefaultCreditDays,
            request.DefaultTerms, request.ContactPosition, request.ContactPhone, request.Notes);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static SupplierDto Map(Supplier supplier) =>
        new(
            supplier.Id,
            supplier.LegalName,
            supplier.Nit,
            supplier.TradeName,
            supplier.Phone,
            supplier.Email,
            supplier.Address,
            supplier.ContactName,
            supplier.IsActive,
            supplier.DefaultCreditDays,
            supplier.DefaultTerms,
            supplier.ContactPosition,
            supplier.ContactPhone,
            supplier.Notes);
}
