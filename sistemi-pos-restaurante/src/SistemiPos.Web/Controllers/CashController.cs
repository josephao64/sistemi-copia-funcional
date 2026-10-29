using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemiPos.Application.Cash;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Web.Models.Cash;
using SistemiPos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Settings;

namespace SistemiPos.Web.Controllers;

[Authorize(Policy = PermissionCodes.CashView)]
public sealed class CashController(ICashService cashService, AppDbContext dbContext, ISettingsService settingsService) : Controller
{
    [Authorize(Policy = PermissionCodes.CashClosingReport), HttpGet("/caja/{id:guid}/corte/{kind}")]
    public async Task<IActionResult> Cut(Guid id, string kind, CancellationToken ct)
    {
        if (kind is not ("x" or "z")) return NotFound();
        try { return View(await cashService.GetCutAsync(id, kind == "z", ct)); }
        catch (DomainRuleException ex) { TempData["Error"] = ex.Message; return RedirectToAction(nameof(Details), new { id }); }
    }
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var current = await cashService.GetCurrentSessionAsync(cancellationToken);
        return current is null
            ? RedirectToAction(nameof(Open))
            : RedirectToAction(nameof(Details), new { id = current.Id });
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var session = await cashService.GetSessionAsync(id, cancellationToken);
        if (session is null)
        {
            return NotFound();
        }

