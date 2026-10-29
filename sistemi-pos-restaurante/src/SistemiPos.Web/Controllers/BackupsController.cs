using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemiPos.Application.Reports;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Web.Controllers;
[Authorize(Policy = PermissionCodes.BackupsManage)]
[Route("control/respaldos")]
public sealed class BackupsController(IBackupService service) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await service.StatusAsync(ct));
    [HttpPost("crear"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        try { await service.CreateAsync(ct); TempData["BackupMessage"]="Respaldo creado con manifiesto y suma SHA256. El simulacro de restauración se registra por separado."; }
        catch(DomainRuleException ex) { TempData["BackupMessage"]=ex.Message; }
        return RedirectToAction(nameof(Index));
    }
}
