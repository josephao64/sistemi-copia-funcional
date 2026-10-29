using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemiPos.Application.Cash;
using SistemiPos.Application.Catalog;
using SistemiPos.Application.Common.Exceptions;
using SistemiPos.Application.Customers;
using SistemiPos.Application.Sales;
using SistemiPos.Application.Security;
using SistemiPos.Application.Settings;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Web.Models.Sales;
using SistemiPos.Infrastructure.Persistence;
using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Sales;
using SistemiPos.Domain.Customers;
using SistemiPos.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace SistemiPos.Web.Controllers;

[Authorize(Policy = RoleAccessPolicies.SalesAccess)]
[Authorize(Policy = PermissionCodes.SalesView)]
public sealed class SalesController(
    ISaleService saleService,
    ISaleReceiptService receiptService,
    ICatalogService catalogService,
    SistemiPos.Application.Recipes.IRecipeOperationsService recipeOperations,
    ICustomerService customerService,
    ICashService cashService,
    ISettingsService settingsService,
    INitLookupService nitLookupService,
    AppDbContext dbContext,
    ISalePdfQueue pdfQueue) : Controller
{

    public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken)
    {
        var sales = await saleService.GetSalesAsync(cancellationToken);
        var term = search?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            var normalizedTerm = NitNormalizer.Normalize(term);
            sales = sales
                .Where(x =>
                    x.Number.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    x.CustomerName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(x.CustomerNit) && x.CustomerNit.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (normalizedTerm is not null && NitNormalizer.Normalize(x.CustomerNit)?.Contains(normalizedTerm, StringComparison.OrdinalIgnoreCase) == true) ||
                    x.SoldAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    x.SoldAtUtc.ToLocalTime().ToString("yyyy-MM-dd").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    x.Items.Any(item => item.ProductName.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        ViewBag.SalesSearch = term;
        return View(sales);
    }

    [Authorize(Roles = SecurityRoleNames.PointOfSaleAccess)]
    [Authorize(Policy = PermissionCodes.SalesManage)]
    [HttpGet("/pos", Name = "pos")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var currentSession = await cashService.GetCurrentSessionAsync(cancellationToken);
        return View("Form", await BuildFormAsync(new SaleFormViewModel { CashSessionId = currentSession?.Id ?? Guid.Empty }, cancellationToken));
    }

    [Authorize(Roles = SecurityRoleNames.PointOfSaleAccess)]
    [Authorize(Policy = PermissionCodes.SalesManage)]
    [HttpGet("/Sales/Create")]
    public IActionResult RedirectLegacyCreate() => RedirectToRoutePermanent("pos");

    [Authorize(Roles = SecurityRoleNames.PointOfSaleAccess)]
    [Authorize(Policy = PermissionCodes.SalesManage)]
    [HttpPost("/pos")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SaleFormViewModel model, CancellationToken cancellationToken)
    {
        if (model.CashSessionId == Guid.Empty)
        {
            if (IsAjaxRequest()) return BadRequest(new { message = "No existe una caja abierta para registrar la venta." });
            return RedirectToRoute("pos");
        }
        model.Items = model.Items.Where(x => x.ProductId != Guid.Empty && x.Quantity > 0).ToList();
        if (model.Items.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "La venta debe incluir al menos una línea.");
        }

        var saleItems = MapSaleItems(model);

        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest()) return BadRequest(new { message = FirstModelError() });
            return View("Form", await BuildFormAsync(model, cancellationToken));
        }

        try
        {
            if (saleItems.Any(x => (x.DiscountValue ?? x.DiscountPercent) > 0) && model.DiscountApprovalRequestId is null)
            {
                var approvalId = await saleService.RequestDiscountApprovalAsync(model.CashSessionId, saleItems, cancellationToken);
                if (IsAjaxRequest()) return StatusCode(StatusCodes.Status202Accepted, new
                {
                    approvalRequired = true,
                    approvalId,
                    message = "La solicitud de descuento fue enviada para aprobación. La ventana permanecerá abierta."
                });
                model.DiscountApprovalRequestId = approvalId;
                ModelState.Remove(nameof(SaleFormViewModel.DiscountApprovalRequestId));
                return View("Form", await BuildFormAsync(model, cancellationToken));
            }

            var saleId = await saleService.CreateSaleAsync(new CreateSaleRequest(
                model.CashSessionId,
                model.CustomerId,
                saleItems,
                new SalePaymentRequest(
                    model.PaymentMethod,
                    model.ReceivedAmount,
                    model.TransferSourceBank,
                    model.TransferBankAccountId,
                    model.TransferReference,
                    model.TransferAmount,
                    new DateTimeOffset(model.TransferDateLocal),
                    model.TransferStatus,
                    model.CardType,
                    model.CardLast4,
                    model.CardAuthorizationNumber,
                    model.CardVoucherReference,
                    model.CardAmount,
                    model.CardStatus,
                    model.PosDeviceId,
                    model.TransferSenderName),
                model.DiscountReason,
                model.DiscountAuthorizedByUserName,
                CustomerNit: model.CustomerNit,
                CustomerName: model.CustomerName,
                CustomerAddress: model.CustomerAddress,
                CreditDueDateUtc: model.PaymentMethod == PaymentMethodType.Credit ? new DateTimeOffset(model.CreditDueDateLocal) : null,
                DiscountAuthorizerPassword: model.DiscountAuthorizerPassword,
                RequirePerLineDiscountAuthorization: true,
                CustomerPhone: model.CustomerPhone,
                CustomerVisitOrigin: model.CustomerVisitOrigin,
                DiscountApprovalRequestId: model.DiscountApprovalRequestId, ClientOperationId: model.OperationId,
                TerminalId: model.TerminalId, ExpectedTotal: model.ExpectedTotal),
                cancellationToken);

            var printUrl = Url.Action(nameof(Print), new { id = saleId, autoPrint = true })!;
            if (IsAjaxRequest()) return Json(new { redirectUrl = printUrl });
            return Redirect(printUrl);
        }
        catch (DomainRuleException ex)
        {
            if (IsAjaxRequest()) return BadRequest(new { message = ex.Message });
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Form", await BuildFormAsync(model, cancellationToken));
        }
        catch (DbUpdateConcurrencyException)
        {
            const string message = "La existencia o la caja cambió mientras se confirmaba la venta. Revise los productos y vuelva a cobrar.";
            if (IsAjaxRequest()) return Conflict(new { message });
            ModelState.AddModelError(string.Empty, message);
            return View("Form", await BuildFormAsync(model, cancellationToken));
        }
    }

    [Authorize(Policy = PermissionCodes.SalesManage)]
    [HttpGet]
    public async Task<IActionResult> LookupCustomerByNit(string nit, CancellationToken cancellationToken)
    {
        var result = await nitLookupService.LookupAsync(nit, cancellationToken);
        var customer = result.CustomerId is Guid customerId
            ? await customerService.GetCustomerAsync(customerId, cancellationToken)
            : null;
        return Json(new
        {
            result.Found,
            result.CustomerId,
            result.Nit,
            result.Name,
            result.Address,
            Phone = customer?.Phone,
            VisitOrigin = customer?.VisitOrigin,
            CreditDays = customer?.CreditDays ?? 30,
            GraceDays = customer?.GraceDays ?? 0,
            Source = result.Source.ToString(),
            result.Message
        });
    }

    [Authorize(Policy = PermissionCodes.SalesManage)]
    [HttpGet]
    public async Task<IActionResult> LookupCustomerByPhone(string phone, CancellationToken cancellationToken)
    {
        var normalizedPhone = string.Concat((phone ?? string.Empty).Where(char.IsDigit));
        if (normalizedPhone.Length == 0) return Json(new { found = false });

        var candidates = await dbContext.Customers
            .AsNoTracking()
            .Where(x => x.IsActive && x.Phone != null)
            .ToListAsync(cancellationToken);
        var customer = candidates.FirstOrDefault(x =>
            string.Concat((x.Phone ?? string.Empty).Where(char.IsDigit)) == normalizedPhone);
        if (customer is null) return Json(new { found = false });

        return Json(new
        {
            found = true,
            customerId = customer.Id,
            name = customer.Name,
            address = customer.Address,
            phone = customer.Phone,
            visitOrigin = customer.VisitOrigin,
            creditDays = customer.CreditDays,
            graceDays = customer.GraceDays
        });
    }

    [Authorize(Policy = PermissionCodes.SalesManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOriginBank(string name, CancellationToken cancellationToken)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return BadRequest(new { message = "El nombre del banco es requerido." });
        if (await dbContext.OriginBanks.AnyAsync(x => x.Name == normalized, cancellationToken))
            return BadRequest(new { message = "Este banco ya existe." });
        var bank = new OriginBank(normalized);
        dbContext.OriginBanks.Add(bank);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Json(new { id = bank.Name, text = bank.Name });
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var sale = await saleService.GetSaleAsync(id, cancellationToken);
        if (sale is null)
        {
            return NotFound();
        }

        return CanAccessSale(sale) ? View(await BuildDetailsAsync(sale, cancellationToken)) : Forbid();
    }

    [Authorize(Policy = PermissionCodes.SalesCancel)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel([Bind(Prefix = "Cancellation")] CancelSaleViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            await saleService.CancelSaleAsync(model.SaleId, new CancelSaleRequest(model.Reason, model.AuthorizerUserName, model.AuthorizerPassword), cancellationToken);
            return RedirectToAction(nameof(Details), new { id = model.SaleId });
        }
        catch (DomainRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var sale = await saleService.GetSaleAsync(model.SaleId, cancellationToken);
            return sale is null ? NotFound() : View("Details", await BuildDetailsAsync(sale, cancellationToken, cancellation: model));
        }
    }

    [Authorize(Policy = PermissionCodes.SalesReturn)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Return([Bind(Prefix = "Return")] ReturnSaleViewModel model, CancellationToken cancellationToken)
    {
        model.Items = model.Items.Where(x => x.Quantity > 0).ToList();
        if (model.Items.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Debe indicar al menos una línea para devolución.");
        }

        if (!ModelState.IsValid)
        {
            var invalidSale = await saleService.GetSaleAsync(model.SaleId, cancellationToken);
            return invalidSale is null ? NotFound() : View("Details", await BuildDetailsAsync(invalidSale, cancellationToken, saleReturn: model));
        }

        try
        {
            await saleService.ReturnSaleAsync(
                model.SaleId,
                new ReturnSaleRequest(
                    model.Reason,
                    model.Items.Select(x => new ReturnSaleItemRequest(x.ProductId, x.Quantity, x.ReturnedToInventory, x.LotId)).ToList(),
                    model.AuthorizerUserName, model.AuthorizerPassword, model.OperationId),
                cancellationToken);
            return RedirectToAction(nameof(Details), new { id = model.SaleId });
        }
        catch (DomainRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var sale = await saleService.GetSaleAsync(model.SaleId, cancellationToken);
            return sale is null ? NotFound() : View("Details", await BuildDetailsAsync(sale, cancellationToken, saleReturn: model));
        }
    }

    [Authorize(Policy = PermissionCodes.SalesReturn)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refund([Bind(Prefix = "Refund")] RefundSaleViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var invalidSale = await saleService.GetSaleAsync(model.SaleId, cancellationToken);
            return invalidSale is null ? NotFound() : View("Details", await BuildDetailsAsync(invalidSale, cancellationToken, refund: model));
        }
        try
        {
            await saleService.RefundSaleAsync(
                model.SaleId,
                new RefundSaleRequest(
                    model.CashSessionId,
                    model.Amount,
                    model.RefundMethod,
                    model.Reason,
                    model.AuthorizedByUserName,
                    model.Reference,
                    model.AuthorizerPassword, model.PaymentId, model.OperationId),
                cancellationToken);
            return RedirectToAction(nameof(Details), new { id = model.SaleId });
        }
        catch (DomainRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var sale = await saleService.GetSaleAsync(model.SaleId, cancellationToken);
            return sale is null ? NotFound() : View("Details", await BuildDetailsAsync(sale, cancellationToken, refund: model));
        }
    }

    [Authorize(Policy = PermissionCodes.TransfersVerify)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyTransfer(TransferReviewViewModel model, CancellationToken cancellationToken)
    {
        await saleService.VerifyTransferAsync(model.SaleId, model.Observation, cancellationToken);
        return RedirectToAction(nameof(Details), new { id = model.SaleId });
    }

    [Authorize(Policy = PermissionCodes.TransfersVerify)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectTransfer(TransferReviewViewModel model, CancellationToken cancellationToken)
    {
        await saleService.RejectTransferAsync(model.SaleId, model.Observation, cancellationToken);
        return RedirectToAction(nameof(Details), new { id = model.SaleId });
    }

    public async Task<IActionResult> Ticket(Guid id, CancellationToken cancellationToken, string? format = null)
    {
        var sale = await saleService.GetSaleAsync(id, cancellationToken);
        if (sale is null)
        {
            return NotFound();
        }
        if (!CanAccessSale(sale))
        {
            return Forbid();
        }

        if (string.Equals(format, "text", StringComparison.OrdinalIgnoreCase))
        {
            var content = await receiptService.GenerateTicketAsync(id, cancellationToken);
            return Content(content, "text/plain; charset=utf-8");
        }

        return View(new PrintableReceiptViewModel
        {
            Sale = sale,
            Settings = await settingsService.GetSystemSettingAsync(cancellationToken),
            CustomerNit = string.IsNullOrWhiteSpace(sale.CustomerNit) ? "C/F" : sale.CustomerNit
        });
    }

    public async Task<IActionResult> Print(Guid id, bool autoPrint = false, CancellationToken cancellationToken = default)
    {
        var sale = await saleService.GetSaleAsync(id, cancellationToken);
        if (sale is null)
        {
            return NotFound();
        }
        if (!CanAccessSale(sale))
        {
            return Forbid();
        }

        var settings = await settingsService.GetSystemSettingAsync(cancellationToken);

        return View(new PrintableReceiptViewModel
        {
            Sale = sale,
            Settings = settings,
            CustomerNit = string.IsNullOrWhiteSpace(sale.CustomerNit) ? "C/F" : sale.CustomerNit
        });
    }

    [HttpGet]
    public async Task<IActionResult> BusinessLogo(CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.GetSystemSettingAsync(cancellationToken);
        var logoPath = settings.BusinessLogoUrl?.Trim();
        if (string.IsNullOrWhiteSpace(logoPath) || !Path.IsPathRooted(logoPath) || !System.IO.File.Exists(logoPath))
        {
            return NotFound();
        }

        var contentType = Path.GetExtension(logoPath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            _ => null
        };

        return contentType is null ? NotFound() : PhysicalFile(logoPath, contentType);
    }

    [Authorize(Roles = SecurityRoleNames.PointOfSaleAccess)]
    [Authorize(Policy = PermissionCodes.SalesManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateProforma(SaleFormViewModel model, CancellationToken cancellationToken)
    {
        var items = model.Items
            .Where(x => x.ProductId != Guid.Empty && x.Quantity > 0)
            .Select(x => new ProformaItemRequest(x.ProductId, x.Quantity, x.DiscountType, x.DiscountValue))
            .ToList();

        if (items.Count == 0)
        {
            return BadRequest(new { message = "Debe agregar al menos un producto para generar la proforma." });
        }

        try
        {
            var content = await receiptService.GenerateProformaPdfAsync(
                new ProformaRequest(items, model.CustomerId, model.CustomerName, model.CustomerNit, model.CustomerAddress),
                cancellationToken);
            return File(content, "application/pdf", "proforma.pdf");
        }
        catch (DomainRuleException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (NotFoundException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    public async Task<IActionResult> Pdf(Guid id, CancellationToken cancellationToken)
    {
        var sale = await saleService.GetSaleAsync(id, cancellationToken);
        if (sale is null)
        {
            return NotFound();
        }
        if (!CanAccessSale(sale))
        {
            return Forbid();
        }

        var requestKey = Guid.TryParse(Request.Query["requestKey"], out var key) ? key : Guid.NewGuid();
        return View(new SalePdfRequestViewModel(sale.Id, sale.Number, requestKey));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> RequestPdf(Guid id, string requestKey, CancellationToken cancellationToken)
    {
        var job = await pdfQueue.RequestAsync(id, requestKey, cancellationToken);
        return Accepted(new
        {
            job.Id, job.Status,
            statusUrl = Url.Action(nameof(PdfStatus), new { jobId = job.Id }),
            downloadUrl = Url.Action(nameof(PdfDownload), new { jobId = job.Id })
        });
    }

    [HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> PdfStatus(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await pdfQueue.GetAsync(jobId, cancellationToken);
        if (job is null) return NotFound();
        return Json(new { job.Id, job.Status, job.Attempts, downloadUrl = Url.Action(nameof(PdfDownload), new { jobId }) });
    }

    [HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> PdfDownload(Guid jobId, CancellationToken cancellationToken)
    {
        var file = await pdfQueue.DownloadAsync(jobId, cancellationToken);
        return file is null ? NotFound() : File(file.Content, "application/pdf", $"{file.SaleNumber}.pdf");
    }

    private async Task<SaleDetailsViewModel> BuildDetailsAsync(
        SaleDto sale,
        CancellationToken cancellationToken,
        CancelSaleViewModel? cancellation = null,
        ReturnSaleViewModel? saleReturn = null,
        RefundSaleViewModel? refund = null)
    {
        var currentCashSession = await cashService.GetCurrentSessionAsync(cancellationToken);

        saleReturn ??= new ReturnSaleViewModel
        {
            SaleId = sale.Id,
            Items = sale.Items
                .Select(x => new ReturnSaleItemViewModel
                {
                    ProductId = x.ProductId,
                    ProductName = x.ProductName,
                    SoldQuantity = x.Quantity
                })
                .ToList()
        };

        if (saleReturn.SaleId == Guid.Empty)
        {
            saleReturn.SaleId = sale.Id;
        }

        if (saleReturn.Items.Count == 0)
        {
            saleReturn.Items = sale.Items
                .Select(x => new ReturnSaleItemViewModel
                {
                    ProductId = x.ProductId,
                    ProductName = x.ProductName,
                    SoldQuantity = x.Quantity
                })
                .ToList();
        }

        var originalLots = await dbContext.InventoryMovementLots.AsNoTracking()
            .Where(x => x.Movement.SourceDocument == sale.Number
                && x.Movement.Type == SistemiPos.Domain.Inventory.InventoryMovementType.SaleExit)
            .Select(x => new { x.Movement.ProductId, x.LotId, x.Lot.LotNumber, x.Lot.ExpiryDate }).Distinct()
            .ToListAsync(cancellationToken);
        foreach (var item in saleReturn.Items)
            item.AvailableLots = originalLots.Where(x => x.ProductId == item.ProductId)
                .Select(x => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(
                    $"{x.LotNumber} ({x.ExpiryDate?.ToString("dd/MM/yyyy") ?? "Sin caducidad"})", x.LotId.ToString()))
                .ToList();

        refund ??= new RefundSaleViewModel
        {
            SaleId = sale.Id,
            CashSessionId = currentCashSession?.Id ?? Guid.Empty,
            Amount = sale.Total
        };

        if (refund.SaleId == Guid.Empty)
        {
            refund.SaleId = sale.Id;
        }

        if (refund.CashSessionId == Guid.Empty)
        {
            refund.CashSessionId = currentCashSession?.Id ?? Guid.Empty;
        }

        cancellation ??= new CancelSaleViewModel { SaleId = sale.Id };
        if (cancellation.SaleId == Guid.Empty)
        {
            cancellation.SaleId = sale.Id;
        }

        return new SaleDetailsViewModel
        {
            Sale = sale,
            Cancellation = cancellation,
            Return = saleReturn,
            Refund = refund,
            CashSessions = new[] { currentCashSession }
                .Where(x => x is not null && x.Status == CashSessionStatus.Active)
                .Select(x => new SelectListItem($"{x!.RegisterCode} - {x.CashierUserName}", x.Id.ToString()))
                .ToList()
        };
    }

    private async Task<SaleFormViewModel> BuildFormAsync(SaleFormViewModel model, CancellationToken cancellationToken)
    {
        var products = await catalogService.SearchProductsAsync(null, cancellationToken);
        var customers = await customerService.GetCustomersAsync(cancellationToken);
        var currentSession = await cashService.GetCurrentSessionAsync(cancellationToken);
        var directSaleProducts = products.Where(x => x.IsActive && x.IsVisible && !x.ManagesVariants
            && x.ArticleType is ArticleType.Reventa or ArticleType.ProductoDeVenta or ArticleType.Bebida).ToList();
        products = await recipeOperations.SaleCatalogAsync(directSaleProducts, currentSession?.BranchId, cancellationToken);
        var bankAccounts = await settingsService.GetBankAccountsAsync(cancellationToken);
        var settings = await settingsService.GetSystemSettingAsync(cancellationToken);

        var branchId = currentSession?.BranchId;
        Dictionary<Guid, int>? menuSortOrder = null;
        if (branchId.HasValue)
        {
            var restaurantSetup = await dbContext.RestaurantSetups.AsNoTracking()
                .FirstOrDefaultAsync(x => x.BranchId == branchId.Value, cancellationToken);
            var definition = restaurantSetup?.Read();
            if (definition?.Menu?.Count > 0)
            {
                menuSortOrder = definition.Menu
                    .GroupBy(m => m.ProductId)
                    .ToDictionary(g => g.Key, g => g.First().SortOrder);
            }
        }

        var vendibleProducts = products
            .Where(x => x.IsActive && x.IsVisible && !x.ManagesVariants
                && x.ArticleType != ArticleType.Preparado
                && x.ArticleType != ArticleType.Ingrediente
                && x.ArticleType != ArticleType.Insumo
                && x.ArticleType != ArticleType.Subreceta
                && x.ArticleType != ArticleType.Empaque)
            .Where(x => menuSortOrder == null || menuSortOrder.ContainsKey(x.Id))
            .OrderBy(x => menuSortOrder != null && menuSortOrder.TryGetValue(x.Id, out var order) ? order : 9999)
            .ThenBy(x => x.Name)
            .ToList();

        model.Products = vendibleProducts
            .Select(x => new SelectListItem($"{x.InternalCode} - {x.DisplayName ?? x.Name}", x.Id.ToString()))
            .ToList();

        model.ProductCatalog = vendibleProducts;

        model.Customers = customers
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToList();

        model.CustomerCatalog = customers
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToList();

        model.CashSessions = new[] { currentSession }
            .Where(x => x is not null)
            .Select(x => new SelectListItem($"{x!.RegisterCode} - {x.CashierUserName}", x.Id.ToString()))
            .ToList();

        if (currentSession is not null) model.CashSessionId = currentSession.Id;

        var assignedPosIds = currentSession?.AssignedPosDevices?.Select(x => x.PosDeviceId).ToList() ?? [];
        model.PosDevices = await dbContext.PosDevices.AsNoTracking()
            .Where(x => x.IsActive && (assignedPosIds.Count == 0 || assignedPosIds.Contains(x.Id))).OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Provider == null || x.TerminalId == null ? x.Name : $"{x.Name} · {x.Provider} · {x.TerminalId}", x.Id.ToString())).ToListAsync(cancellationToken);
        model.CreditPaymentsEnabled = settings.CreditPaymentsEnabled;

        model.BankAccounts = bankAccounts
            .Where(x => x.IsActive)
            .Select(x => new SelectListItem($"{x.Bank} - {x.AccountName}", x.Id.ToString()))
            .ToList();

        var originBankNames = await dbContext.OriginBanks.AsNoTracking().Where(x => x.IsActive)
            .Select(x => x.Name).ToListAsync(cancellationToken);
        model.OriginBanks = originBankNames.Concat(bankAccounts.Where(x => x.IsActive).Select(x => x.Bank))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x)
            .Select(x => new SelectListItem(x, x)).ToList();

        model.CashPaymentsEnabled = true;
        model.TransferPaymentsEnabled = settings.TransferPaymentsEnabled;
        model.PosPaymentsEnabled = settings.PosPaymentsEnabled;

        model.CurrencySymbol = settings.CurrencySymbol;
        model.Terminals = (await saleService.GetPosTerminalsAsync(cancellationToken))
            .Select(x => new SelectListItem($"{x.Code} · Caja {x.RegisterCode}", x.Id.ToString())).ToList();
        model.CurrentCashRegisterCode = currentSession?.RegisterCode;
        if (!model.TerminalId.HasValue && model.Terminals.Count == 1) model.TerminalId = Guid.Parse(model.Terminals[0].Value);
        model.CurrencyDecimals = settings.CurrencyDecimals;
        return model;
    }

    private static List<SaleItemRequest> MapSaleItems(SaleFormViewModel model) => model.Items.Select(item =>
        item.DiscountValue <= 0
            ? new SaleItemRequest(item.ProductId, item.Quantity, 0m, SaleDiscountType.None, 0m)
            : new SaleItemRequest(item.ProductId, item.Quantity,
                item.DiscountType == SaleDiscountType.Percentage ? item.DiscountValue : 0m,
                item.DiscountType, item.DiscountValue, DiscountComment: item.DiscountComment)).ToList();

    private bool IsAjaxRequest() =>
        string.Equals(Request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    private string FirstModelError() => ModelState.Values
        .SelectMany(value => value.Errors)
        .Select(error => error.ErrorMessage)
        .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
        ?? "Revise los datos del cobro e intente nuevamente.";

    private bool CanAccessSale(SaleDto sale) => true;
}
