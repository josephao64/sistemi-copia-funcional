using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Common.Exceptions;
using SistemiPos.Application.Sales;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Web.Controllers;

[Authorize]
public sealed class DiscountApprovalsController(ISaleService saleService) : Controller
{
    [Authorize(Policy = PermissionCodes.DiscountsAuthorize)]
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await saleService.GetPendingDiscountApprovalsAsync(cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Status(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var approval = await saleService.GetDiscountApprovalAsync(id, cancellationToken);
            return approval is null ? NotFound(new { message = "Solicitud no encontrada." }) : Json(new
            {
                status = approval.Status.ToString(),
                approval.DecisionComment,
                approval.ResolvedByUserName
            });
        }
        catch (DomainRuleException ex) { return Forbid(ex.Message); }
    }

    [Authorize(Policy = PermissionCodes.DiscountsAuthorize)]
    [HttpGet]
    public async Task<IActionResult> PendingCount(CancellationToken cancellationToken) =>
        Json(new { count = (await saleService.GetPendingDiscountApprovalsAsync(cancellationToken)).Count });

    [Authorize(Policy = PermissionCodes.DiscountsAuthorize)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(Guid id, bool approve, string? comment, CancellationToken cancellationToken)
    {
        try
        {
            await saleService.DecideDiscountApprovalAsync(id, approve, comment, cancellationToken);
            TempData["Success"] = approve ? "Descuento aprobado." : "Descuento rechazado.";
        }
        catch (DomainRuleException ex) { TempData["Error"] = ex.Message; }
        catch (NotFoundException ex) { TempData["Error"] = ex.Message; }
        catch (DbUpdateConcurrencyException) { TempData["Error"] = "La solicitud ya fue resuelta por otro usuario."; }
        return RedirectToAction(nameof(Index));
    }
}
