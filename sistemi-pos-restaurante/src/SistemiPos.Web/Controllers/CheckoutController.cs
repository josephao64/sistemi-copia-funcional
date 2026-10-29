using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemiPos.Application.Cash;
using SistemiPos.Application.Common.Exceptions;
using SistemiPos.Application.Orders;
using SistemiPos.Application.Sales;
using SistemiPos.Application.Security;
using SistemiPos.Application.Settings;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Web.Models.Sales;

namespace SistemiPos.Web.Controllers;

[Authorize(Policy = PermissionCodes.SalesManage), Route("pos/cobro")]
public sealed class CheckoutController(ISaleService sales, IOrderService orders, ICashService cash, ISettingsService settings) : Controller
{
    [HttpGet("pedido/{id:guid}")]
    public async Task<IActionResult> Order(Guid id, CancellationToken ct)
    {
        try
        {
            var order = await orders.GetOrderAsync(id, ct);
            var model = new CheckoutViewModel { OrderId = id, Items = order.Lines.Where(x => x.Sellable > 0)
                .Select(x => new CheckoutItemViewModel { ProductId = x.ProductId, OrderLineId = x.Id, Quantity = x.Sellable }).ToList() };
            return View("Form", await PopulateAsync(model, ct));
        }
        catch (NotFoundException) { return NotFound(); }
        catch (DomainRuleException ex) { TempData["OrderError"] = ex.Message; return RedirectToAction("Details", "Orders", new { id }); }
    }

    [HttpPost("preparar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Prepare(SaleFormViewModel source, CancellationToken ct)
    {
        var model = new CheckoutViewModel { CashSessionId = source.CashSessionId, TerminalId = source.TerminalId, CustomerId = source.CustomerId, CustomerNit = source.CustomerNit,
            CustomerName = source.CustomerName, CustomerAddress = source.CustomerAddress, ApprovalId = source.DiscountApprovalRequestId,
            DiscountReason = source.DiscountReason, Items = source.Items.Where(x => x.ProductId != Guid.Empty && x.Quantity > 0)
                .Select(x => new CheckoutItemViewModel { ProductId = x.ProductId, Quantity = x.Quantity, DiscountType = x.DiscountType, DiscountValue = x.DiscountValue }).ToList() };
        return View("Form", await PopulateAsync(model, ct));
    }

    [HttpPost("calcular"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Calculate(CheckoutViewModel model, CancellationToken ct)
    {
        await PopulateAsync(model, ct);
        if (model.Quote?.Shares is { } shares && model.Payments.Count == 1
            && model.Payments[0].Method == SistemiPos.Domain.Sales.PaymentMethodType.Cash && model.Payments[0].Received is null)
        {
            model.Payments = shares.Select((amount, index) => new CheckoutPaymentViewModel { Amount = amount, PartNumber = index + 1 }).ToList();
            foreach (var key in ModelState.Keys.Where(x => x.StartsWith("Payments[", StringComparison.Ordinal)).ToList()) ModelState.Remove(key);
        }
        return View("Form", model);
    }

    [HttpPost("confirmar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(CheckoutViewModel model, CancellationToken ct)
    {
        try
        {
            if (!ModelState.IsValid) return View("Form", await PopulateAsync(model, ct));
            var session = model.CashSessionId == Guid.Empty ? null : await cash.GetSessionAsync(model.CashSessionId, ct);
            if (session is null) throw new DomainRuleException("Abre tu caja antes de cobrar.");
            var items = model.Items.Where(x => x.Quantity > 0).Select(x => x.ToRequest()).ToList();
            if (items.Any(x => (x.DiscountValue ?? x.DiscountPercent) > 0) && model.ApprovalId is null)
            {
                model.ApprovalId = model.OrderId.HasValue
                    ? await sales.RequestOrderDiscountApprovalAsync(session.Id, model.OrderId.Value, items, ct)
                    : await sales.RequestDiscountApprovalAsync(session.Id, items, ct);
                ModelState.Remove(nameof(model.ApprovalId));
                ModelState.AddModelError(string.Empty, "Descuento enviado a aprobación. Confirma de nuevo cuando el responsable lo apruebe.");
                return View("Form", await PopulateAsync(model, ct));
            }
            if (model.Payments.Count is 0 or > 10) throw new DomainRuleException("Registra entre uno y diez pagos.");
            if (model.SplitMode is not ("None" or "People" or "Amounts")) throw new DomainRuleException("La división seleccionada no es válida.");
            var payments = model.Payments.Select(x => x.ToRequest(model.Payments[0].Amount, model.Payments.Count == 1)).ToList();
            var id = await sales.CreateSaleAsync(new CreateSaleRequest(session.Id, model.CustomerId, items, payments[0],
                DiscountReason: model.DiscountReason, CustomerNit: model.CustomerNit, CustomerName: model.CustomerName,
                CustomerAddress: model.CustomerAddress, DiscountApprovalRequestId: model.ApprovalId,
                ClientOperationId: model.OperationId, Payments: payments, RestaurantOrderId: model.OrderId,
                TerminalId: model.TerminalId,
                SplitPeople: model.SplitMode == "People" ? model.People : null,
                SplitAmounts: model.SplitMode == "Amounts" ? model.Amounts : null, ExpectedTotal: model.ExpectedTotal), ct);
            return RedirectToAction("Print", "Sales", new { id, autoPrint = true });
        }
        catch (DomainRuleException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
        catch (NotFoundException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
        return View("Form", await PopulateAsync(model, ct));
    }

    private async Task<CheckoutViewModel> PopulateAsync(CheckoutViewModel model, CancellationToken ct)
    {
        model.Quote = null;
        try { model.Quote = await sales.QuoteAsync(model.Items.Where(x => x.Quantity > 0).Select(x => x.ToRequest()).ToList(), model.OrderId, ct, model.SplitMode == "People" ? model.People : null); }
        catch (DomainRuleException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
        catch (NotFoundException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
        var config = await settings.GetSystemSettingAsync(ct);
        model.SymbolPosition = config.CurrencySymbolPosition.ToString(); model.DecimalSeparator = config.DecimalSeparator; model.ThousandsSeparator = config.ThousandsSeparator;
        model.ExpectedTotal = model.Quote?.Total;
        ModelState.Remove(nameof(model.ExpectedTotal));
        if (model.Payments.Count == 1 && model.Payments[0].Amount == 0 && model.Quote is not null)
            model.Payments[0].Amount = model.Quote.Total;
        model.CurrencySymbol = config.CurrencySymbol; model.CurrencyDecimals = config.CurrencyDecimals;
        model.Banks = (await settings.GetBankAccountsAsync(ct)).Where(x => x.IsActive).Select(x => new SelectListItem($"{x.Bank} · {x.AccountName}", x.Id.ToString())).ToList();
        var session = await cash.GetCurrentSessionAsync(ct);
        if (model.CashSessionId == Guid.Empty && session is not null) model.CashSessionId = session.Id;
        model.OtherMethods = (await sales.GetOtherPaymentMethodsAsync(ct)).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList();
        model.Terminals = (await sales.GetPosTerminalsAsync(ct)).Select(x => new SelectListItem(x.Code, x.Id.ToString())).ToList();
        if (!model.TerminalId.HasValue && model.Terminals.Count == 1) model.TerminalId = Guid.Parse(model.Terminals[0].Value);
        model.Devices = session?.AssignedPosDevices?.Select(x => new SelectListItem(x.PosDeviceName, x.PosDeviceId.ToString())).ToList() ?? [];
        return model;
    }
}
