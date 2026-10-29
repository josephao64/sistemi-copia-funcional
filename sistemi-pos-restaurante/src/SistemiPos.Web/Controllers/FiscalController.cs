using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemiPos.Application.Sales;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Web.Controllers;
[Authorize(Policy = PermissionCodes.FiscalManage)]
[Route("control/fiscal")]
public sealed class FiscalController(IFiscalService service) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await service.ListAsync(ct));
    [HttpPost("{id:guid}/preparar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Prepare(Guid id, CancellationToken ct)
    {
        try { await service.PrepareAsync(id,ct); TempData["FiscalMessage"]="Documento preparado. Revisa el estado fiscal antes de continuar."; }
        catch(DomainRuleException ex) { TempData["FiscalMessage"]=ex.Message; }
        return RedirectToAction(nameof(Index));
    }
}
