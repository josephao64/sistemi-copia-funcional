using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemiPos.Application.Security;
using SistemiPos.Application.Settings;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Infrastructure.Security;
using SistemiPos.Web.Models.Settings;
using SistemiPos.Infrastructure.Persistence;
using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace SistemiPos.Web.Controllers;

[Authorize(Roles = SecurityRoleNames.SuperAdministrator)]
[Authorize(Policy = PermissionCodes.SettingsView)]
public sealed class SettingsController(
    ISettingsService settingsService,
    UserManager<AppUser> userManager,
    RoleManager<AppRole> roleManager,
    AppDbContext dbContext,
    IDataProtectionProvider dataProtectionProvider) : Controller
{
    private readonly IDataProtector infileCredentialProtector = dataProtectionProvider
        .CreateProtector("SistemiPos.Settings.InfileCredentials.v1");

    [HttpGet]
    public async Task<IActionResult> CashConfiguration(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetSystemSettingAsync(cancellationToken);
        var totals = await (from payment in dbContext.SalePayments.AsNoTracking()
                            join sale in dbContext.Sales.AsNoTracking() on payment.SaleId equals sale.Id
                            where payment.PosDeviceId != null && sale.Status != SaleStatus.Cancelled
                            group payment by payment.PosDeviceId!.Value into values
                            select new { PosDeviceId = values.Key, Total = values.Sum(x => x.CardAmount ?? x.TotalAmount) })
            .ToDictionaryAsync(x => x.PosDeviceId, x => x.Total, cancellationToken);
        return View(new CashConfigurationViewModel
        {
            Denominations = await dbContext.CashDenominations.AsNoTracking()
            .Where(x => !(x.Kind == DenominationKind.Banknote && x.Value == 1m && !x.IsActive))
            .OrderBy(x => x.Kind).ThenByDescending(x => x.Value).ToListAsync(cancellationToken),
            PosDevices = await dbContext.PosDevices.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken),
            BankAccounts = await dbContext.BankAccounts.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Bank).ThenBy(x => x.AccountName).ToListAsync(cancellationToken),
            PosDeviceTotals = totals,
            CurrencySymbol = settings.CurrencySymbol,
            PaymentMethods = await dbContext.PaymentMethodDefinitions.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken)
        });
    }

    [Authorize(Policy = PermissionCodes.SettingsManage)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDenomination(DenominationKind kind, decimal value, CancellationToken cancellationToken)
    {
        try { var item = new CashDenomination(kind, value); dbContext.CashDenominations.Add(item); await dbContext.SaveChangesAsync(cancellationToken); if (IsAjaxRequest()) return Json(new { id = item.Id, text = item.Value.ToString("N2") }); TempData["Success"] = "Denominación agregada."; }
        catch (Exception ex) when (ex is DomainRuleException or DbUpdateException) { var message = ex is DomainRuleException ? ex.Message : "La denominación ya existe."; if (IsAjaxRequest()) return BadRequest(new { message }); TempData["Error"] = message; }
        return RedirectToAction(nameof(CashConfiguration));
    }

    [Authorize(Policy = PermissionCodes.SettingsManage)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleDenomination(Guid id, CancellationToken cancellationToken)
    {
        var item = await dbContext.CashDenominations.FindAsync([id], cancellationToken); if (item is null) return NotFound();
        item.Update(item.Kind, item.Value, !item.IsActive); await dbContext.SaveChangesAsync(cancellationToken); return RedirectToAction(nameof(CashConfiguration));
    }

    [Authorize(Policy = PermissionCodes.SettingsManage)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPosDevice(PosDeviceFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || model.BankAccountId is null)
        {
            var message = "Complete los datos requeridos del dispositivo POS.";
            if (IsAjaxRequest()) return BadRequest(new { message });
            TempData["Error"] = message;
            return RedirectToAction(nameof(CashConfiguration));
        }
        if (!await dbContext.BankAccounts.AnyAsync(x => x.Id == model.BankAccountId && x.IsActive, cancellationToken))
        {
            if (IsAjaxRequest()) return BadRequest(new { message = "La cuenta bancaria seleccionada no está disponible." });
            TempData["Error"] = "La cuenta bancaria seleccionada no está disponible.";
            return RedirectToAction(nameof(CashConfiguration));
        }
        try { var item = new PosDevice(model.Name, model.Provider, model.TerminalId, model.BankAccountId.Value, model.Location, model.Observation, model.IsActive); dbContext.PosDevices.Add(item); await dbContext.SaveChangesAsync(cancellationToken); if (IsAjaxRequest()) return Json(new { id = item.Id, text = item.Name }); TempData["Success"] = "Dispositivo POS agregado."; }
        catch (Exception ex) when (ex is DomainRuleException or DbUpdateException) { var message = ex is DomainRuleException ? ex.Message : "El dispositivo POS ya existe."; if (IsAjaxRequest()) return BadRequest(new { message }); TempData["Error"] = message; }
        return RedirectToAction(nameof(CashConfiguration));
    }

    [Authorize(Policy = PermissionCodes.SettingsManage)]
    [HttpGet]
    public async Task<IActionResult> EditPosDevice(Guid id, CancellationToken cancellationToken)
    {
        var item = await dbContext.PosDevices.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null) return NotFound();
        return View("PosDeviceForm", await PopulatePosDeviceAccountsAsync(new PosDeviceFormViewModel
        {
            Id = item.Id, Name = item.Name, Provider = item.Provider ?? string.Empty, TerminalId = item.TerminalId ?? string.Empty,
            BankAccountId = item.BankAccountId, Location = item.Location ?? string.Empty, Observation = item.Observation, IsActive = item.IsActive
        }, cancellationToken));
    }

    [Authorize(Policy = PermissionCodes.SettingsManage)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPosDevice(PosDeviceFormViewModel model, CancellationToken cancellationToken)
    {
        if (model.Id is null) return NotFound();
        if (!ModelState.IsValid || model.BankAccountId is null)
            return View("PosDeviceForm", await PopulatePosDeviceAccountsAsync(model, cancellationToken));
        if (!await dbContext.BankAccounts.AnyAsync(x => x.Id == model.BankAccountId, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.BankAccountId), "La cuenta bancaria seleccionada no existe.");
            return View("PosDeviceForm", await PopulatePosDeviceAccountsAsync(model, cancellationToken));
        }
        var item = await dbContext.PosDevices.FindAsync([model.Id.Value], cancellationToken);
        if (item is null) return NotFound();
        try
        {
            item.UpdateDetails(model.Name, model.Provider, model.TerminalId, model.BankAccountId.Value, model.Location, model.Observation, model.IsActive);
            await dbContext.SaveChangesAsync(cancellationToken);
            TempData["Success"] = "El dispositivo POS fue actualizado.";
            return RedirectToAction(nameof(CashConfiguration));
        }
        catch (Exception ex) when (ex is DomainRuleException or DbUpdateException)
        {
            ModelState.AddModelError(string.Empty, ex is DomainRuleException ? ex.Message : "No fue posible actualizar el dispositivo POS.");
            return View("PosDeviceForm", await PopulatePosDeviceAccountsAsync(model, cancellationToken));
        }
    }

    [Authorize(Policy = PermissionCodes.SettingsManage)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePosDevice(Guid id, CancellationToken cancellationToken)
    {
        var item = await dbContext.PosDevices.FindAsync([id], cancellationToken); if (item is null) return NotFound();
        item.Update(item.Name, !item.IsActive); await dbContext.SaveChangesAsync(cancellationToken); return RedirectToAction(nameof(CashConfiguration));
    }

    private async Task<PosDeviceFormViewModel> PopulatePosDeviceAccountsAsync(PosDeviceFormViewModel model, CancellationToken cancellationToken)
    {
        model.BankAccounts = await dbContext.BankAccounts.AsNoTracking().OrderBy(x => x.Bank).ThenBy(x => x.AccountName)
            .Select(x => new SelectListItem($"{x.Bank} · {x.AccountName} · {x.AccountNumber}", x.Id.ToString(), x.Id == model.BankAccountId))
            .ToListAsync(cancellationToken);
        return model;
    }

    [Authorize(Policy = PermissionCodes.SettingsManage)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePaymentMethod(Guid id, CancellationToken cancellationToken)
    {
        var item = await dbContext.PaymentMethodDefinitions.FindAsync([id], cancellationToken); if (item is null) return NotFound();
        if (item.Behavior == PaymentFinancialBehavior.Cash)
        {
            if (!item.IsActive)
            {
                item.Update(item.Name, item.Behavior, true);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            TempData["Info"] = "El pago en efectivo permanece siempre habilitado.";
            return RedirectToAction(nameof(CashConfiguration));
        }
        item.Update(item.Name, item.Behavior, !item.IsActive); await dbContext.SaveChangesAsync(cancellationToken); return RedirectToAction(nameof(CashConfiguration));
    }
    public async Task<IActionResult> Index(string? tab, CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetSystemSettingAsync(cancellationToken);
        var currencies = await settingsService.GetCurrenciesAsync(cancellationToken);
        ViewBag.Currencies = currencies;
        ViewBag.ActiveSettingsTab = NormalizeSettingsTab(tab);
        return View(new SystemSettingFormViewModel
        {
            CurrencyId = settings.CurrencyId ?? currencies.FirstOrDefault(x => x.Code == settings.CurrencyCode)?.Id,
            BusinessName = settings.BusinessName,
            BusinessLegalName = settings.BusinessLegalName,
            BusinessNit = settings.BusinessNit,
            BusinessAddress = settings.BusinessAddress,
            BusinessPhone = settings.BusinessPhone,
            BusinessEmail = settings.BusinessEmail,
            BusinessLogoUrl = settings.BusinessLogoUrl,
            CurrencyCode = settings.CurrencyCode,
            CurrencyName = settings.CurrencyName,
            CurrencySymbol = settings.CurrencySymbol,
            CurrencyDecimals = settings.CurrencyDecimals,
            DecimalSeparator = settings.DecimalSeparator,
            ThousandsSeparator = settings.ThousandsSeparator,
            CurrencySymbolPosition = settings.CurrencySymbolPosition,
            TaxPercent = settings.TaxPercent,
            ShowTaxOnReceipt = settings.ShowTaxOnReceipt,
            ReceiptFooter = settings.ReceiptFooter,
            CashPaymentsEnabled = settings.CashPaymentsEnabled,
            TransferPaymentsEnabled = settings.TransferPaymentsEnabled,
            PosPaymentsEnabled = settings.PosPaymentsEnabled,
            CreditPaymentsEnabled = settings.CreditPaymentsEnabled,
            DiscountsEnabled = settings.DiscountsEnabled,
            DefaultMaxDiscountPercent = settings.DefaultMaxDiscountPercent,
            NitLookupProvider = settings.NitLookupProvider,
            ProductsWithoutMovementDays = settings.ProductsWithoutMovementDays,
            EstablishmentName = settings.EstablishmentName,
            EstablishmentCode = settings.EstablishmentCode,
            FelEnabled = settings.FelEnabled,
            FelEnvironment = settings.FelEnvironment,
            DefaultFelDocumentType = settings.DefaultFelDocumentType,
            FelTaxPhrases = settings.FelTaxPhrases,
            HasInfileApiUser = !string.IsNullOrWhiteSpace(settings.InfileApiUserProtected),
            HasInfileApiKey = !string.IsNullOrWhiteSpace(settings.InfileApiKeyProtected),
            HasInfileSignatureUser = !string.IsNullOrWhiteSpace(settings.InfileSignatureUserProtected),
            HasInfileSignatureKey = !string.IsNullOrWhiteSpace(settings.InfileSignatureKeyProtected)
        });
    }

    [Authorize(Policy = PermissionCodes.SettingsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SystemSettingFormViewModel model, string? activeTab, CancellationToken cancellationToken)
    {
        var selectedTab = NormalizeSettingsTab(activeTab);
        var current = await settingsService.GetSystemSettingAsync(cancellationToken);
        if (!ModelState.IsValid)
        {
            PrepareCredentialFieldsForRedisplay(model, current);
            ViewBag.Currencies = await settingsService.GetCurrenciesAsync(cancellationToken);
            ViewBag.ActiveSettingsTab = selectedTab;
            return View(model);
        }

        try
        {
            string ProtectedOrExisting(string? value, string? existing) =>
                string.IsNullOrWhiteSpace(value) ? existing ?? string.Empty : infileCredentialProtector.Protect(value.Trim());

            var configurablePaymentMethods = await dbContext.PaymentMethodDefinitions
                .Where(x => x.Behavior == PaymentFinancialBehavior.Transfer || x.Behavior == PaymentFinancialBehavior.Pos)
                .ToListAsync(cancellationToken);
            foreach (var paymentMethod in configurablePaymentMethods)
            {
                var enabled = paymentMethod.Behavior == PaymentFinancialBehavior.Transfer
                    ? model.TransferPaymentsEnabled
                    : model.PosPaymentsEnabled;
                if (paymentMethod.IsActive != enabled)
                    paymentMethod.Update(paymentMethod.Name, paymentMethod.Behavior, enabled);
            }

            await settingsService.UpdateSystemSettingAsync(new UpdateSystemSettingRequest(
                model.BusinessName,
                model.BusinessNit,
                model.BusinessAddress,
                model.BusinessPhone,
                model.BusinessLogoUrl,
                model.CurrencyCode,
                model.CurrencyName,
                model.CurrencySymbol,
                model.CurrencyDecimals,
                model.DecimalSeparator,
                model.ThousandsSeparator,
                model.CurrencySymbolPosition,
                model.TaxPercent,
                model.ShowTaxOnReceipt,
                model.ReceiptFooter,
                true,
                model.TransferPaymentsEnabled,
                model.PosPaymentsEnabled,
                model.DiscountsEnabled,
                model.DefaultMaxDiscountPercent,
                model.NitLookupProvider,
                model.ProductsWithoutMovementDays,
                model.CurrencyId,
                model.BusinessEmail,
                model.BusinessLegalName,
                model.EstablishmentName,
                model.EstablishmentCode,
                model.FelEnabled,
                model.FelEnvironment,
                model.DefaultFelDocumentType,
                model.FelTaxPhrases,
                ProtectedOrExisting(model.InfileApiUser, current.InfileApiUserProtected),
                ProtectedOrExisting(model.InfileApiKey, current.InfileApiKeyProtected),
                ProtectedOrExisting(model.InfileSignatureUser, current.InfileSignatureUserProtected),
                ProtectedOrExisting(model.InfileSignatureKey, current.InfileSignatureKeyProtected),
                CreditPaymentsEnabled: model.CreditPaymentsEnabled), cancellationToken);
            TempData["Success"] = "La configuración fue actualizada.";
            return RedirectToAction(nameof(Index), new { tab = selectedTab });
        }
        catch (DomainRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            PrepareCredentialFieldsForRedisplay(model, current);
            ViewBag.Currencies = await settingsService.GetCurrenciesAsync(cancellationToken);
            ViewBag.ActiveSettingsTab = selectedTab;
            return View(model);
        }
    }

    private static string NormalizeSettingsTab(string? tab) =>
        tab is "company" or "billing" or "operation" or "cash" or "sales" ? tab : "company";

    [Authorize(Policy = PermissionCodes.SettingsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePaymentOptions(bool transferPaymentsEnabled, bool posPaymentsEnabled,
        bool creditPaymentsEnabled,
        CancellationToken cancellationToken)
    {
        var setting = await dbContext.SystemSettings.FirstOrDefaultAsync(cancellationToken);
        if (setting is null)
            return NotFound(new { message = "No se encontró la configuración del sistema." });

        setting.ConfigurePaymentMethods(transferPaymentsEnabled, posPaymentsEnabled, creditPaymentsEnabled);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Json(new { message = "Las formas de pago fueron actualizadas." });
    }

    private void PrepareCredentialFieldsForRedisplay(SystemSettingFormViewModel model, SystemSettingDto settings)
    {
        model.InfileApiUser = null;
        model.InfileApiKey = null;
        model.InfileSignatureUser = null;
        model.InfileSignatureKey = null;
        model.HasInfileApiUser = !string.IsNullOrWhiteSpace(settings.InfileApiUserProtected);
        model.HasInfileApiKey = !string.IsNullOrWhiteSpace(settings.InfileApiKeyProtected);
        model.HasInfileSignatureUser = !string.IsNullOrWhiteSpace(settings.InfileSignatureUserProtected);
        model.HasInfileSignatureKey = !string.IsNullOrWhiteSpace(settings.InfileSignatureKeyProtected);
        ModelState.Remove(nameof(model.InfileApiUser));
        ModelState.Remove(nameof(model.InfileApiKey));
        ModelState.Remove(nameof(model.InfileSignatureUser));
        ModelState.Remove(nameof(model.InfileSignatureKey));
    }

    [Authorize(Policy = PermissionCodes.SettingsManage), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCurrency(string code, string name, string symbol, int decimals, string decimalSeparator, string thousandsSeparator, CurrencySymbolPosition symbolPosition, CancellationToken cancellationToken)
    {
        try
        {
            var id = await settingsService.CreateCurrencyAsync(code, name, symbol, decimals, decimalSeparator, thousandsSeparator, symbolPosition, cancellationToken);
            return Json(new { id, text = $"{code.Trim().ToUpperInvariant()} · {name.Trim()} ({symbol.Trim()})", code = code.Trim().ToUpperInvariant(), name = name.Trim(), symbol = symbol.Trim(), decimals, decimalSeparator, thousandsSeparator, symbolPosition = symbolPosition.ToString() });
        }
        catch (Exception ex) when (ex is DomainRuleException or DbUpdateException) { return BadRequest(new { message = ex is DomainRuleException ? ex.Message : "Ya existe una moneda con ese código." }); }
    }

    public async Task<IActionResult> BankAccounts(CancellationToken cancellationToken)
    {
        var accounts = await settingsService.GetBankAccountsAsync(cancellationToken);
        return View(accounts);
    }

    [Authorize(Policy = PermissionCodes.SettingsManage)]
    [HttpGet]
    public IActionResult CreateBankAccount() => View("BankAccountForm", new BankAccountFormViewModel());

    [Authorize(Policy = PermissionCodes.SettingsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBankAccount(BankAccountFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("BankAccountForm", model);
        }

        await settingsService.CreateBankAccountAsync(new SaveBankAccountRequest(model.Bank, model.AccountName, model.AccountNumber, model.IsActive), cancellationToken);
        return RedirectToAction(nameof(BankAccounts));
    }

    [Authorize(Policy = PermissionCodes.SettingsManage)]
    [HttpGet]
    public async Task<IActionResult> EditBankAccount(Guid id, CancellationToken cancellationToken)
    {
        var account = await settingsService.GetBankAccountAsync(id, cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        return View("BankAccountForm", new BankAccountFormViewModel
        {
            Id = account.Id,
            Bank = account.Bank,
            AccountName = account.AccountName,
            AccountNumber = account.AccountNumber,
            IsActive = account.IsActive
        });
    }

    [Authorize(Policy = PermissionCodes.SettingsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditBankAccount(BankAccountFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || model.Id is null)
        {
            return View("BankAccountForm", model);
        }

        await settingsService.UpdateBankAccountAsync(model.Id.Value, new SaveBankAccountRequest(model.Bank, model.AccountName, model.AccountNumber, model.IsActive), cancellationToken);
        return RedirectToAction(nameof(BankAccounts));
    }

    public async Task<IActionResult> Discounts(CancellationToken cancellationToken)
    {
        return View(await BuildDiscountSettingsViewModelAsync(cancellationToken));
    }

    [Authorize(Policy = PermissionCodes.SettingsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRoleDiscount(DiscountSettingsViewModel model, CancellationToken cancellationToken)
    {
        await settingsService.SaveRoleDiscountSettingAsync(new SaveRoleDiscountSettingRequest(model.SelectedRoleId, model.RoleMaxDiscountPercent), cancellationToken);
        return RedirectToAction(nameof(Discounts));
    }

    [Authorize(Policy = PermissionCodes.SettingsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveUserDiscount(DiscountSettingsViewModel model, CancellationToken cancellationToken)
    {
        await settingsService.SaveUserDiscountSettingAsync(new SaveUserDiscountSettingRequest(model.SelectedUserId, model.UserMaxDiscountPercent), cancellationToken);
        return RedirectToAction(nameof(Discounts));
    }

    private async Task<DiscountSettingsViewModel> BuildDiscountSettingsViewModelAsync(CancellationToken cancellationToken)
    {
        var roles = roleManager.Roles.OrderBy(x => x.Name).ToList();
        var users = userManager.Users.OrderBy(x => x.UserName).ToList();
        var roleSettings = await settingsService.GetRoleDiscountSettingsAsync(cancellationToken);
        var userSettings = await settingsService.GetUserDiscountSettingsAsync(cancellationToken);

        return new DiscountSettingsViewModel
        {
            Roles = roles.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList(),
            Users = users.Select(x => new SelectListItem(x.UserName, x.Id.ToString())).ToList(),
            RoleSettings = roleSettings.Select(x =>
            {
                var roleName = roles.FirstOrDefault(r => r.Id == x.RoleId)?.Name ?? x.RoleId.ToString();
                return (roleName, x.MaxDiscountPercent);
            }).ToList(),
            UserSettings = userSettings.Select(x =>
            {
                var userName = users.FirstOrDefault(u => u.Id == x.UserId)?.UserName ?? x.UserId.ToString();
                return (userName, x.MaxDiscountPercent);
            }).ToList()
        };
    }

    private bool IsAjaxRequest() => string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
}
