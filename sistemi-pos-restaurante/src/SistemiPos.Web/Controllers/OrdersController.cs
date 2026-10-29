using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Cash;
using SistemiPos.Application.Catalog;
using SistemiPos.Application.Common.Exceptions;
using SistemiPos.Application.Orders;
using SistemiPos.Application.Organizations;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Web.Models.Orders;

namespace SistemiPos.Web.Controllers;

[Authorize(Policy = PermissionCodes.OrdersView), Route("pedidos")]
public sealed class OrdersController(IOrderService orders, ICatalogService catalog, ICashService cash,
    IOperationContextAccessor context, IOrganizationService organizations) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var result = await orders.GetOrdersAsync(ct);
        ViewBag.TerminalCount = (await orders.GetTerminalsAsync(ct)).Count;
        return View(result);
    }

    [HttpGet("terminales")]
    public async Task<IActionResult> Terminals(CancellationToken ct)
    {
        var terminals = await orders.GetTerminalsAsync(ct);
        return View(new TerminalListViewModel
        {
            BranchName = context.CurrentBranchName ?? "Sucursal actual",
            Terminals = terminals,
            Warehouses = context.CurrentBranchId.HasValue
                ? await organizations.GetWarehousesAsync(context.CurrentBranchId, false, ct) : []
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        try
        {
            var order = await orders.GetOrderAsync(id, ct);
            var products = await catalog.SearchProductsAsync(null, context.CurrentBranchId, ct);
            ViewBag.OrderProducts = products;
            return View(order);
        }
        catch (NotFoundException) { return NotFound(); }
    }

    [Authorize(Policy = PermissionCodes.OrdersCreate), HttpGet("nuevo")]
    public async Task<IActionResult> Create(CancellationToken ct) => View(await PopulateAsync(new(), ct));

    [Authorize(Policy = PermissionCodes.OrdersCreate), HttpPost("nuevo"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(NewOrderViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var session = await cash.GetCurrentSessionAsync(ct)
                    ?? throw new DomainRuleException("Abre tu caja antes de crear un pedido.");
                var id = await orders.CreateOrderAsync(new CreateOrderRequest(model.TerminalId, session.Id,
                    model.Lines.Select(x => new OrderLineRequest(x.ProductId, x.Quantity)).ToList()), ct);
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (DomainRuleException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
            catch (NotFoundException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
        }
        return View(await PopulateAsync(model, ct));
    }

    [HttpPost("{id:guid}/operaciones"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(Guid id, OrderActionRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["OrderError"] = "Revisa los datos de la operación.";
            return RedirectToAction(nameof(Details), new { id });
        }
        try { await orders.ApplyAsync(id, request, ct); }
        catch (DomainRuleException ex) { TempData["OrderError"] = ex.Message; }
        catch (NotFoundException) { return NotFound(); }
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = PermissionCodes.PosTerminalsManage), HttpGet("terminales/nueva")]
    public async Task<IActionResult> Terminal(CancellationToken ct) => View(new TerminalFormViewModel
    {
        Warehouses = context.CurrentBranchId.HasValue
            ? await organizations.GetWarehousesAsync(context.CurrentBranchId, true, ct) : []
    });

    [Authorize(Policy = PermissionCodes.PosTerminalsManage), HttpPost("terminales/nueva"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Terminal(TerminalFormViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await orders.CreateTerminalAsync(new SaveTerminalRequest(model.WarehouseId, model.Code, model.RegisterCode), ct);
                TempData["Success"] = $"Terminal {model.Code.Trim().ToUpperInvariant()} guardada correctamente. Ya está disponible en su sucursal.";
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return Json(new { reload = true });
                return RedirectToAction(nameof(Terminals));
            }
            catch (DomainRuleException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
            catch (NotFoundException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
        }
        model.Warehouses = context.CurrentBranchId.HasValue
            ? await organizations.GetWarehousesAsync(context.CurrentBranchId, true, ct) : [];
        return View(model);
    }

    [Authorize(Policy = PermissionCodes.PosTerminalsManage), HttpGet("terminales/{id:guid}/editar")]
    public async Task<IActionResult> EditTerminal(Guid id, CancellationToken ct)
    {
        try
        {
            var terminal = await orders.GetTerminalForEditAsync(id, ct);
            return View("Terminal", new TerminalFormViewModel
            {
                Id = terminal.Id, Code = terminal.Code, RegisterCode = terminal.RegisterCode, WarehouseId = terminal.WarehouseId,
                Warehouses = await organizations.GetWarehousesAsync(terminal.BranchId, false, ct)
            });
        }
        catch (NotFoundException) { return NotFound(); }
    }

    [Authorize(Policy = PermissionCodes.PosTerminalsManage), HttpPost("terminales/{id:guid}/editar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTerminal(Guid id, TerminalFormViewModel model, CancellationToken ct)
    {
        model.Id = id;
        if (ModelState.IsValid)
        {
            try
            {
                await orders.UpdateTerminalAsync(id, new SaveTerminalRequest(model.WarehouseId, model.Code, model.RegisterCode), ct);
                TempData["Success"] = "Terminal actualizada correctamente.";
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return Json(new { reload = true });
                return RedirectToAction(nameof(Terminals));
            }
            catch (DomainRuleException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
            catch (NotFoundException) { return NotFound(); }
        }
        model.Warehouses = context.CurrentBranchId.HasValue
            ? await organizations.GetWarehousesAsync(context.CurrentBranchId, false, ct) : [];
        return View("Terminal", model);
    }

    private async Task<NewOrderViewModel> PopulateAsync(NewOrderViewModel model, CancellationToken ct)
    {
        model.Terminals = await orders.GetTerminalsAsync(ct);
        model.Products = (await catalog.SearchProductsAsync(null, context.CurrentBranchId, ct))
            .Where(x => x.IsActive && !x.ManagesVariants && x.ArticleType is ArticleType.Preparado or ArticleType.Reventa or ArticleType.ProductoDeVenta or ArticleType.Bebida).ToList();
        return model;
    }

    [Authorize(Policy = PermissionCodes.OrdersUpdate), HttpPost("{id:guid}/lineas/{lineId:guid}/revision"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Revise(Guid id, Guid lineId, ReviseOrderLineRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["OrderError"] = "Revisa los datos de la revisión."; return RedirectToAction(nameof(Details), new { id }); }
        try { await orders.ReviseLineAsync(id, lineId, request, ct); }
        catch (DomainRuleException ex) { TempData["OrderError"] = ex.Message; }
        catch (NotFoundException) { return NotFound(); }
        return RedirectToAction(nameof(Details), new { id });
    }
}
