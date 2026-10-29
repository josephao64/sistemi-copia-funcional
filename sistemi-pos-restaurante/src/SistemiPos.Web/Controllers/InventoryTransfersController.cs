using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemiPos.Application.Catalog;
using SistemiPos.Application.Common.Exceptions;
using SistemiPos.Application.Inventory;
using SistemiPos.Application.Organizations;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Inventory;
using SistemiPos.Web.Models.Inventory;
using SistemiPos.Web.Services;

namespace SistemiPos.Web.Controllers;

[Authorize(Policy = PermissionCodes.InventoryTransfersView)]
public sealed class InventoryTransfersController(
    IInventoryTransferService transferService,
    IInventoryRepository inventoryRepository,
    ICatalogRepository catalogRepository,
    IOrganizationService organizationService,
    INavigationPermissionService navigationPermissionService) : Controller
{
    public async Task<IActionResult> Index(
        Guid? originWarehouseId = null,
        Guid? destinationWarehouseId = null,
        InventoryTransferStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var transfers = await transferService.GetTransfersAsync(originWarehouseId, destinationWarehouseId, status, cancellationToken);
        var warehouses = await organizationService.GetWarehousesAsync(onlyActive: true, cancellationToken: cancellationToken);

        var warehouseList = warehouses
            .Select(w => new SelectListItem($"{w.BranchName} - {w.Name}", w.Id.ToString()))
            .ToList();

        var viewModel = new InventoryTransferIndexViewModel
        {
            Transfers = transfers,
            OriginWarehouseId = originWarehouseId,
            DestinationWarehouseId = destinationWarehouseId,
            Status = status,
            Warehouses = warehouseList
        };

        return View(viewModel);
    }

    [Authorize(Policy = PermissionCodes.InventoryTransfersCreate)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken = default)
    {
        var warehouses = await organizationService.GetWarehousesAsync(onlyActive: true, cancellationToken: cancellationToken);
        var warehouseList = warehouses
            .Select(w => new SelectListItem($"{w.BranchName} - {w.Name}", w.Id.ToString()))
            .ToList();

        var viewModel = new CreateInventoryTransferViewModel
        {
            Warehouses = warehouseList
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PermissionCodes.InventoryTransfersCreate)]
    public async Task<IActionResult> Create(CreateInventoryTransferViewModel model, CancellationToken cancellationToken = default)
    {
        if (!model.OriginWarehouseId.HasValue || model.OriginWarehouseId.Value == Guid.Empty ||
            !model.DestinationWarehouseId.HasValue || model.DestinationWarehouseId.Value == Guid.Empty)
        {
            ModelState.AddModelError(string.Empty, "Debe seleccionar la bodega de origen y de destino.");
        }

        if (model.OriginWarehouseId.HasValue && model.DestinationWarehouseId.HasValue &&
            model.OriginWarehouseId.Value == model.DestinationWarehouseId.Value)
        {
            ModelState.AddModelError(string.Empty, "La bodega de origen no puede ser igual a la bodega de destino.");
        }

        var validItems = model.Items?.Where(x => x.ProductId != Guid.Empty && x.Quantity > 0).ToList() ?? [];
        if (validItems.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Debe incluir al menos un producto con cantidad mayor a cero.");
        }

        if (!ModelState.IsValid)
        {
            var warehouses = await organizationService.GetWarehousesAsync(onlyActive: true, cancellationToken: cancellationToken);
            model.Warehouses = warehouses
                .Select(w => new SelectListItem($"{w.BranchName} - {w.Name}", w.Id.ToString()))
                .ToList();
            return View(model);
        }

        try
        {
            var request = new CreateInventoryTransferRequest(
                model.OriginWarehouseId!.Value,
                model.DestinationWarehouseId!.Value,
                validItems.Select(x => new CreateInventoryTransferItemRequest(
                    x.ProductId,
                    x.Quantity,
                    x.AssignedUnitCost)).ToList(),
                model.Observations);

            var transfer = await transferService.CreateTransferAsync(request, cancellationToken);
            TempData["SuccessMessage"] = $"Traslado {transfer.Number} creado exitosamente como borrador.";
            return RedirectToAction(nameof(Details), new { id = transfer.Id });
        }
        catch (DomainRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var warehouses = await organizationService.GetWarehousesAsync(onlyActive: true, cancellationToken: cancellationToken);
            model.Warehouses = warehouses
                .Select(w => new SelectListItem($"{w.BranchName} - {w.Name}", w.Id.ToString()))
                .ToList();
            return View(model);
        }
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken = default)
    {
        var transfer = await transferService.GetTransferByIdAsync(id, cancellationToken);
        if (transfer is null)
        {
            return NotFound();
        }

        var permissions = await navigationPermissionService.GetPermissionCodesAsync(cancellationToken);
        var viewModel = new InventoryTransferDetailViewModel
        {
            Transfer = transfer,
            CanDispatch = transfer.Status == InventoryTransferStatus.Draft && permissions.Contains(PermissionCodes.InventoryTransfersDispatch),
            CanReceive = transfer.Status == InventoryTransferStatus.Dispatched && permissions.Contains(PermissionCodes.InventoryTransfersReceive),
            CanCancel = transfer.Status == InventoryTransferStatus.Draft && permissions.Contains(PermissionCodes.InventoryTransfersCreate)
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PermissionCodes.InventoryTransfersDispatch)]
    public async Task<IActionResult> Dispatch(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var transfer = await transferService.DispatchTransferAsync(id, cancellationToken);
            TempData["SuccessMessage"] = $"Traslado {transfer.Number} despachado exitosamente. Se descontó la existencia de la bodega de origen.";
        }
        catch (DomainRuleException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PermissionCodes.InventoryTransfersReceive)]
    public async Task<IActionResult> Receive(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var transfer = await transferService.ReceiveTransferAsync(id, null, cancellationToken);
            TempData["SuccessMessage"] = $"Traslado {transfer.Number} recibido exitosamente. Se incrementó la existencia en la bodega destino con su costo asignado.";
        }
        catch (DomainRuleException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PermissionCodes.InventoryTransfersCreate)]
    public async Task<IActionResult> Cancel(Guid id, string? reason, CancellationToken cancellationToken = default)
    {
        try
        {
            var transfer = await transferService.CancelTransferAsync(id, new CancelInventoryTransferRequest(reason), cancellationToken);
            TempData["SuccessMessage"] = $"Traslado {transfer.Number} cancelado exitosamente.";
        }
        catch (DomainRuleException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> SearchProducts(string? query, Guid? warehouseId, CancellationToken cancellationToken = default)
    {
        List<InventoryStockDto> warehouseStocks = [];
        if (warehouseId.HasValue && warehouseId.Value != Guid.Empty)
        {
            warehouseStocks = await inventoryRepository.GetStocksAsync(onlyLowStock: false, branchId: null, warehouseId: warehouseId.Value, cancellationToken: cancellationToken);
        }

        var stockMap = warehouseStocks.GroupBy(s => s.ProductId).ToDictionary(g => g.Key, g => g.First().CurrentStock);

        var products = await catalogRepository.SearchProductsAsync(query, cancellationToken);
        var activeProducts = products.Where(p => p.IsActive).ToList();

        var sortedProducts = activeProducts
            .OrderByDescending(p => stockMap.GetValueOrDefault(p.Id, 0) > 0)
            .ThenBy(p => p.Name)
            .Take(30)
            .ToList();

        var results = sortedProducts.Select(p => new
        {
            id = p.Id,
            name = p.DisplayName,
            code = p.InternalCode,
            cost = p.LastCost > 0 ? p.LastCost : p.PreviousCost,
            currentStock = stockMap.GetValueOrDefault(p.Id, 0)
        }).ToList();

        return Json(results);
    }
}
