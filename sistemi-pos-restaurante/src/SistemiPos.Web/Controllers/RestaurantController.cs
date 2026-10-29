using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Restaurant;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Restaurant;

namespace SistemiPos.Web.Controllers;

[Authorize(Policy = PermissionCodes.OrdersView)]
[Route("restaurante")]
public sealed class RestaurantController(IRestaurantService service, IOperationContextAccessor operationContext) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await service.WorkspaceAsync(ct, includeMenu: false));
    [HttpGet("nuevo"), Authorize(Policy = PermissionCodes.OrdersCreate)]
    public async Task<IActionResult> New(CancellationToken ct) => View("Form", await service.WorkspaceAsync(ct));
    [HttpGet("{id:guid}/editar"), Authorize(Policy = PermissionCodes.OrdersUpdate)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var order = await service.GetAsync(id, ct);
        if (order.Status != RestaurantOrderStatus.Draft) return RedirectToAction(nameof(Details), new { id });
        ViewBag.Draft = order; return View("Form", await service.WorkspaceAsync(ct, order.WarehouseId));
    }
    [HttpGet("menu")]
    public async Task<IActionResult> Menu(Guid warehouseId, CancellationToken ct)
    { var workspace = await service.WorkspaceAsync(ct, warehouseId); return Ok(new { workspace.Menu }); }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    { var order = await service.GetAsync(id, ct); ViewBag.Workspace = await service.WorkspaceAsync(ct, order.WarehouseId); return View(order); }
    [HttpGet("cocina"), Authorize(Policy = PermissionCodes.KitchenOrdersView)]
    public async Task<IActionResult> Kitchen(CancellationToken ct) => View(await service.WorkspaceAsync(ct, includeMenu: false));
    [HttpGet("mesas"), Authorize(Policy = PermissionCodes.TablesView)]
    public async Task<IActionResult> Tables(CancellationToken ct) => View(await service.WorkspaceAsync(ct, includeMenu: false));
    [HttpGet("delivery"), Authorize(Policy = PermissionCodes.DeliveryManage)]
    public async Task<IActionResult> Delivery(CancellationToken ct) => View(await service.WorkspaceAsync(ct, includeMenu: false));
    [HttpGet("configuracion"), Authorize(Policy = PermissionCodes.RestaurantConfigure)]
    public async Task<IActionResult> Configuration(CancellationToken ct)
    {
        var workspace = await service.WorkspaceAsync(ct, includeMenu: false);
        ViewBag.Products = await service.MenuCandidatesAsync(ct);
        ViewBag.Components = (await service.ProductsAsync(ct))
            .Where(x => x.IsActive && !x.ManagesVariants && x.BaseUnit is { IsActive: true })
            .Select(x => new { x.Id, Name = x.DisplayName, Unit = x.BaseUnit!.Code })
            .OrderBy(x => x.Name).ToList();
        ViewBag.AuthorizedBranches = (await operationContext.GetAuthorizedBranchesAsync(ct))
            .Where(x => x.IsActive && x.Id != workspace.BranchId)
            .Select(x => new { x.Id, x.Name, x.Code })
            .ToList();
        return View(workspace);
    }
    [HttpPost("configuracion"), Authorize(Policy = PermissionCodes.RestaurantConfigure), ValidateAntiForgeryToken]
    public async Task<IActionResult> Configure([FromBody] SetupRequest request, CancellationToken ct) => await Respond(async () =>
    { await service.SaveSetupAsync(request.Definition, request.Revision, ct); return new { url = Url.Action(nameof(Configuration)) }; });
    [HttpPost("copiar-menu"), Authorize(Policy = PermissionCodes.RestaurantConfigure), ValidateAntiForgeryToken]
    public async Task<IActionResult> CopyMenu([FromBody] CopyMenuCommand request, CancellationToken ct) => await Respond(async () =>
    {
        var definition = await service.CopyBranchMenuAsync(request.SourceBranchId, ct);
        return new { definition };
    });
    [HttpPost("borrador"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDraft([FromBody] DraftCommand request, CancellationToken ct) => await Respond(async () => new { url = Url.Action(nameof(Details), new { id = await service.SaveDraftAsync(request, ct) }) });
    [HttpPost("{id:guid}/accion"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Act(Guid id, [FromBody] OrderAction request, CancellationToken ct) => await Respond(async () =>
    { await service.ActAsync(id, request, ct); return new { url = Url.Action(nameof(Details), new { id }) }; });
    [HttpPost("{id:guid}/lineas"), Authorize(Policy = PermissionCodes.OrdersUpdate), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddLines(Guid id, [FromBody] AddOrderLines request, CancellationToken ct) => await Respond(async () =>
    { await service.AddLinesAsync(id, request, ct); return new { url = Url.Action(nameof(Details), new { id }) }; });
    [HttpGet("{id:guid}/checkout"), Authorize(Policy = PermissionCodes.SalesManage)]
    public async Task<IActionResult> Checkout(Guid id, CancellationToken ct) => Ok(await service.CheckoutAsync(id, ct));
    private async Task<IActionResult> Respond(Func<Task<object>> action)
    {
        if (!ModelState.IsValid) return BadRequest(new { error = "Revisa los datos ingresados." });
        try { return Ok(await action()); }
        catch (DomainRuleException ex) { return Conflict(new { error = ex.Message }); }
    }
    public sealed record SetupRequest(int Revision, RestaurantDefinition Definition);
}
