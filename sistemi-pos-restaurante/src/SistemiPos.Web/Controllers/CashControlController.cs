using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Cash;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Infrastructure.Persistence;
using SistemiPos.Application.Settings;

namespace SistemiPos.Web.Controllers;

[Authorize(Policy = PermissionCodes.CashControlAccess)]
public sealed class CashControlController(
    ICashService cashService,
    AppDbContext db,
    ISettingsService settingsService,
    SistemiPos.Application.Abstractions.IOperationContextAccessor operationContext) : Controller
{
    public async Task<IActionResult> Index(DateTime? date, string? user, CancellationToken ct)
    {
        var sessions = await cashService.GetSessionsAsync(ct); var query = sessions.AsEnumerable();
        query = query.Where(x => x.Status == CashSessionStatus.Active);
        var currentBranchId = operationContext.CurrentBranchId;
        if (currentBranchId.HasValue)
        {
            query = query.Where(x => x.BranchId == null || x.BranchId == currentBranchId.Value);
        }
        if (date is not null) query = query.Where(x => x.OpenedAtUtc.ToOffset(TimeSpan.FromHours(-6)).Date == date.Value.Date);
        if (!string.IsNullOrWhiteSpace(user)) query = query.Where(x => x.CashierUserName.Contains(user, StringComparison.OrdinalIgnoreCase));
        ViewBag.Date = date;
        ViewBag.User = user;
        ViewBag.Users = await db.Users.AsNoTracking().Where(x => x.IsActive && x.UserName != null)
            .OrderBy(x => x.UserName).Select(x => x.UserName!).ToListAsync(ct);
        return View(query.OrderByDescending(x => x.OpenedAtUtc).ToList());
    }

    public async Task<IActionResult> History(DateTime? date, string? user, CancellationToken ct)
    {
        var sessions = await cashService.GetSessionsAsync(ct);
        var query = sessions.Where(x => x.Status != CashSessionStatus.Active);
        var currentBranchId = operationContext.CurrentBranchId;
        if (currentBranchId.HasValue)
        {
            query = query.Where(x => x.BranchId == null || x.BranchId == currentBranchId.Value);
        }
        if (date is not null)
            query = query.Where(x => (x.ClosedAtUtc ?? x.OpenedAtUtc).ToOffset(TimeSpan.FromHours(-6)).Date == date.Value.Date);
        if (!string.IsNullOrWhiteSpace(user))
            query = query.Where(x => x.CashierUserName.Contains(user.Trim(), StringComparison.OrdinalIgnoreCase));

        ViewBag.CashHistory = true;
        ViewBag.Date = date;
        ViewBag.User = user;
        ViewBag.Users = await db.Users.AsNoTracking().Where(x => x.IsActive && x.UserName != null)
            .OrderBy(x => x.UserName).Select(x => x.UserName!).ToListAsync(ct);
        return View("Index", query.OrderByDescending(x => x.ClosedAtUtc ?? x.OpenedAtUtc).ToList());
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var session = await cashService.GetSessionAsync(id, ct);
        if (session is null) return NotFound();
        ViewBag.CurrencySymbol = (await settingsService.GetSystemSettingAsync(ct)).CurrencySymbol;
        return View(session);
    }

    [Authorize(Policy = PermissionCodes.CashManage)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reconcile(Guid id, string? observation, CancellationToken ct)
    {
        try
        {
            await cashService.ReconcileSessionAsync(id, observation, ct);
            TempData["Success"] = "La diferencia de caja fue marcada como cuadrada.";
        }
        catch (DomainRuleException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = PermissionCodes.CashMovementsView)]
    public async Task<IActionResult> Movements(CancellationToken ct)
    {
        ViewBag.CurrentSession = await cashService.GetCurrentSessionAsync(ct);
        var items = await db.CashMovements.AsNoTracking().OrderByDescending(x => x.OccurredAtUtc).Take(1000).ToListAsync(ct); return View(items);
    }

    [Authorize(Policy = PermissionCodes.CashAuditView)]
    public async Task<IActionResult> Audit(CancellationToken ct)
    {
        var items = await db.FinancialAuditEntries.AsNoTracking()
            .Where(x => x.CashSessionId != null)
            .OrderByDescending(x => x.OccurredAtUtc).Take(2000).ToListAsync(ct);
        return View(items);
    }

    [Authorize(Policy = PermissionCodes.CashManage)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMovement(Guid cashSessionId, CashMovementType type, decimal amount, string reason,
        string? authorizerUserName, string? authorizerPassword, CancellationToken ct)
    {
        try { await cashService.RecordMovementAsync(cashSessionId, new RecordCashMovementRequest(type, amount, reason, authorizerUserName, authorizerPassword), ct); TempData["Success"] = "Movimiento registrado."; }
        catch (DomainRuleException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Movements));
    }
}
