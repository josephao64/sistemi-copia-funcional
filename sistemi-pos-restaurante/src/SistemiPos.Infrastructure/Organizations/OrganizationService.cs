using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Common.Exceptions;
using SistemiPos.Application.Organizations;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Organizations;

namespace SistemiPos.Infrastructure.Organizations;

public sealed class OrganizationService(
    IOrganizationRepository repository,
    IUnitOfWork unitOfWork) : IOrganizationService
{
    // Business Groups
    public async Task<IReadOnlyList<BusinessGroupDto>> GetBusinessGroupsAsync(bool onlyActive = false, CancellationToken cancellationToken = default)
    {
        var groups = await repository.GetBusinessGroupsAsync(onlyActive, cancellationToken);
        return groups.Select(x => new BusinessGroupDto(
            x.Id,
            x.Code,
            x.Name,
            x.LegalName,
            x.TaxNumber,
            x.Description,
            x.IsActive,
            x.Companies.Count)).ToList();
    }

    public async Task<BusinessGroupDto?> GetBusinessGroupByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var group = await repository.GetBusinessGroupByIdAsync(id, cancellationToken);
        if (group is null) return null;

        return new BusinessGroupDto(
            group.Id,
            group.Code,
            group.Name,
            group.LegalName,
            group.TaxNumber,
            group.Description,
            group.IsActive,
            group.Companies.Count);
    }

    public async Task<BusinessGroupDto> CreateBusinessGroupAsync(CreateBusinessGroupDto dto, CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetBusinessGroupByCodeAsync(dto.Code, cancellationToken);
        if (existing is not null)
        {
            throw new DomainRuleException($"Ya existe un grupo empresarial con el código '{dto.Code.Trim().ToUpperInvariant()}'.");
        }

        var group = new BusinessGroup(dto.Code, dto.Name, dto.LegalName, dto.TaxNumber, dto.Description);
        await repository.AddBusinessGroupAsync(group, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new BusinessGroupDto(group.Id, group.Code, group.Name, group.LegalName, group.TaxNumber, group.Description, group.IsActive, 0);
    }

    public async Task<BusinessGroupDto> UpdateBusinessGroupAsync(UpdateBusinessGroupDto dto, CancellationToken cancellationToken = default)
    {
        var group = await repository.GetBusinessGroupByIdAsync(dto.Id, cancellationToken)
            ?? throw new NotFoundException("Grupo empresarial no encontrado.");

        var existingWithCode = await repository.GetBusinessGroupByCodeAsync(dto.Code, cancellationToken);
        if (existingWithCode is not null && existingWithCode.Id != group.Id)
        {
            throw new DomainRuleException($"Ya existe otro grupo empresarial con el código '{dto.Code.Trim().ToUpperInvariant()}'.");
        }

        group.Update(dto.Code, dto.Name, dto.LegalName, dto.TaxNumber, dto.Description);
        group.SetActive(dto.IsActive);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new BusinessGroupDto(group.Id, group.Code, group.Name, group.LegalName, group.TaxNumber, group.Description, group.IsActive, group.Companies.Count);
    }

    public async Task ToggleBusinessGroupActiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var group = await repository.GetBusinessGroupByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Grupo empresarial no encontrado.");

        group.SetActive(!group.IsActive);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // Companies
    public async Task<IReadOnlyList<CompanyDto>> GetCompaniesAsync(Guid? businessGroupId = null, bool onlyActive = false, CancellationToken cancellationToken = default)
    {
        var companies = await repository.GetCompaniesAsync(businessGroupId, onlyActive, cancellationToken);
        return companies.Select(x => new CompanyDto(
            x.Id,
            x.BusinessGroupId,
            x.BusinessGroup.Name,
            x.Code,
            x.TradeName,
            x.LegalName,
            x.Nit,
            x.Email,
            x.Phone,
            x.Address,
            x.TaxRegime,
            x.IsActive,
            x.Branches.Count)).ToList();
    }

    public async Task<CompanyDto?> GetCompanyByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var company = await repository.GetCompanyByIdAsync(id, cancellationToken);
        if (company is null) return null;

        return new CompanyDto(
            company.Id,
            company.BusinessGroupId,
            company.BusinessGroup.Name,
            company.Code,
            company.TradeName,
            company.LegalName,
            company.Nit,
            company.Email,
            company.Phone,
            company.Address,
            company.TaxRegime,
            company.IsActive,
            company.Branches.Count);
    }

    public async Task<CompanyDto> CreateCompanyAsync(CreateCompanyDto dto, CancellationToken cancellationToken = default)
    {
        var group = await repository.GetBusinessGroupByIdAsync(dto.BusinessGroupId, cancellationToken)
            ?? throw new NotFoundException("El grupo empresarial seleccionado no existe.");

        var existingWithCode = await repository.GetCompanyByCodeAsync(dto.BusinessGroupId, dto.Code, cancellationToken);
        if (existingWithCode is not null)
        {
            throw new DomainRuleException($"Ya existe una empresa con el código '{dto.Code.Trim().ToUpperInvariant()}' en este grupo empresarial.");
        }

        var company = new Company(
            dto.BusinessGroupId,
            dto.Code,
            dto.TradeName,
            dto.LegalName,
            dto.Nit,
            dto.Email,
            dto.Phone,
            dto.Address,
            dto.TaxRegime);

        await repository.AddCompanyAsync(company, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CompanyDto(
            company.Id,
            company.BusinessGroupId,
            group.Name,
            company.Code,
            company.TradeName,
            company.LegalName,
            company.Nit,
            company.Email,
            company.Phone,
            company.Address,
            company.TaxRegime,
            company.IsActive,
            0);
    }

    public async Task<CompanyDto> UpdateCompanyAsync(UpdateCompanyDto dto, CancellationToken cancellationToken = default)
    {
        var company = await repository.GetCompanyByIdAsync(dto.Id, cancellationToken)
            ?? throw new NotFoundException("Empresa no encontrada.");

        var existingWithCode = await repository.GetCompanyByCodeAsync(dto.BusinessGroupId, dto.Code, cancellationToken);
        if (existingWithCode is not null && existingWithCode.Id != company.Id)
        {
            throw new DomainRuleException($"Ya existe otra empresa con el código '{dto.Code.Trim().ToUpperInvariant()}' en este grupo empresarial.");
        }

        company.Update(
            dto.Code,
            dto.TradeName,
            dto.LegalName,
            dto.Nit,
            dto.Email,
            dto.Phone,
            dto.Address,
            dto.TaxRegime);
        company.SetActive(dto.IsActive);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CompanyDto(
            company.Id,
            company.BusinessGroupId,
            company.BusinessGroup.Name,
            company.Code,
            company.TradeName,
            company.LegalName,
            company.Nit,
            company.Email,
            company.Phone,
            company.Address,
            company.TaxRegime,
            company.IsActive,
            company.Branches.Count);
    }

    public async Task ToggleCompanyActiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var company = await repository.GetCompanyByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Empresa no encontrada.");

        company.SetActive(!company.IsActive);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // Branches
    public async Task<IReadOnlyList<BranchDto>> GetBranchesAsync(Guid? companyId = null, bool onlyActive = false, CancellationToken cancellationToken = default)
    {
        var branches = await repository.GetBranchesAsync(companyId, onlyActive, cancellationToken);
        return branches.Select(x => new BranchDto(
            x.Id,
            x.CompanyId,
            x.Company.TradeName,
            x.Code,
            x.Name,
            x.Address,
            x.Phone,
            x.TimeZone,
            x.EstablishmentCode,
            x.IsActive,
            x.Warehouses.Count)).ToList();
    }

    public async Task<BranchDto?> GetBranchByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var branch = await repository.GetBranchByIdAsync(id, cancellationToken);
        if (branch is null) return null;

        return new BranchDto(
            branch.Id,
            branch.CompanyId,
            branch.Company.TradeName,
            branch.Code,
            branch.Name,
            branch.Address,
            branch.Phone,
            branch.TimeZone,
            branch.EstablishmentCode,
            branch.IsActive,
            branch.Warehouses.Count);
    }

    public async Task<BranchDto> CreateBranchAsync(CreateBranchDto dto, CancellationToken cancellationToken = default)
    {
        var company = await repository.GetCompanyByIdAsync(dto.CompanyId, cancellationToken)
            ?? throw new NotFoundException("La empresa seleccionada no existe.");

        var existingWithCode = await repository.GetBranchByCodeAsync(dto.CompanyId, dto.Code, cancellationToken);
        if (existingWithCode is not null)
        {
            throw new DomainRuleException($"Ya existe una sucursal con el código '{dto.Code.Trim().ToUpperInvariant()}' en esta empresa.");
        }

        var branch = new Branch(
            dto.CompanyId,
            dto.Code,
            dto.Name,
            dto.Address,
            dto.Phone,
            dto.TimeZone,
            dto.EstablishmentCode);

        await repository.AddBranchAsync(branch, cancellationToken);

        // Crear automáticamente la bodega principal predeterminada para la sucursal
        var defaultWarehouse = new Warehouse(
            branch.Id,
            "BOD-01",
            "Bodega Principal",
            "Bodega principal predeterminada de la sucursal",
            isDefault: true);
        await repository.AddWarehouseAsync(defaultWarehouse, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new BranchDto(
            branch.Id,
            branch.CompanyId,
            company.TradeName,
            branch.Code,
            branch.Name,
            branch.Address,
            branch.Phone,
            branch.TimeZone,
            branch.EstablishmentCode,
            branch.IsActive,
            1);
    }

    public async Task<BranchDto> UpdateBranchAsync(UpdateBranchDto dto, CancellationToken cancellationToken = default)
    {
        var branch = await repository.GetBranchByIdAsync(dto.Id, cancellationToken)
            ?? throw new NotFoundException("Sucursal no encontrada.");

        var existingWithCode = await repository.GetBranchByCodeAsync(dto.CompanyId, dto.Code, cancellationToken);
        if (existingWithCode is not null && existingWithCode.Id != branch.Id)
        {
            throw new DomainRuleException($"Ya existe otra sucursal con el código '{dto.Code.Trim().ToUpperInvariant()}' en esta empresa.");
        }

        branch.Update(
            dto.Code,
            dto.Name,
            dto.Address,
            dto.Phone,
            dto.TimeZone,
            dto.EstablishmentCode);
        branch.SetActive(dto.IsActive);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new BranchDto(
            branch.Id,
            branch.CompanyId,
            branch.Company.TradeName,
            branch.Code,
            branch.Name,
            branch.Address,
            branch.Phone,
            branch.TimeZone,
            branch.EstablishmentCode,
            branch.IsActive,
            branch.Warehouses.Count);
    }

    public async Task ToggleBranchActiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var branch = await repository.GetBranchByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Sucursal no encontrada.");

        branch.SetActive(!branch.IsActive);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // Warehouses
    public async Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(Guid? branchId = null, bool onlyActive = false, CancellationToken cancellationToken = default)
    {
        var warehouses = await repository.GetWarehousesAsync(branchId, onlyActive, cancellationToken);
        return warehouses.Select(x => new WarehouseDto(
            x.Id,
            x.BranchId,
            x.Branch.Name,
            x.Branch.Company.TradeName,
            x.Code,
            x.Name,
            x.Description,
            x.IsDefault,
            x.IsKitchen,
            x.IsActive)).ToList();
    }

    public async Task<WarehouseDto?> GetWarehouseByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var warehouse = await repository.GetWarehouseByIdAsync(id, cancellationToken);
        if (warehouse is null) return null;

        return new WarehouseDto(
            warehouse.Id,
            warehouse.BranchId,
            warehouse.Branch.Name,
            warehouse.Branch.Company.TradeName,
            warehouse.Code,
            warehouse.Name,
            warehouse.Description,
            warehouse.IsDefault,
            warehouse.IsKitchen,
            warehouse.IsActive);
    }

    public async Task<WarehouseDto> CreateWarehouseAsync(CreateWarehouseDto dto, CancellationToken cancellationToken = default)
    {
        var branch = await repository.GetBranchByIdAsync(dto.BranchId, cancellationToken)
            ?? throw new NotFoundException("La sucursal seleccionada no existe.");

        var existingWithCode = await repository.GetWarehouseByCodeAsync(dto.BranchId, dto.Code, cancellationToken);
        if (existingWithCode is not null)
        {
            throw new DomainRuleException($"Ya existe una bodega con el código '{dto.Code.Trim().ToUpperInvariant()}' en esta sucursal.");
        }

        if (dto.IsDefault)
        {
            var existingWarehouses = await repository.GetWarehousesAsync(dto.BranchId, cancellationToken: cancellationToken);
            foreach (var w in existingWarehouses)
            {
                w.SetDefault(false);
            }
        }

        var warehouse = new Warehouse(
            dto.BranchId,
            dto.Code,
            dto.Name,
            dto.Description,
            dto.IsDefault,
            dto.IsKitchen);

        await repository.AddWarehouseAsync(warehouse, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new WarehouseDto(
            warehouse.Id,
            warehouse.BranchId,
            branch.Name,
            branch.Company.TradeName,
            warehouse.Code,
            warehouse.Name,
            warehouse.Description,
            warehouse.IsDefault,
            warehouse.IsKitchen,
            warehouse.IsActive);
    }

    public async Task<WarehouseDto> UpdateWarehouseAsync(UpdateWarehouseDto dto, CancellationToken cancellationToken = default)
    {
        var warehouse = await repository.GetWarehouseByIdAsync(dto.Id, cancellationToken)
            ?? throw new NotFoundException("Bodega no encontrada.");

        var existingWithCode = await repository.GetWarehouseByCodeAsync(dto.BranchId, dto.Code, cancellationToken);
        if (existingWithCode is not null && existingWithCode.Id != warehouse.Id)
        {
            throw new DomainRuleException($"Ya existe otra bodega con el código '{dto.Code.Trim().ToUpperInvariant()}' en esta sucursal.");
        }

        if (dto.IsDefault && !warehouse.IsDefault)
        {
            var existingWarehouses = await repository.GetWarehousesAsync(dto.BranchId, cancellationToken: cancellationToken);
            foreach (var w in existingWarehouses)
            {
                w.SetDefault(false);
            }
        }

        warehouse.Update(dto.Code, dto.Name, dto.Description, dto.IsDefault, dto.IsKitchen);
        warehouse.SetActive(dto.IsActive);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new WarehouseDto(
            warehouse.Id,
            warehouse.BranchId,
            warehouse.Branch.Name,
            warehouse.Branch.Company.TradeName,
            warehouse.Code,
            warehouse.Name,
            warehouse.Description,
            warehouse.IsDefault,
            warehouse.IsKitchen,
            warehouse.IsActive);
    }

    public async Task ToggleWarehouseActiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var warehouse = await repository.GetWarehouseByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Bodega no encontrada.");

        warehouse.SetActive(!warehouse.IsActive);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // User Scopes
    public async Task<IReadOnlyList<UserScopeDto>> GetUserScopesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var scopes = await repository.GetUserScopesAsync(userId, cancellationToken);
        return scopes.Select(x => new UserScopeDto(
            x.Id,
            x.UserId,
            x.ScopeLevel,
            x.BusinessGroupId,
            x.BusinessGroup?.Name,
            x.CompanyId,
            x.Company?.TradeName,
            x.BranchId,
            x.Branch?.Name,
            x.IsDefault)).ToList();
    }

    public async Task SaveUserScopesAsync(SaveUserScopesDto dto, CancellationToken cancellationToken = default)
    {
        var assignments = new List<UserScopeAssignment>();

        if (dto.ScopeLevel == UserScopeLevel.Global)
        {
            assignments.Add(new UserScopeAssignment(dto.UserId, UserScopeLevel.Global, isDefault: true));
        }
        else if (dto.ScopeLevel == UserScopeLevel.BusinessGroup && dto.BusinessGroupId.HasValue)
        {
            assignments.Add(new UserScopeAssignment(dto.UserId, UserScopeLevel.BusinessGroup, businessGroupId: dto.BusinessGroupId.Value, isDefault: true));
        }
        else if (dto.ScopeLevel == UserScopeLevel.Company && dto.CompanyId.HasValue)
        {
            assignments.Add(new UserScopeAssignment(dto.UserId, UserScopeLevel.Company, companyId: dto.CompanyId.Value, isDefault: true));
        }
        else if (dto.ScopeLevel == UserScopeLevel.Branch)
        {
            var defaultId = dto.DefaultBranchId ?? dto.BranchIds.FirstOrDefault();
            foreach (var branchId in dto.BranchIds.Distinct())
            {
                assignments.Add(new UserScopeAssignment(
                    dto.UserId,
                    UserScopeLevel.Branch,
                    branchId: branchId,
                    isDefault: branchId == defaultId));
            }
        }

        await repository.SaveUserScopesAsync(dto.UserId, assignments, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BranchDto>> GetAuthorizedBranchesForUserAsync(Guid userId, bool isSuperAdmin, CancellationToken cancellationToken = default)
    {
        var branches = await repository.GetAuthorizedBranchesForUserAsync(userId, isSuperAdmin, cancellationToken);
        return branches.Select(x => new BranchDto(
            x.Id,
            x.CompanyId,
            x.Company.TradeName,
            x.Code,
            x.Name,
            x.Address,
            x.Phone,
            x.TimeZone,
            x.EstablishmentCode,
            x.IsActive,
            x.Warehouses.Count)).ToList();
    }
}
