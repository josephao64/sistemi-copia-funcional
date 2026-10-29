using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Organizations;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Web.Models.Organizations;

namespace SistemiPos.Web.Controllers;

[Authorize(Policy = PermissionCodes.OrganizationsView)]
public sealed class OrganizationController(
    IOrganizationService organizationService,
    IOperationContextAccessor operationContext) : Controller
{
    public async Task<IActionResult> Index(string tab = "branches", CancellationToken cancellationToken = default)
    {
        var model = new OrganizationIndexViewModel
        {
            ActiveTab = tab.ToLowerInvariant(),
            BusinessGroups = await organizationService.GetBusinessGroupsAsync(cancellationToken: cancellationToken),
            Companies = await organizationService.GetCompaniesAsync(cancellationToken: cancellationToken),
            Branches = await organizationService.GetBranchesAsync(cancellationToken: cancellationToken),
            Warehouses = await organizationService.GetWarehousesAsync(cancellationToken: cancellationToken)
        };

        return View(model);
    }

    // --- Selector de Ubicación (ORG-08) ---
    [Authorize(Policy = PermissionCodes.BranchSwitch)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectBranch(Guid branchId, string? returnUrl, CancellationToken cancellationToken)
    {
        try
        {
            await operationContext.SetCurrentBranchAsync(branchId, cancellationToken);
            TempData["Success"] = "Sucursal activa cambiada exitosamente.";
        }
        catch (UnauthorizedAccessException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (Exception)
        {
            TempData["Error"] = "No se pudo cambiar la sucursal.";
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            // If the return URL had an explicit old branchId parameter, strip it so the new active branch takes effect
            if (returnUrl.Contains("branchId=", StringComparison.OrdinalIgnoreCase))
            {
                var cleanUrl = returnUrl.Split('?')[0];
                return Redirect(cleanUrl);
            }

            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    // --- Grupos Empresariales ---
    [Authorize(Policy = PermissionCodes.OrganizationsManage)]
    [HttpGet]
    public IActionResult CreateGroup()
    {
        return View("GroupForm", new BusinessGroupFormViewModel());
    }

    [Authorize(Policy = PermissionCodes.OrganizationsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateGroup(BusinessGroupFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("GroupForm", model);
        }

        try
        {
            await organizationService.CreateBusinessGroupAsync(new CreateBusinessGroupDto(
                model.Code,
                model.Name,
                model.LegalName,
                model.TaxNumber,
                model.Description), cancellationToken);

            TempData["Success"] = "Grupo empresarial creado con éxito.";
            return RedirectToAction(nameof(Index), new { tab = "groups" });
        }
        catch (DomainRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("GroupForm", model);
        }
    }

    [Authorize(Policy = PermissionCodes.OrganizationsManage)]
    [HttpGet]
    public async Task<IActionResult> EditGroup(Guid id, CancellationToken cancellationToken)
    {
        var group = await organizationService.GetBusinessGroupByIdAsync(id, cancellationToken);
        if (group is null) return NotFound();

        var model = new BusinessGroupFormViewModel
        {
            Id = group.Id,
            Code = group.Code,
            Name = group.Name,
            LegalName = group.LegalName,
            TaxNumber = group.TaxNumber,
            Description = group.Description,
            IsActive = group.IsActive
        };

        return View("GroupForm", model);
    }

    [Authorize(Policy = PermissionCodes.OrganizationsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditGroup(BusinessGroupFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || !model.Id.HasValue)
        {
            return View("GroupForm", model);
        }

        try
        {
            await organizationService.UpdateBusinessGroupAsync(new UpdateBusinessGroupDto(
                model.Id.Value,
                model.Code,
                model.Name,
                model.LegalName,
                model.TaxNumber,
                model.Description,
                model.IsActive), cancellationToken);

            TempData["Success"] = "Grupo empresarial actualizado con éxito.";
            return RedirectToAction(nameof(Index), new { tab = "groups" });
        }
        catch (DomainRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("GroupForm", model);
        }
    }

    [Authorize(Policy = PermissionCodes.OrganizationsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleGroupActive(Guid id, CancellationToken cancellationToken)
    {
        await organizationService.ToggleBusinessGroupActiveAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index), new { tab = "groups" });
    }

    // --- Empresas ---
    [Authorize(Policy = PermissionCodes.OrganizationsManage)]
    [HttpGet]
    public async Task<IActionResult> CreateCompany(Guid? businessGroupId, CancellationToken cancellationToken)
    {
        var groups = await organizationService.GetBusinessGroupsAsync(onlyActive: true, cancellationToken);
        var model = new CompanyFormViewModel
        {
            BusinessGroupId = businessGroupId ?? groups.FirstOrDefault()?.Id ?? Guid.Empty,
            AvailableBusinessGroups = groups.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList()
        };

        return View("CompanyForm", model);
    }

    [Authorize(Policy = PermissionCodes.OrganizationsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCompany(CompanyFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var groups = await organizationService.GetBusinessGroupsAsync(onlyActive: true, cancellationToken);
            model.AvailableBusinessGroups = groups.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList();
            return View("CompanyForm", model);
        }

        try
        {
            await organizationService.CreateCompanyAsync(new CreateCompanyDto(
                model.BusinessGroupId,
                model.Code,
                model.TradeName,
                model.LegalName,
                model.Nit,
                model.Email,
                model.Phone,
                model.Address,
                model.TaxRegime), cancellationToken);

            TempData["Success"] = "Empresa creada con éxito.";
            return RedirectToAction(nameof(Index), new { tab = "companies" });
        }
        catch (DomainRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var groups = await organizationService.GetBusinessGroupsAsync(onlyActive: true, cancellationToken);
            model.AvailableBusinessGroups = groups.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList();
            return View("CompanyForm", model);
        }
    }

    [Authorize(Policy = PermissionCodes.OrganizationsManage)]
    [HttpGet]
    public async Task<IActionResult> EditCompany(Guid id, CancellationToken cancellationToken)
    {
        var company = await organizationService.GetCompanyByIdAsync(id, cancellationToken);
        if (company is null) return NotFound();

        var groups = await organizationService.GetBusinessGroupsAsync(onlyActive: true, cancellationToken);
        var model = new CompanyFormViewModel
        {
            Id = company.Id,
            BusinessGroupId = company.BusinessGroupId,
            Code = company.Code,
            TradeName = company.TradeName,
            LegalName = company.LegalName,
            Nit = company.Nit,
            Email = company.Email,
            Phone = company.Phone,
            Address = company.Address,
            TaxRegime = company.TaxRegime,
            IsActive = company.IsActive,
            AvailableBusinessGroups = groups.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList()
        };

        return View("CompanyForm", model);
    }

    [Authorize(Policy = PermissionCodes.OrganizationsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCompany(CompanyFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || !model.Id.HasValue)
        {
            var groups = await organizationService.GetBusinessGroupsAsync(onlyActive: true, cancellationToken);
            model.AvailableBusinessGroups = groups.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList();
            return View("CompanyForm", model);
        }

        try
        {
            await organizationService.UpdateCompanyAsync(new UpdateCompanyDto(
                model.Id.Value,
                model.BusinessGroupId,
                model.Code,
                model.TradeName,
                model.LegalName,
                model.Nit,
                model.Email,
                model.Phone,
                model.Address,
                model.TaxRegime,
                model.IsActive), cancellationToken);

            TempData["Success"] = "Empresa actualizada con éxito.";
            return RedirectToAction(nameof(Index), new { tab = "companies" });
        }
        catch (DomainRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var groups = await organizationService.GetBusinessGroupsAsync(onlyActive: true, cancellationToken);
            model.AvailableBusinessGroups = groups.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList();
            return View("CompanyForm", model);
        }
    }

    [Authorize(Policy = PermissionCodes.OrganizationsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCompanyActive(Guid id, CancellationToken cancellationToken)
    {
        await organizationService.ToggleCompanyActiveAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index), new { tab = "companies" });
    }

    // --- Sucursales ---
    [Authorize(Policy = PermissionCodes.OrganizationsManage)]
    [HttpGet]
    public async Task<IActionResult> CreateBranch(Guid? companyId, CancellationToken cancellationToken)
    {
        var companies = await organizationService.GetCompaniesAsync(onlyActive: true, cancellationToken: cancellationToken);
        var model = new BranchFormViewModel
        {
            CompanyId = companyId ?? companies.FirstOrDefault()?.Id ?? Guid.Empty,
            AvailableCompanies = companies.Select(x => new SelectListItem(x.TradeName, x.Id.ToString())).ToList()
        };

        return View("BranchForm", model);
    }

    [Authorize(Policy = PermissionCodes.OrganizationsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBranch(BranchFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var companies = await organizationService.GetCompaniesAsync(onlyActive: true, cancellationToken: cancellationToken);
            model.AvailableCompanies = companies.Select(x => new SelectListItem(x.TradeName, x.Id.ToString())).ToList();
            return View("BranchForm", model);
        }

        try
        {
            await organizationService.CreateBranchAsync(new CreateBranchDto(
                model.CompanyId,
                model.Code,
                model.Name,
                model.Address,
                model.Phone,
                model.TimeZone,
                model.EstablishmentCode), cancellationToken);

            TempData["Success"] = "Sucursal creada con éxito (con su bodega principal asignada automáticamente).";
            return RedirectToAction(nameof(Index), new { tab = "branches" });
        }
        catch (DomainRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var companies = await organizationService.GetCompaniesAsync(onlyActive: true, cancellationToken: cancellationToken);
            model.AvailableCompanies = companies.Select(x => new SelectListItem(x.TradeName, x.Id.ToString())).ToList();
            return View("BranchForm", model);
        }
    }

    [Authorize(Policy = PermissionCodes.OrganizationsManage)]
    [HttpGet]
    public async Task<IActionResult> EditBranch(Guid id, CancellationToken cancellationToken)
    {
        var branch = await organizationService.GetBranchByIdAsync(id, cancellationToken);
        if (branch is null) return NotFound();

        var companies = await organizationService.GetCompaniesAsync(onlyActive: true, cancellationToken: cancellationToken);
        var model = new BranchFormViewModel
        {
            Id = branch.Id,
            CompanyId = branch.CompanyId,
            Code = branch.Code,
            Name = branch.Name,
            Address = branch.Address,
            Phone = branch.Phone,
            TimeZone = branch.TimeZone,
            EstablishmentCode = branch.EstablishmentCode,
            IsActive = branch.IsActive,
            AvailableCompanies = companies.Select(x => new SelectListItem(x.TradeName, x.Id.ToString())).ToList()
        };

        return View("BranchForm", model);
    }

    [Authorize(Policy = PermissionCodes.OrganizationsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditBranch(BranchFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || !model.Id.HasValue)
        {
            var companies = await organizationService.GetCompaniesAsync(onlyActive: true, cancellationToken: cancellationToken);
            model.AvailableCompanies = companies.Select(x => new SelectListItem(x.TradeName, x.Id.ToString())).ToList();
            return View("BranchForm", model);
        }

        try
        {
            await organizationService.UpdateBranchAsync(new UpdateBranchDto(
                model.Id.Value,
                model.CompanyId,
                model.Code,
                model.Name,
                model.Address,
                model.Phone,
                model.TimeZone,
                model.EstablishmentCode,
                model.IsActive), cancellationToken);

            TempData["Success"] = "Sucursal actualizada con éxito.";
            return RedirectToAction(nameof(Index), new { tab = "branches" });
        }
        catch (DomainRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var companies = await organizationService.GetCompaniesAsync(onlyActive: true, cancellationToken: cancellationToken);
            model.AvailableCompanies = companies.Select(x => new SelectListItem(x.TradeName, x.Id.ToString())).ToList();
            return View("BranchForm", model);
        }
    }

    [Authorize(Policy = PermissionCodes.OrganizationsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleBranchActive(Guid id, CancellationToken cancellationToken)
    {
        await organizationService.ToggleBranchActiveAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index), new { tab = "branches" });
    }

    // --- Bodegas ---
    [Authorize(Policy = PermissionCodes.OrganizationsManage)]
    [HttpGet]
    public async Task<IActionResult> CreateWarehouse(Guid? branchId, CancellationToken cancellationToken)
    {
        var branches = await organizationService.GetBranchesAsync(onlyActive: true, cancellationToken: cancellationToken);
        var model = new WarehouseFormViewModel
        {
            BranchId = branchId ?? branches.FirstOrDefault()?.Id ?? Guid.Empty,
            AvailableBranches = branches.Select(x => new SelectListItem($"{x.Name} ({x.CompanyTradeName})", x.Id.ToString())).ToList()
        };

        return View("WarehouseForm", model);
    }

    [Authorize(Policy = PermissionCodes.OrganizationsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateWarehouse(WarehouseFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var branches = await organizationService.GetBranchesAsync(onlyActive: true, cancellationToken: cancellationToken);
            model.AvailableBranches = branches.Select(x => new SelectListItem($"{x.Name} ({x.CompanyTradeName})", x.Id.ToString())).ToList();
            return View("WarehouseForm", model);
        }

        try
        {
            await organizationService.CreateWarehouseAsync(new CreateWarehouseDto(
                model.BranchId,
                model.Code,
                model.Name,
                model.Description,
                model.IsDefault,
                model.IsKitchen), cancellationToken);

            TempData["Success"] = "Bodega creada con éxito.";
            return RedirectToAction(nameof(Index), new { tab = "warehouses" });
        }
        catch (DomainRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var branches = await organizationService.GetBranchesAsync(onlyActive: true, cancellationToken: cancellationToken);
            model.AvailableBranches = branches.Select(x => new SelectListItem($"{x.Name} ({x.CompanyTradeName})", x.Id.ToString())).ToList();
            return View("WarehouseForm", model);
        }
    }

    [Authorize(Policy = PermissionCodes.OrganizationsManage)]
    [HttpGet]
    public async Task<IActionResult> EditWarehouse(Guid id, CancellationToken cancellationToken)
    {
        var warehouse = await organizationService.GetWarehouseByIdAsync(id, cancellationToken);
        if (warehouse is null) return NotFound();

        var branches = await organizationService.GetBranchesAsync(onlyActive: true, cancellationToken: cancellationToken);
        var model = new WarehouseFormViewModel
        {
            Id = warehouse.Id,
            BranchId = warehouse.BranchId,
            Code = warehouse.Code,
            Name = warehouse.Name,
            Description = warehouse.Description,
            IsDefault = warehouse.IsDefault,
            IsKitchen = warehouse.IsKitchen,
            IsActive = warehouse.IsActive,
            AvailableBranches = branches.Select(x => new SelectListItem($"{x.Name} ({x.CompanyTradeName})", x.Id.ToString())).ToList()
        };

        return View("WarehouseForm", model);
    }

    [Authorize(Policy = PermissionCodes.OrganizationsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditWarehouse(WarehouseFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || !model.Id.HasValue)
        {
            var branches = await organizationService.GetBranchesAsync(onlyActive: true, cancellationToken: cancellationToken);
            model.AvailableBranches = branches.Select(x => new SelectListItem($"{x.Name} ({x.CompanyTradeName})", x.Id.ToString())).ToList();
            return View("WarehouseForm", model);
        }

        try
        {
            await organizationService.UpdateWarehouseAsync(new UpdateWarehouseDto(
                model.Id.Value,
                model.BranchId,
                model.Code,
                model.Name,
                model.Description,
                model.IsDefault,
                model.IsKitchen,
                model.IsActive), cancellationToken);

            TempData["Success"] = "Bodega actualizada con éxito.";
            return RedirectToAction(nameof(Index), new { tab = "warehouses" });
        }
        catch (DomainRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var branches = await organizationService.GetBranchesAsync(onlyActive: true, cancellationToken: cancellationToken);
            model.AvailableBranches = branches.Select(x => new SelectListItem($"{x.Name} ({x.CompanyTradeName})", x.Id.ToString())).ToList();
            return View("WarehouseForm", model);
        }
    }

    [Authorize(Policy = PermissionCodes.OrganizationsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleWarehouseActive(Guid id, CancellationToken cancellationToken)
    {
        await organizationService.ToggleWarehouseActiveAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index), new { tab = "warehouses" });
    }
}