        if (!CanAccessSession(session)) return Forbid();
        ViewBag.Denominations = await dbContext.CashDenominations.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.Kind).ThenByDescending(x => x.Value).ToListAsync(cancellationToken);
        ViewBag.CurrencySymbol = (await settingsService.GetSystemSettingAsync(cancellationToken)).CurrencySymbol;
        ViewBag.UsedPosTotals = await cashService.GetUsedPosTotalsAsync(id, cancellationToken);
        return View(session);
    }

    [Authorize(Policy = PermissionCodes.CashManage)]
    [HttpGet]
    public async Task<IActionResult> Movement(CancellationToken cancellationToken)
    {
        var current = await cashService.GetCurrentSessionAsync(cancellationToken);
        if (current is null) return RedirectToAction(nameof(Open));
        ViewBag.CurrencySymbol = (await settingsService.GetSystemSettingAsync(cancellationToken)).CurrencySymbol;
        return View(current);
    }

    [Authorize(Policy = PermissionCodes.CashOpen)]
    [HttpGet]
    public async Task<IActionResult> Open(bool returnToSales, CancellationToken cancellationToken)
    {
        var current = await cashService.GetCurrentSessionAsync(cancellationToken);
        if (current is not null) return RedirectToAction(nameof(Details), new { id = current.Id });
        await PopulateOpenViewDataAsync(cancellationToken);
        return View(new OpenCashSessionViewModel { ReturnToSales = returnToSales });
    }

    [Authorize(Policy = PermissionCodes.CashOpen)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Open(OpenCashSessionViewModel model, List<Guid> denominationIds,
        List<string?> quantities, CancellationToken cancellationToken)
    {
        var posPaymentsEnabled = await IsPosPaymentEnabledAsync(cancellationToken);
        if (!posPaymentsEnabled)
        {
            model.UsePosDevices = false;
            model.PosDeviceIds.Clear();
        }
        else if (!model.UsePosDevices)
            model.PosDeviceIds.Clear();
        else if (model.PosDeviceIds.Count == 0)
            ModelState.AddModelError(nameof(model.PosDeviceIds), "Seleccione al menos un POS bancario.");
        if (denominationIds.Count > 0)
        {
            var submitted = denominationIds
                .Select((id, index) =>
                {
                    var parsed = ParseQuantity(index < quantities.Count ? quantities[index] : null);
                    return new { Id = id, parsed.IsValid, parsed.Quantity };
                })
                .ToList();
            ViewBag.OpeningQuantities = submitted.GroupBy(x => x.Id)
                .ToDictionary(x => x.Key, x => x.Sum(y => Math.Max(0, y.Quantity)));
            var values = await dbContext.CashDenominations.AsNoTracking()
                .Where(x => x.IsActive && denominationIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Value, cancellationToken);

            if (submitted.Any(x => !x.IsValid || x.Quantity < 0 || !values.ContainsKey(x.Id)))
            {
                ModelState.AddModelError(nameof(model.OpeningAmount), "El arqueo inicial contiene denominaciones no válidas.");
            }
            else
            {
                model.OpeningAmount = decimal.Round(submitted.Sum(x => values[x.Id] * x.Quantity), 2);
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateOpenViewDataAsync(cancellationToken);
            return View(model);
        }

        try
        {
            var id = await cashService.OpenSessionAsync(new OpenCashSessionRequest(
                model.RegisterCode ?? string.Empty, model.OpeningAmount, model.Observation, model.PosDeviceIds, model.UsePosDevices), cancellationToken);
            return model.ReturnToSales ? RedirectToRoute("pos") : RedirectToAction(nameof(Details), new { id });
        }
        catch (DomainRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateOpenViewDataAsync(cancellationToken);
            return View(model);
        }
    }

    [Authorize(Policy = PermissionCodes.CashCloseOwn)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(CloseCashSessionViewModel model, CancellationToken cancellationToken)
    {
        if (!await CanAccessSessionAsync(model.Id, cancellationToken))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Revise los datos requeridos para cerrar la caja.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        try
        {
            await cashService.CloseSessionAsync(model.Id,
                new CloseCashSessionRequest(model.CountedCash, model.Observation, model.AuthorizerUserName, model.AuthorizerPassword), cancellationToken);
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }
        catch (DomainRuleException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }
    }

    [Authorize(Policy = PermissionCodes.CashCount)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordCount(Guid id, List<Guid> denominationIds, List<string?> quantities, CancellationToken cancellationToken)
    {
        try
        {
            var parsedLines = denominationIds.Select((denominationId, index) =>
            {
                var parsed = ParseQuantity(index < quantities.Count ? quantities[index] : null);
                return new { DenominationId = denominationId, parsed.IsValid, parsed.Quantity };
            }).ToList();
            if (parsedLines.Any(x => !x.IsValid))
                throw new DomainRuleException("El arqueo contiene cantidades no válidas.");
            var lines = parsedLines.Select(x => new CashCountLineRequest(x.DenominationId, x.Quantity)).ToList();
            await cashService.RecordCountAsync(id, new RecordCashCountRequest(lines), cancellationToken);
        }
        catch (DomainRuleException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = PermissionCodes.CashCloseOwn)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordPosClosing(Guid id, Guid posDeviceId, decimal physicalTotal, string? batchReference, CancellationToken cancellationToken)
    {
        try
        {
            await cashService.RecordPosClosingAsync(id, new RecordPosClosingRequest(posDeviceId, physicalTotal, batchReference, null), cancellationToken);
        }
        catch (DomainRuleException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = PermissionCodes.CashManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMovement(CashMovementFormViewModel model, CancellationToken cancellationToken)
    {
        if (!await CanAccessSessionAsync(model.CashSessionId, cancellationToken))
        {
            return Forbid();
        }

        if (model.Type is not CashMovementType.OtherIncome and not CashMovementType.Expense)
        {
            ModelState.AddModelError(nameof(model.Type), "Seleccione Entrada o Salida.");
        }

        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest()) return BadRequest(new { message = "Revisa el tipo, monto y motivo del movimiento." });
            TempData["Error"] = "No fue posible registrar el movimiento de caja.";
            return RedirectToAction(nameof(Movement));
        }

        try
        {
            await cashService.RecordMovementAsync(model.CashSessionId, new RecordCashMovementRequest(model.Type, model.Amount, model.Reason,
                model.AuthorizerUserName, model.AuthorizerPassword), cancellationToken);
        }
        catch (DomainRuleException ex)
        {
            if (IsAjaxRequest()) return BadRequest(new { message = ex.Message });
            TempData["Error"] = ex.Message;
        }

        if (IsAjaxRequest()) return Json(new { message = "La entrada o salida fue registrada correctamente." });
        return RedirectToAction(nameof(Movement));
    }

    private bool IsAjaxRequest() => string.Equals(Request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    [Authorize(Policy = PermissionCodes.CashManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAdjustment(CashMovementFormViewModel model, CancellationToken cancellationToken)
    {
        if (!await CanAccessSessionAsync(model.CashSessionId, cancellationToken))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "No fue posible registrar el ajuste de caja.";
            return RedirectToAction(nameof(Details), new { id = model.CashSessionId });
        }

        try
        {
            await cashService.RecordAdjustmentAsync(model.CashSessionId, new RecordCashMovementRequest(model.Type, model.Amount, model.Reason), cancellationToken);
        }
        catch (DomainRuleException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = model.CashSessionId });
    }

    private bool CanAccessSession(CashSessionDto session) =>
        string.Equals(session.CashierUserName, User.Identity?.Name, StringComparison.OrdinalIgnoreCase);

    private async Task<bool> CanAccessSessionAsync(Guid id, CancellationToken cancellationToken)
    {
        var session = await cashService.GetSessionAsync(id, cancellationToken);
        return session is not null && CanAccessSession(session);
    }

    private async Task PopulateOpenViewDataAsync(CancellationToken cancellationToken)
    {
        var posPaymentsEnabled = await IsPosPaymentEnabledAsync(cancellationToken);
        ViewBag.Denominations = await dbContext.CashDenominations.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Kind)
            .ThenByDescending(x => x.Value)
            .ToListAsync(cancellationToken);
        ViewBag.CurrencySymbol = (await settingsService.GetSystemSettingAsync(cancellationToken)).CurrencySymbol;
        ViewBag.PosPaymentsEnabled = posPaymentsEnabled;
        if (!posPaymentsEnabled)
        {
            ViewBag.PosDevices = Array.Empty<SistemiPos.Domain.Configuration.PosDevice>();
            ViewBag.HasActivePosDevices = false;
            return;
        }

        var activePosDevices = await dbContext.PosDevices.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        var assignedPosIds = await (from assignment in dbContext.CashSessionPosDevices.AsNoTracking()
                                    join session in dbContext.CashSessions.AsNoTracking()
                                        on assignment.CashSessionId equals session.Id
                                    where session.Status == CashSessionStatus.Active
                                    select assignment.PosDeviceId)
            .Distinct()
            .ToListAsync(cancellationToken);
        ViewBag.HasActivePosDevices = activePosDevices.Count > 0;
        ViewBag.PosDevices = activePosDevices.Where(x => !assignedPosIds.Contains(x.Id)).ToList();
    }

    private async Task<bool> IsPosPaymentEnabledAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetSystemSettingAsync(cancellationToken);
        return settings.PosPaymentsEnabled;
    }

    private static (bool IsValid, int Quantity) ParseQuantity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return (true, 0);
        return int.TryParse(value, out var quantity) ? (true, quantity) : (false, 0);
    }
}
