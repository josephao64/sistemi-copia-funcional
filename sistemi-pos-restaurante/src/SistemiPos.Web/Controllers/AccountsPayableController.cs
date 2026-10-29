using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemiPos.Application.Finance;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Finance;

namespace SistemiPos.Web.Controllers;
[Authorize(Policy = PermissionCodes.AccountsPayableView)]
public sealed class AccountsPayableController(IFinancialService service) : Controller
{
    public async Task<IActionResult> Index(Guid? supplierId, AccountStatus? status, DateTime? dueFrom, DateTime? dueTo, CancellationToken ct)
    {
        var all = await service.GetPayablesAsync(ct);
        IEnumerable<PayableAccountDto> values = all;
        if (supplierId.HasValue) values = values.Where(x => x.SupplierId == supplierId);
        if (status.HasValue) values = values.Where(x => x.Status == status);
        if (dueFrom.HasValue) values = values.Where(x => x.DueDateUtc.Date >= dueFrom.Value.Date);
        if (dueTo.HasValue) values = values.Where(x => x.DueDateUtc.Date <= dueTo.Value.Date);
        ViewBag.SupplierId = supplierId; ViewBag.Status = status; ViewBag.DueFrom = dueFrom; ViewBag.DueTo = dueTo;
        ViewBag.Suppliers = all
            .GroupBy(x => new { x.SupplierId, x.SupplierName })
            .Select(x => new SelectListItem(x.Key.SupplierName, x.Key.SupplierId.ToString()))
            .OrderBy(x => x.Text)
            .ToList();
        return View(values.ToList());
    }
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var account = await service.GetPayableAsync(id, ct);
        return account is null ? NotFound() : View(account);
    }
    [Authorize(Policy = PermissionCodes.AccountsPayablePay), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(Guid id, decimal amount, FinancialPaymentMethod paymentMethod, MoneySourceType moneySource,
        Guid? sourceAccountId, string? reference, string? authorizerUserName, string? authorizerPassword, CancellationToken ct)
    {
        try { await service.PayPayableAsync(id, new(amount, paymentMethod, moneySource, sourceAccountId, reference, authorizerUserName, authorizerPassword), ct); TempData["Success"] = "Pago registrado y origen afectado automáticamente."; }
        catch (DomainRuleException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id });
    }
    [Authorize(Policy = PermissionCodes.AccountsPayableCancel), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        try { await service.CancelPayableAsync(id, ct); } catch (DomainRuleException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id });
    }
}
