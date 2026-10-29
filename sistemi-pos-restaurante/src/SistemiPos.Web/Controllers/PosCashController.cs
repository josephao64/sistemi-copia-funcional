using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Cash;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Application.Security;

namespace SistemiPos.Web.Controllers;

[Authorize(Policy = PermissionCodes.SalesManage)]
[Route("pos/caja")]
public sealed class PosCashController(IPosCashService cash) : Controller
{
    [HttpGet("estado")]
    public async Task<IActionResult> State(Guid? terminalId, CancellationToken ct) => await Run(async () => Ok(await cash.StateAsync(terminalId, ct)));

    [HttpGet("resultado")]
    public async Task<IActionResult> Result(Guid terminalId, string kind, string key, CancellationToken ct) => await Run(async () =>
        await cash.ResultAsync(terminalId, kind, key, ct) is { } result ? Ok(result) : NotFound(new { code = "OPERATION_NOT_FOUND", message = "La operación aún no tiene un resultado confirmado." }));

    [HttpGet("{id:guid}/comprobante")]
    public async Task<IActionResult> Receipt(Guid id, CancellationToken ct) => await Run(async () => View("Receipt", await cash.ReceiptAsync(id, ct)));

    [HttpPost("abrir"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Open([FromBody] PosCashOpen request, CancellationToken ct) =>
        await Run(async () => Ok(await cash.OpenAsync(request, Request.Headers["Idempotency-Key"].ToString(), ct)));

    [HttpPost("movimiento"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Movement([FromBody] PosCashMovement request, CancellationToken ct) =>
        await Run(async () => Ok(await cash.MovementAsync(request, Request.Headers["Idempotency-Key"].ToString(), ct)));

    [HttpPost("cerrar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Close([FromBody] PosCashClose request, CancellationToken ct) =>
        await Run(async () => Ok(await cash.CloseAsync(request, Request.Headers["Idempotency-Key"].ToString(), ct)));

    private async Task<IActionResult> Run(Func<Task<IActionResult>> operation)
    {
        Response.Headers.CacheControl = "no-store";
        if (!ModelState.IsValid) return UnprocessableEntity(new { code = "INVALID_REQUEST", message = "Revisa los campos y el formato de los importes." });
        try { return await operation(); }
        catch (CashOperationException ex) { return StatusCode(ex.Status, new { code = ex.Code, message = ex.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { code = "CASH_SESSION_CHANGED", message = "La caja cambió durante la operación. Actualiza el estado antes de reintentar." }); }
        catch (DomainRuleException ex) { return UnprocessableEntity(new { code = "CASH_RULE", message = ex.Message }); }
    }
}
