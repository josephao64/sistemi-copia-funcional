using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Finance;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Finance;
using SistemiPos.Infrastructure.Persistence;
using SistemiPos.Web.Models.Finance;

namespace SistemiPos.Web.Controllers;

[Authorize]
public sealed class PettyCashController(
    IFinancialService service,
    ICurrentUserAccessor currentUser,
    AppDbContext dbContext) : Controller
{
    public async Task<IActionResult> Index(PettyCashMovementType? movementType, string? category, DateTime? from, DateTime? to,
        string? search, CancellationToken ct)
    {
        ViewBag.CurrentUserName = currentUser.UserName;
        try
        {
            var pettyCash = await service.GetPettyCashAsync(ct);
            var movements = pettyCash.Movements.AsEnumerable();
            if (movementType.HasValue) movements = movements.Where(x => x.Type == movementType.Value);
            if (!string.IsNullOrWhiteSpace(category)) movements = movements.Where(x => string.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase));
            if (from.HasValue) movements = movements.Where(x => x.OccurredAtUtc.ToOffset(TimeSpan.FromHours(-6)).Date >= from.Value.Date);
            if (to.HasValue) movements = movements.Where(x => x.OccurredAtUtc.ToOffset(TimeSpan.FromHours(-6)).Date <= to.Value.Date);
            if (!string.IsNullOrWhiteSpace(search)) movements = movements.Where(x => x.Concept.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (x.Document?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                || (x.Beneficiary?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
            ViewBag.MovementType = movementType; ViewBag.Category = category; ViewBag.From = from; ViewBag.To = to; ViewBag.Search = search;
            var settings = await dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync(ct);
            ViewBag.CurrencySymbol = settings?.CurrencySymbol ?? "Q"; ViewBag.CurrencyDecimals = settings?.CurrencyDecimals ?? 2;
            return View(pettyCash with { Movements = movements.OrderByDescending(x => x.OccurredAtUtc).ToList() });
        }
        catch (DomainRuleException ex) { return View("Unassigned", ex.Message); }
    }

    public async Task<IActionResult> History(CancellationToken ct)
    {
        var cycles = await (from cycle in dbContext.PettyCashCycles.AsNoTracking()
                            join fund in dbContext.PettyCashFunds.AsNoTracking() on cycle.PettyCashFundId equals fund.Id
                            where cycle.Status == PettyCashCycleStatus.Closed
                            orderby cycle.ClosedAtUtc descending
                            select new PettyCashHistoryRow(cycle.Id, fund.Name, fund.ResponsibleUserName, cycle.OpenedAtUtc,
                                cycle.ClosedAtUtc, cycle.InitialBalance, cycle.ExpectedBalance, cycle.CountedBalance, cycle.Difference)).ToListAsync(ct);
        return View(cycles);
    }

    public async Task<IActionResult> HistoryDetails(Guid id, CancellationToken ct)
    {
        var cycle = await (from item in dbContext.PettyCashCycles.AsNoTracking()
                           join fund in dbContext.PettyCashFunds.AsNoTracking() on item.PettyCashFundId equals fund.Id
                           where item.Id == id && item.Status == PettyCashCycleStatus.Closed
                           select new PettyCashHistoryRow(item.Id, fund.Name, fund.ResponsibleUserName, item.OpenedAtUtc,
                               item.ClosedAtUtc, item.InitialBalance, item.ExpectedBalance, item.CountedBalance, item.Difference)).SingleOrDefaultAsync(ct);
        if (cycle is null) return NotFound();
        var movements = await dbContext.PettyCashMovements.AsNoTracking().Where(x => x.CycleId == id).OrderByDescending(x => x.OccurredAtUtc)
            .Select(x => new PettyCashMovementDto(x.Id, x.Type, x.Amount, x.Concept, x.Category, x.Document, x.Beneficiary,
                x.Observation, x.UserName, x.OccurredAtUtc, x.AuthorizedByUserName, x.IsCancelled, x.CancellationReason)).ToListAsync(ct);
        return View(new PettyCashCycleHistoryViewModel(cycle, movements));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Open(decimal initialBalance, CancellationToken ct)
    {
        try { await service.OpenPettyCashAsync(initialBalance, ct); TempData["Success"] = "Caja chica abierta."; }
        catch (DomainRuleException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMovement(PettyCashMovementType type, decimal amount, string concept, Guid? categoryId,
        string? document, string? beneficiary, string? observation, CancellationToken ct)
    {
        try { await service.AddPettyCashMovementAsync(new(type, amount, concept, categoryId, document, beneficiary, observation, null), ct); TempData["Success"] = "Movimiento registrado."; }
        catch (DomainRuleException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(string name, CancellationToken ct)
    {
        try
        {
            _ = await service.GetPettyCashAsync(ct);
            var existing = await dbContext.PettyCashCategories.FirstOrDefaultAsync(x => x.Name == name, ct);
            if (existing is not null) return Json(new { id = existing.Id, text = existing.Name });
            var category = new PettyCashCategory(name); dbContext.PettyCashCategories.Add(category); await dbContext.SaveChangesAsync(ct);
            return Json(new { id = category.Id, text = category.Name });
        }
        catch (DomainRuleException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [Authorize(Roles = SecurityRoleNames.SuperAdministrator)]
    public async Task<IActionResult> Manage(CancellationToken ct)
    {
        ViewBag.Users = await dbContext.Users.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.FullName).ToListAsync(ct);
        return View(await dbContext.PettyCashFunds.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct));
    }

    [Authorize(Roles = SecurityRoleNames.SuperAdministrator), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveFund(Guid? id, string name, Guid responsibleUserId, bool isActive, CancellationToken ct)
    {
        try
        {
            var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == responsibleUserId && x.IsActive, ct) ?? throw new DomainRuleException("El usuario responsable no existe o está inactivo.");
            if (await dbContext.PettyCashFunds.AnyAsync(x => x.ResponsibleUserId == responsibleUserId && x.Id != id, ct)) throw new DomainRuleException("El usuario ya tiene una Caja chica asignada.");
            var fund = id.HasValue ? await dbContext.PettyCashFunds.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new DomainRuleException("Caja chica no encontrada.") : new PettyCashFund(name, user.Id, user.UserName!);
            fund.UpdateAssignment(name, user.Id, user.UserName!, isActive);
            if (!id.HasValue) dbContext.PettyCashFunds.Add(fund);
            await dbContext.SaveChangesAsync(ct); if (IsAjaxRequest()) return Json(new { id = fund.Id, text = fund.Name }); TempData["Success"] = "Caja chica guardada.";
        }
        catch (DomainRuleException ex) { if (IsAjaxRequest()) return BadRequest(new { message = ex.Message }); TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Manage));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Settle(decimal countedCash, string? observation, CancellationToken ct)
    {
        try { await service.SettlePettyCashAsync(new(countedCash, observation), ct); TempData["Success"] = "Liquidación registrada."; }
        catch (DomainRuleException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelMovement(Guid movementId, string reason, CancellationToken ct)
    {
        try { await service.CancelPettyCashMovementAsync(movementId, reason, currentUser.UserName, string.Empty, ct); TempData["Success"] = "Movimiento anulado."; }
        catch (DomainRuleException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    private bool IsAjaxRequest() => string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

}
