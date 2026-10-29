using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using SistemiPos.Application.Common.Exceptions;
using SistemiPos.Application.Customers;
using SistemiPos.Application.Finance;
using SistemiPos.Application.Security;
using SistemiPos.Application.Settings;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Finance;
using SistemiPos.Web.Models.Finance;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SistemiPos.Web.Controllers;
[Authorize(Policy = PermissionCodes.AccountsReceivableView)]
public sealed class AccountsReceivableController(IFinancialService service, ISettingsService settingsService,
    ICustomerService customerService) : Controller
{
    public Task<IActionResult> Index(Guid? customerId, AccountStatus? status, DateTime? from, DateTime? to, string? search, CancellationToken ct) => List(ReceivableSection.General, "General", nameof(Index), customerId, status, from, to, search, ct);
    public Task<IActionResult> Overdue(Guid? customerId, AccountStatus? status, DateTime? from, DateTime? to, string? search, CancellationToken ct) => List(ReceivableSection.Overdue, "Vencidas", nameof(Overdue), customerId, status, from, to, search, ct);
    public Task<IActionResult> Delinquent(Guid? customerId, AccountStatus? status, DateTime? from, DateTime? to, string? search, CancellationToken ct) => List(ReceivableSection.Delinquent, "En mora", nameof(Delinquent), customerId, status, from, to, search, ct);
    public Task<IActionResult> WithoutPayments(Guid? customerId, AccountStatus? status, DateTime? from, DateTime? to, string? search, CancellationToken ct) => List(ReceivableSection.WithoutPayments, "Sin abonos", nameof(WithoutPayments), customerId, status, from, to, search, ct);
    public Task<IActionResult> PartiallyPaid(Guid? customerId, AccountStatus? status, DateTime? from, DateTime? to, string? search, CancellationToken ct) => List(ReceivableSection.PartiallyPaid, "Parcialmente abonadas", nameof(PartiallyPaid), customerId, status, from, to, search, ct);
    public Task<IActionResult> History(Guid? customerId, AccountStatus? status, DateTime? from, DateTime? to, string? search, CancellationToken ct) => List(ReceivableSection.History, "Historial", nameof(History), customerId, status, from, to, search, ct);

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var account = await service.GetReceivableAsync(id, ct);
        if (account is null) return NotFound();
        var settings = await settingsService.GetSystemSettingAsync(ct);
        ViewBag.CurrencySymbol = settings.CurrencySymbol;
        ViewBag.CurrencyDecimals = settings.CurrencyDecimals;
        return View(account);
    }

    [Authorize(Policy = PermissionCodes.AccountsReceivableCollect), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Collect(Guid id, decimal amount, FinancialPaymentMethod paymentMethod, string? reference, string? returnUrl, CancellationToken ct)
    {
        try
        {
            await service.CollectReceivableAsync(id, new(amount, paymentMethod, reference), ct);
            TempData["Success"] = "Abono registrado correctamente.";
        }
        catch (Exception exception) when (exception is DomainRuleException or NotFoundException or DbUpdateConcurrencyException)
        {
            TempData["Error"] = exception is DbUpdateConcurrencyException
                ? "La cuenta o la caja cambió mientras se registraba el abono. No se aplicó el movimiento; inténtalo nuevamente."
                : exception.Message;
        }
        return LocalRedirectOrIndex(returnUrl);
    }
    [Authorize(Policy = PermissionCodes.AccountsReceivableCancel), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, string? returnUrl, CancellationToken ct)
    {
        try
        {
            await service.CancelReceivableAsync(id, ct);
            TempData["Success"] = "Cuenta anulada correctamente.";
        }
        catch (Exception exception) when (exception is DomainRuleException or NotFoundException or DbUpdateConcurrencyException)
        {
            TempData["Error"] = exception is DbUpdateConcurrencyException
                ? "La cuenta cambió mientras se intentaba anular. Actualiza la información e inténtalo nuevamente."
                : exception.Message;
        }
        return LocalRedirectOrIndex(returnUrl);
    }

    private async Task<IActionResult> List(ReceivableSection section, string title, string action, Guid? customerId,
        AccountStatus? status, DateTime? from, DateTime? to, string? search, CancellationToken ct)
    {
        var result = await service.GetReceivablesAsync(new(section, customerId, status, from, to, search), ct);
        var customers = await customerService.GetCustomersAsync(ct);
        var settings = await settingsService.GetSystemSettingAsync(ct);
        return View("Index", new ReceivableListViewModel
        {
            Section = section, Title = title, Action = action, Accounts = result.Accounts, Summary = result.Summary,
            CustomerId = customerId, CustomerName = customers.FirstOrDefault(x => x.Id == customerId)?.Name,
            Status = status, From = from, To = to, Search = search,
            Customers = customers.Where(x => x.IsActive).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList(),
            CurrencySymbol = settings.CurrencySymbol, CurrencyDecimals = settings.CurrencyDecimals
        });
    }

    private IActionResult LocalRedirectOrIndex(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index));
}
