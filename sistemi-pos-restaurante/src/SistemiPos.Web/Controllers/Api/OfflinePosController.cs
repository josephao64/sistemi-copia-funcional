using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemiPos.Application.Cash;
using SistemiPos.Application.Catalog;
using SistemiPos.Application.Customers;
using SistemiPos.Application.Sales;
using SistemiPos.Application.Security;
using SistemiPos.Application.Settings;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Sales;
using SistemiPos.Web.Models.Offline;

namespace SistemiPos.Web.Controllers.Api;

[ApiController]
[Route("api/offline-pos")]
[Authorize(Policy = RoleAccessPolicies.SalesAccess)]
[Authorize(Policy = PermissionCodes.SalesManage)]
public sealed class OfflinePosController(
    ICatalogService catalogService,
    ICustomerService customerService,
    ICashService cashService,
    ISettingsService settingsService,
    ISaleService saleService,
    Microsoft.AspNetCore.Antiforgery.IAntiforgery? antiforgery = null) : ControllerBase
{
    [HttpGet("bootstrap")]
    public async Task<ActionResult<OfflineBootstrapResponse>> GetBootstrapAsync(CancellationToken cancellationToken, Guid? terminalId = null)
    {
        var products = await catalogService.SearchProductsAsync(null, cancellationToken);
        var customers = await customerService.GetCustomersAsync(cancellationToken);
        var currentSession = await cashService.GetCurrentSessionAsync(cancellationToken);
        if (currentSession is null) return Conflict(new { message = "Abre tu caja antes de preparar el modo offline." });
        var offlineContext = await saleService.CreateOfflineTicketAsync(currentSession.Id, terminalId, cancellationToken);
        var stocks = await saleService.GetOfflineStocksAsync(offlineContext.Ticket, cancellationToken);
        var settings = await settingsService.GetSystemSettingAsync(cancellationToken);
        var bankAccounts = await settingsService.GetBankAccountsAsync(cancellationToken);

        return Ok(new OfflineBootstrapResponse(
            products.Where(x => x.IsActive && x.IsVisible && !x.ManagesVariants && x.ArticleType == SistemiPos.Domain.Catalog.ArticleType.Reventa).Select(x => new OfflineProductDto(
                x.Id,
                x.InternalCode,
                x.Barcode,
                x.DisplayName ?? x.Name,
                x.SalePrice,
                stocks.GetValueOrDefault(x.Id),
                x.MultiPriceEnabled,
                x.MultiPriceMode.ToString(),
                (x.PriceRules ?? []).Select(rule => new OfflineProductPriceRuleDto(rule.Quantity, rule.Price, rule.IsActive)).ToList())).ToList(),
            customers.Where(x => x.IsActive).Select(x => new OfflineCustomerDto(x.Id, x.Name, x.Nit)).ToList(),
            currentSession is null ? [] : [new OfflineCashSessionDto(currentSession.Id, currentSession.RegisterCode, currentSession.CashierUserName)],
            bankAccounts.Where(x => x.IsActive).Select(x => new OfflineBankAccountDto(x.Id, x.Bank, x.AccountName, x.AccountNumber)).ToList(),
            new OfflineSettingDto(
                settings.BusinessName,
                settings.BusinessNit,
                settings.BusinessAddress,
                settings.BusinessPhone,
                settings.CurrencyCode,
                settings.CurrencyName,
                settings.CurrencySymbol,
                settings.CurrencyDecimals,
                settings.DecimalSeparator,
                settings.ThousandsSeparator,
                settings.CurrencySymbolPosition.ToString(),
                settings.TaxPercent,
                settings.ShowTaxOnReceipt,
                settings.ReceiptFooter,
                settings.CashPaymentsEnabled,
                settings.TransferPaymentsEnabled,
                settings.PosPaymentsEnabled,
                DateTimeOffset.UtcNow),
            currentSession!.AssignedPosDevices?.Select(x => new OfflinePosDeviceDto(x.PosDeviceId, x.PosDeviceName)).ToList(),
            offlineContext, antiforgery?.GetAndStoreTokens(HttpContext).RequestToken));
    }

    [HttpPost("sync"), ValidateAntiForgeryToken]
    public async Task<ActionResult<OfflineSyncBatchResponse>> SyncAsync([FromBody] OfflineSyncBatchRequest request, CancellationToken cancellationToken)
    {
        var results = new List<OfflineSyncResultDto>();
        if (request.Sales.Count > 500) return BadRequest(new { message = "El lote supera las 500 operaciones permitidas." });

        foreach (var sale in request.Sales)
        {
            try
            {
                if (sale.Items.Any(x => x.DiscountPercent > 0))
                {
                    results.Add(new OfflineSyncResultDto(
                        sale.ClientOperationId,
                        "Conflict",
                        null,
                        "Las ventas con descuento requieren conexión y aprobación de un superior antes de guardarse."));
                    continue;
                }

                if (sale.PaymentMethod is not (PaymentMethodType.Cash or PaymentMethodType.Transfer or PaymentMethodType.Pos))
                {
                    results.Add(new OfflineSyncResultDto(
                        sale.ClientOperationId,
                        "Conflict",
                        null,
                        "La forma de pago seleccionada requiere conexión con el servidor."));
                    continue;
                }

                var saleId = await saleService.CreateSaleAsync(new CreateSaleRequest(
                    sale.CashSessionId,
                    sale.CustomerId,
                    sale.Items.Select(x => new SaleItemRequest(x.ProductId, x.Quantity, x.DiscountPercent)).ToList(),
                    new SalePaymentRequest(
                        sale.PaymentMethod,
                        sale.ReceivedAmount,
                        sale.TransferSourceBank,
                        sale.TransferBankAccountId,
                        sale.TransferReference,
                        sale.TransferAmount,
                        sale.TransferDateUtc,
                        sale.TransferStatus,
                        sale.CardType,
                        sale.CardLast4,
                        sale.CardAuthorizationNumber,
                        sale.CardVoucherReference,
                        sale.CardAmount,
                        sale.CardStatus,
                        sale.PosDeviceId,
                        sale.TransferSenderName),
                    ClientOperationId: sale.ClientOperationId,
                    LocalCreatedAtUtc: sale.LocalCreatedAtUtc,
                    CustomerNit: sale.CustomerNit,
                    CustomerName: sale.CustomerName,
                    CustomerAddress: sale.CustomerAddress,
                    CustomerPhone: sale.CustomerPhone,
                    CustomerVisitOrigin: sale.CustomerVisitOrigin, OfflineTicket: sale.OfflineTicket,
                    ExpectedTotal: sale.ExpectedTotal), cancellationToken);

                var syncedSale = await saleService.GetSaleAsync(saleId, cancellationToken);
                results.Add(new OfflineSyncResultDto(sale.ClientOperationId, "Synced", syncedSale?.Number, null));
            }
            catch (DomainRuleException ex)
            {
                results.Add(new OfflineSyncResultDto(sale.ClientOperationId, "Conflict", null, ex.Message));
            }
            catch (Exception)
            {
                results.Add(new OfflineSyncResultDto(sale.ClientOperationId, "Error", null, "No fue posible registrar la operación. Se conserva pendiente para reintentar."));
            }
        }

        return Ok(new OfflineSyncBatchResponse(results));
    }
}
