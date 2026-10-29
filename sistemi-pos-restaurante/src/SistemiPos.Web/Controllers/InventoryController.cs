using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemiPos.Application.Common.Exceptions;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Inventory;
using SistemiPos.Application.Exports;
using SistemiPos.Application.Reports;
using SistemiPos.Application.Security;
using SistemiPos.Application.Organizations;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Inventory;
using SistemiPos.Web.Models.Inventory;

namespace SistemiPos.Web.Controllers;

[Authorize(Policy = PermissionCodes.InventoryView)]
public sealed class InventoryController(
    IInventoryQueryService inventoryQueryService,
    IPhysicalInventoryCountService physicalInventoryCountService,
    IExcelExportService excelExportService,
    IReportService reportService,
    IDateTimeProvider dateTimeProvider,
    IOperationContextAccessor operationContext,
    IOrganizationService organizationService) : Controller
{
    public async Task<IActionResult> Index(
        InventoryListFilter filter = InventoryListFilter.All,
        Guid? branchId = null,
        Guid? warehouseId = null,
        CancellationToken cancellationToken = default)
    {
        var authorizedBranches = await operationContext.GetAuthorizedBranchesAsync(cancellationToken);
        var targetBranchId = branchId ?? operationContext.CurrentBranchId;
        if (targetBranchId.HasValue && authorizedBranches.Count > 0 && !authorizedBranches.Any(x => x.Id == targetBranchId.Value))
        {
            targetBranchId = authorizedBranches.FirstOrDefault()?.Id;
        }

        var warehouses = await organizationService.GetWarehousesAsync(targetBranchId, onlyActive: true, cancellationToken: cancellationToken);

        var stocks = await inventoryQueryService.GetCurrentStockAsync(false, targetBranchId, warehouseId, cancellationToken);

        var availableBranches = authorizedBranches
            .Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(x.Name, x.Id.ToString(), x.Id == targetBranchId))
            .ToList();

        var availableWarehouses = warehouses
            .Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(x.Name, x.Id.ToString(), x.Id == warehouseId))
            .ToList();

        if (filter == InventoryListFilter.All)
        {
            return View(new InventoryIndexViewModel
            {
                Filter = filter,
                Stocks = stocks,
                SelectedBranchId = targetBranchId,
                SelectedWarehouseId = warehouseId,
                AvailableBranches = availableBranches,
                AvailableWarehouses = availableWarehouses
            });
        }

        var localNow = dateTimeProvider.UtcNow.ToOffset(TimeSpan.FromHours(-6));
        var dashboard = await reportService.GetDashboardSummaryAsync(localNow, cancellationToken);
        var productIds = filter switch
        {
            InventoryListFilter.LowStock => dashboard.LowStock.Select(x => x.ProductId).ToHashSet(),
            InventoryListFilter.OutOfStock => dashboard.OutOfStock.Select(x => x.ProductId).ToHashSet(),
            InventoryListFilter.WithoutMovement => dashboard.ProductsWithoutMovement.Select(x => x.ProductId).ToHashSet(),
            _ => []
        };

        return View(new InventoryIndexViewModel
        {
            Filter = filter,
            Stocks = stocks.Where(x => productIds.Contains(x.ProductId)).ToList(),
            NoMovementDetails = dashboard.ProductsWithoutMovement.ToDictionary(x => x.ProductId),
            SelectedBranchId = targetBranchId,
            SelectedWarehouseId = warehouseId,
            AvailableBranches = availableBranches,
            AvailableWarehouses = availableWarehouses
        });
    }

    [Authorize(Policy = PermissionCodes.ProductsManage)]
    [HttpGet]
    [Route("Inventory/PhysicalCount")]
    [Route("~/PhysicalInventoryCounts")]
    public async Task<IActionResult> PhysicalCount(
        Guid? id,
        string? search,
        string? category,
        PhysicalInventoryCountFilter filter = PhysicalInventoryCountFilter.All,
        CancellationToken cancellationToken = default)
    {
        if (!id.HasValue)
        {
            return View("PhysicalCountIndex", await BuildPhysicalCountIndexAsync(new CreatePhysicalInventoryCountViewModel(), cancellationToken));
        }

        try
        {
            var count = await physicalInventoryCountService.GetAsync(id.Value, cancellationToken);
            IEnumerable<PhysicalInventoryCountItemDto> items = count.Items;
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                items = items.Where(x =>
                    x.InternalCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    x.ProductName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    x.CategoryName.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                items = items.Where(x => string.Equals(x.CategoryName, category, StringComparison.OrdinalIgnoreCase));
            }

            items = filter switch
            {
                PhysicalInventoryCountFilter.Pending => items.Where(x => x.Status == PhysicalInventoryCountItemStatus.Pending),
                PhysicalInventoryCountFilter.Counted => items.Where(x => x.Status == PhysicalInventoryCountItemStatus.Counted),
                PhysicalInventoryCountFilter.Difference => items.Where(x => x.Status == PhysicalInventoryCountItemStatus.Counted && x.Difference != 0),
                PhysicalInventoryCountFilter.Shortage => items.Where(x => x.Status == PhysicalInventoryCountItemStatus.Counted && x.Difference < 0),
                PhysicalInventoryCountFilter.Surplus => items.Where(x => x.Status == PhysicalInventoryCountItemStatus.Counted && x.Difference > 0),
                PhysicalInventoryCountFilter.RecountRequired => items.Where(x => x.Status == PhysicalInventoryCountItemStatus.RecountRequired),
                _ => items
            };

            return View(new PhysicalInventoryCountPageViewModel
            {
                Count = count,
                VisibleItems = items.ToList(),
                Search = search,
                Category = category,
                Filter = filter
            });
        }
        catch (NotFoundException exception)
        {
            TempData["Error"] = exception.Message;
            return RedirectToAction(nameof(PhysicalCount));
        }
    }

    public async Task<IActionResult> Kardex(
        Guid? productId,
        Guid? branchId = null,
        Guid? warehouseId = null,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var authorizedBranches = await operationContext.GetAuthorizedBranchesAsync(cancellationToken);
        var targetBranchId = branchId ?? operationContext.CurrentBranchId;
        if (targetBranchId.HasValue && authorizedBranches.Count > 0 && !authorizedBranches.Any(x => x.Id == targetBranchId.Value))
        {
            targetBranchId = authorizedBranches.FirstOrDefault()?.Id;
        }

        var warehouses = await organizationService.GetWarehousesAsync(targetBranchId, onlyActive: true, cancellationToken: cancellationToken);
        if (warehouseId.HasValue && warehouses.Count > 0 && !warehouses.Any(w => w.Id == warehouseId.Value))
        {
            warehouseId = null;
        }

        var movements = await inventoryQueryService.GetKardexAsync(productId, page, pageSize, warehouseId, cancellationToken);
        ViewData["ProductId"] = productId;
        ViewData["BranchId"] = targetBranchId;
        ViewData["WarehouseId"] = warehouseId;
        ViewData["AvailableBranches"] = authorizedBranches
            .Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(x.Name, x.Id.ToString(), x.Id == targetBranchId))
            .ToList();
        ViewData["AvailableWarehouses"] = warehouses
            .Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(x.Name, x.Id.ToString(), x.Id == warehouseId))
            .ToList();
        return View(movements);
    }

    [HttpGet]
    public async Task<IActionResult> Export(
        InventoryListFilter filter = InventoryListFilter.All,
        Guid? productId = null,
        Guid? branchId = null,
        Guid? warehouseId = null,
        CancellationToken cancellationToken = default)
    {
        if (productId is not null)
        {
            var movements = await inventoryQueryService.GetKardexExportAsync(productId, warehouseId, cancellationToken);
            var columns = new List<ExcelColumn>
            {
                new("Fecha", ExcelCellType.DateTime, 20),
                new("Producto", ExcelCellType.Text, 30),
                new("Bodega", ExcelCellType.Text, 25),
                new("Tipo", ExcelCellType.Text, 20),
                new("Cantidad", ExcelCellType.Decimal, 14),
                new("Anterior", ExcelCellType.Decimal, 14),
                new("Nueva", ExcelCellType.Decimal, 14),
                new("Usuario", ExcelCellType.Text, 20),
                new("Documento", ExcelCellType.Text, 24)
            };
            var rows = movements.Select(x => (IReadOnlyList<object?>)[
                x.OccurredAtUtc,
                x.ProductName,
                x.WarehouseName ?? "Sin bodega",
                x.Type.ToString(),
                x.Quantity,
                x.PreviousStock,
                x.NewStock,
                x.UserName,
                x.SourceDocument
            ]).ToList();
            return File(excelExportService.CreateWorkbook("Kardex", columns, rows), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "kardex.xlsx");
        }

        var targetBranchId = branchId ?? operationContext.CurrentBranchId;
        var stocks = await inventoryQueryService.GetCurrentStockAsync(false, targetBranchId, warehouseId, cancellationToken);
        var localNow = dateTimeProvider.UtcNow.ToOffset(TimeSpan.FromHours(-6));
        var dashboard = await reportService.GetDashboardSummaryAsync(localNow, cancellationToken);
        var ids = filter switch
        {
            InventoryListFilter.LowStock => dashboard.LowStock.Select(x => x.ProductId).ToHashSet(),
            InventoryListFilter.OutOfStock => dashboard.OutOfStock.Select(x => x.ProductId).ToHashSet(),
            InventoryListFilter.WithoutMovement => dashboard.ProductsWithoutMovement.Select(x => x.ProductId).ToHashSet(),
            _ => null
        };
        if (ids is not null) stocks = stocks.Where(x => ids.Contains(x.ProductId)).ToList();
        var columns2 = new List<ExcelColumn>
        {
            new("Código", ExcelCellType.Text, 16),
            new("Producto", ExcelCellType.Text, 30),
            new("Categoría", ExcelCellType.Text, 22),
            new("Sucursal", ExcelCellType.Text, 22),
            new("Bodega", ExcelCellType.Text, 22),
            new("Existencia", ExcelCellType.Decimal, 14),
            new("Mínimo", ExcelCellType.Decimal, 14),
            new("Estado", ExcelCellType.Text, 18)
        };
        var rows2 = stocks.Select(x => (IReadOnlyList<object?>)[
            x.InternalCode,
            x.ProductName,
            x.CategoryName,
            x.BranchName ?? "Sin asignar",
            x.WarehouseName ?? "Sin asignar",
            x.CurrentStock,
            x.MinimumStock,
            x.CurrentStock == 0 ? "Agotado" : x.IsLowStock ? "Existencia baja" : "Normal"
        ]).ToList();
        var label = filter switch { InventoryListFilter.LowStock => "existencia-baja", InventoryListFilter.OutOfStock => "agotados", InventoryListFilter.WithoutMovement => "sin-movimientos", _ => "inventario" };
        return File(excelExportService.CreateWorkbook("Inventario", columns2, rows2), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{label}.xlsx");
    }

    [Authorize(Policy = PermissionCodes.ProductsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePhysicalCount(CreatePhysicalInventoryCountViewModel model, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return View("PhysicalCountIndex", await BuildPhysicalCountIndexAsync(model, cancellationToken));
        }

        try
        {
            var id = await physicalInventoryCountService.CreateAsync(
                new CreatePhysicalInventoryCountRequest(model.Name, model.Scope, model.CategoryIds),
                cancellationToken);
            TempData["Success"] = "La toma física fue creada. Ya puede iniciar el conteo por producto.";
            return RedirectToAction(nameof(PhysicalCount), new { id });
        }
        catch (DomainRuleException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("PhysicalCountIndex", await BuildPhysicalCountIndexAsync(model, cancellationToken));
        }
    }

    [Authorize(Policy = PermissionCodes.ProductsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BeginPhysicalCountItem(Guid id, Guid itemId, CancellationToken cancellationToken = default)
    {
        try
        {
            return Json(await physicalInventoryCountService.BeginItemAsync(id, itemId, cancellationToken));
        }
        catch (Exception exception) when (exception is DomainRuleException or NotFoundException)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [Authorize(Policy = PermissionCodes.ProductsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePhysicalCountItem(
        Guid id,
        Guid itemId,
        [FromBody] SavePhysicalInventoryCountItemRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await physicalInventoryCountService.SaveItemAsync(id, itemId, request, cancellationToken);
            return result.Saved ? Json(result) : Conflict(result);
        }
        catch (Exception exception) when (exception is DomainRuleException or NotFoundException)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [Authorize(Policy = PermissionCodes.ProductsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FinalizePhysicalCount(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await physicalInventoryCountService.FinalizeAsync(id, cancellationToken);
            TempData["Success"] = "La toma fue finalizada y sus diferencias se aplicaron transaccionalmente al inventario.";
        }
        catch (Exception exception) when (exception is DomainRuleException or NotFoundException or Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            TempData["Error"] = exception is Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException
                ? "El inventario cambió durante la finalización. No se aplicó ningún ajuste; intente finalizar nuevamente."
                : exception.Message;
        }

        return RedirectToAction(nameof(PhysicalCount), new { id });
    }

    [Authorize(Policy = PermissionCodes.ProductsManage)]
    [HttpGet]
    public async Task<IActionResult> ExportPhysicalCount(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var export = await physicalInventoryCountService.GetExportAsync(id, cancellationToken);
            var count = export.Count;
            var columns = new List<ExcelColumn>
            {
                new("Código", ExcelCellType.Text, 16),
                new("Producto", ExcelCellType.Text, 28),
                new("Categoría", ExcelCellType.Text, 22),
                new("Existencia sistema de referencia", ExcelCellType.Decimal, 24),
                new("Existencia física contada", ExcelCellType.Decimal, 22),
                new("Diferencia", ExcelCellType.Decimal, 14),
                new("Resultado", ExcelCellType.Text, 14),
                new("Comentario", ExcelCellType.Text, 34),
                new("Existencia operativa antes del ajuste", ExcelCellType.Decimal, 26),
                new("Ajuste aplicado", ExcelCellType.Decimal, 16),
                new("Existencia después del ajuste", ExcelCellType.Decimal, 24)
            };
            var rows = count.Items.Select(item => (IReadOnlyList<object?>)new object?[]
            {
                item.InternalCode,
                item.ProductName,
                item.CategoryName,
                item.ReferenceStock,
                item.CountedStock,
                item.Difference,
                item.Difference switch { < 0 => "Faltante", > 0 => "Sobrante", _ => "Correcto" },
                item.Comment,
                item.OperationalStockBeforeAdjustment,
                item.AppliedAdjustment,
                item.OperationalStockAfterAdjustment
            }).ToList();
            var header = new List<ExcelReportLine>
            {
                new("Nombre de la toma", count.Name),
                new("Fecha de creación", count.CreatedAtUtc, ExcelCellType.DateTime),
                new("Fecha de finalización", count.FinalizedAtUtc, ExcelCellType.DateTime),
                new("Usuario creador", count.CreatedBy),
                new("Usuario que finalizó", count.FinalizedBy),
                new("Alcance", count.Scope == PhysicalInventoryCountScope.AllInventory ? "Todo el inventario" : "Categorías"),
                new("Categorías incluidas", count.Categories.Count == 0 ? "Todas" : string.Join(", ", count.Categories))
            };
            var summary = new List<ExcelReportLine>
            {
                new("Total productos", count.Summary.TotalProducts, ExcelCellType.Integer),
                new("Productos correctos", count.Summary.CorrectProducts, ExcelCellType.Integer),
                new("Productos con faltante", count.Summary.ShortageProducts, ExcelCellType.Integer),
                new("Productos con sobrante", count.Summary.SurplusProducts, ExcelCellType.Integer),
                new("Diferencia total positiva", export.TotalPositiveDifference, ExcelCellType.Decimal),
                new("Diferencia total negativa", export.TotalNegativeDifference, ExcelCellType.Decimal)
            };
            var content = excelExportService.CreateWorkbook("Toma física", columns, rows, header, summary);
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"toma-fisica-{count.CreatedAtUtc:yyyyMMdd}-{count.Id:N}.xlsx");
        }
        catch (Exception exception) when (exception is DomainRuleException or NotFoundException)
        {
            TempData["Error"] = exception.Message;
            return RedirectToAction(nameof(PhysicalCount), new { id });
        }
    }

    private async Task<PhysicalInventoryCountIndexViewModel> BuildPhysicalCountIndexAsync(
        CreatePhysicalInventoryCountViewModel create,
        CancellationToken cancellationToken) =>
        new()
        {
            Create = create,
            Categories = await physicalInventoryCountService.GetCategoryOptionsAsync(cancellationToken),
            Counts = await physicalInventoryCountService.GetHistoryAsync(cancellationToken)
        };
}
