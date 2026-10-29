using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Common.Exceptions;
using SistemiPos.Application.Finance;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Audit;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Finance;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Finance;

public sealed class FinancialService(AppDbContext db, ICurrentUserAccessor currentUser, IDateTimeProvider clock,
    IOperationAuthorizationService authorization) : IFinancialService
{
    public async Task<IReadOnlyList<PayableAccountDto>> GetPayablesAsync(CancellationToken ct = default) =>
        await (from a in db.PayableAccounts.AsNoTracking()
               join s in db.Suppliers.AsNoTracking() on a.SupplierId equals s.Id
               orderby a.DueDateUtc descending
               select new PayableAccountDto(a.Id, a.PurchaseId, a.SupplierId, s.LegalName, a.InvoiceNumber,
                   a.InvoiceDateUtc, a.DueDateUtc, a.Total, a.Paid, a.Balance,
                   a.Status == AccountStatus.Pending && a.DueDateUtc < clock.UtcNow ? AccountStatus.Overdue : a.Status)).ToListAsync(ct);

    public async Task<PayableAccountDetailDto?> GetPayableAsync(Guid id, CancellationToken ct = default)
    {
        var account = await db.PayableAccounts.AsNoTracking().Include(x => x.Payments).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (account is null) return null;
        var purchase = await db.Purchases.AsNoTracking().SingleAsync(x => x.Id == account.PurchaseId, ct);
        var supplier = await db.Suppliers.AsNoTracking().SingleAsync(x => x.Id == account.SupplierId, ct);
        var orderNumber = purchase.PurchaseOrderId.HasValue
            ? await db.PurchaseOrders.AsNoTracking().Where(x => x.Id == purchase.PurchaseOrderId).Select(x => x.Number).SingleOrDefaultAsync(ct)
            : null;
        var status = account.Status == AccountStatus.Pending && account.DueDateUtc < clock.UtcNow ? AccountStatus.Overdue : account.Status;
        return new PayableAccountDetailDto(account.Id, account.PurchaseId, account.SupplierId, supplier.LegalName,
            account.InvoiceNumber, purchase.Number, purchase.PurchaseOrderId, orderNumber, account.InvoiceDateUtc,
            account.DueDateUtc, account.Total, account.Paid, account.Balance, status,
            account.Payments.OrderByDescending(x => x.PaidAtUtc).Select(x => new PayablePaymentDto(x.Id, x.PaidAtUtc,
                x.Amount, x.PaymentMethod, x.MoneySource, x.Reference, x.UserName)).ToList());
    }

    public async Task PayPayableAsync(Guid id, PayPayableRequest request, CancellationToken ct = default)
    {
        var account = await db.PayableAccounts.Include(x => x.Payments).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Cuenta por pagar no encontrada.");
        var authorizedBy = await ResolveMovementAuthorizationAsync(request.AuthorizerUserName, request.AuthorizerPassword, ct);
        var payment = new PayablePayment(clock.UtcNow, request.Amount, request.PaymentMethod, request.MoneySource,
            request.SourceAccountId, request.Reference, currentUser.UserName, authorizedBy);

        if (request.MoneySource == MoneySourceType.SalesCash)
        {
            var session = await CurrentSessionAsync(ct);
            var movement = new CashMovement(CashMovementType.SupplierPayment, request.Amount, $"Pago proveedor {account.InvoiceNumber}",
                currentUser.UserName, clock.UtcNow, authorizedBy, nameof(PayableAccount), account.Id, account.InvoiceNumber) { CashSessionId = session.Id };
            session.AddMovement(movement); payment.CashMovementId = movement.Id;
        }
        else if (request.MoneySource == MoneySourceType.PettyCash)
        {
            var fund = await GetFundEntityAsync(ct);
            var movement = fund.AddMovement(new PettyCashMovement(PettyCashMovementType.SupplierPayment, request.Amount,
                $"Pago proveedor {account.InvoiceNumber}", "Proveedores", account.InvoiceNumber, null, request.Reference,
                currentUser.UserName, clock.UtcNow, authorizedBy, nameof(PayableAccount), account.Id, fund.CurrentCycleId));
            payment.PettyCashMovementId = movement.Id;
        }

        account.AddPayment(payment);
        Audit("Pago a proveedor", null, account.Id, account.InvoiceNumber, request.Amount, authorizedBy, request.Reference);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ReceivableListResult> GetReceivablesAsync(ReceivableQuery query, CancellationToken ct = default)
    {
        var accounts = await db.ReceivableAccounts.AsNoTracking().Include(x => x.Payments).ToListAsync(ct);
        var customerIds = accounts.Select(x => x.CustomerId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(x => customerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var now = clock.UtcNow;

        var all = accounts
            .Where(x => x.Status != AccountStatus.Cancelled && customers.ContainsKey(x.CustomerId))
            .Select(x => new ReceivableAccountDto(x.Id, x.SaleId, x.CustomerId, customers[x.CustomerId].Name,
                x.SaleNumber, x.SaleDateUtc, x.DueDateUtc, x.Total, x.Paid, x.Balance, CurrentStatus(x, now),
                x.CreditDays, x.GraceDays, x.Payments.OrderByDescending(p => p.ReceivedAtUtc).Select(p => (DateTimeOffset?)p.ReceivedAtUtc).FirstOrDefault()))
            .ToList();

        var filtered = all.AsEnumerable();
        if (query.CustomerId.HasValue) filtered = filtered.Where(x => x.CustomerId == query.CustomerId);
        if (query.Status.HasValue) filtered = filtered.Where(x => x.Status == query.Status);
        if (query.From.HasValue) filtered = filtered.Where(x => x.DueDateUtc.Date >= query.From.Value.Date);
        if (query.To.HasValue) filtered = filtered.Where(x => x.DueDateUtc.Date <= query.To.Value.Date);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            filtered = filtered.Where(x => x.CustomerName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || x.SaleNumber.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var applicable = filtered.ToList();
        var pending = applicable.Where(x => x.Balance > 0).ToList();
        var summary = new ReceivableSummaryDto(pending.Sum(x => x.Balance), pending.Count,
            pending.Count(x => x.Paid == 0), pending.Count(x => x.Paid > 0),
            pending.Count(x => x.Status == AccountStatus.Overdue), pending.Where(x => x.Status == AccountStatus.Overdue).Sum(x => x.Balance),
            pending.Count(x => x.Status == AccountStatus.Delinquent), pending.Where(x => x.Status == AccountStatus.Delinquent).Sum(x => x.Balance));

        var sectionItems = query.Section switch
        {
            ReceivableSection.Overdue => applicable.Where(x => x.Balance > 0 && x.Status == AccountStatus.Overdue),
            ReceivableSection.Delinquent => applicable.Where(x => x.Balance > 0 && x.Status == AccountStatus.Delinquent),
            ReceivableSection.WithoutPayments => applicable.Where(x => x.Balance > 0 && x.Paid == 0),
            ReceivableSection.PartiallyPaid => applicable.Where(x => x.Balance > 0 && x.Paid > 0),
            ReceivableSection.History => applicable.Where(x => x.Balance == 0),
            _ => applicable.Where(x => x.Balance > 0)
        };
        sectionItems = query.Section == ReceivableSection.History
            ? sectionItems.OrderByDescending(x => x.LastPaymentAtUtc)
            : sectionItems.OrderBy(x => x.DueDateUtc);
        return new ReceivableListResult(sectionItems.ToList(), summary);
    }

    public async Task<ReceivableAccountDetailDto?> GetReceivableAsync(Guid id, CancellationToken ct = default)
    {
        var account = await db.ReceivableAccounts.AsNoTracking().Include(x => x.Payments).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (account is null) return null;
        var customer = await db.Customers.AsNoTracking().SingleAsync(x => x.Id == account.CustomerId, ct);
        return new ReceivableAccountDetailDto(account.Id, account.SaleId, account.CustomerId, customer.Name, customer.Nit,
            account.SaleNumber, account.SaleDateUtc, account.DueDateUtc, account.Total, account.Paid, account.Balance,
            account.CreditDays, account.GraceDays, CurrentStatus(account, clock.UtcNow),
            account.Payments.OrderByDescending(x => x.ReceivedAtUtc).Select(x => new ReceivablePaymentDto(x.Id,
                x.ReceivedAtUtc, x.Amount, x.PaymentMethod, x.Reference, x.UserName)).ToList());
    }

    public async Task CollectReceivableAsync(Guid id, CollectReceivableRequest request, CancellationToken ct = default)
    {
        var account = await db.ReceivableAccounts.Include(x => x.Payments).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Cuenta por cobrar no encontrada.");
        CashSession? cashSession = null;
        if (request.PaymentMethod == FinancialPaymentMethod.Cash)
            cashSession = await CurrentSessionAsync(ct);
        var cashSessionId = cashSession?.Id;
        var payment = new ReceivablePayment(clock.UtcNow, request.Amount, request.PaymentMethod, request.Reference,
            currentUser.UserName, null, cashSessionId);
        if (cashSession is not null)
        {
            var movement = new CashMovement(CashMovementType.CollectionCash, request.Amount, $"Abono {account.SaleNumber}",
                currentUser.UserName, clock.UtcNow, null, nameof(ReceivableAccount), account.Id, account.SaleNumber) { CashSessionId = cashSession.Id };
            cashSession.AddMovement(movement); payment.CashMovementId = movement.Id;
        }
        account.AddPayment(payment);
        Audit("Abono de cuenta por cobrar", cashSessionId, account.Id, account.SaleNumber, request.Amount, null, request.Reference);
        await db.SaveChangesAsync(ct);
    }

    public async Task<PettyCashDto> GetPettyCashAsync(CancellationToken ct = default) => await MapAsync(await GetFundEntityAsync(ct), ct);

    public async Task OpenPettyCashAsync(decimal initialBalance, CancellationToken ct = default)
    {
        var fund = await GetAssignedFundAsync(ct);
        var cycle = fund.Open(initialBalance, currentUser.UserId, currentUser.UserName, clock.UtcNow);
        db.PettyCashCycles.Add(cycle);
        Audit("Apertura de caja chica", null, cycle.Id, fund.Name, initialBalance, null, null);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddPettyCashMovementAsync(PettyCashMovementRequest request, CancellationToken ct = default)
    {
        var fund = await GetFundEntityAsync(ct);
        var category = request.CategoryId is null ? null : await db.PettyCashCategories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.CategoryId && x.IsActive, ct)
            ?? throw new DomainRuleException("La categoría seleccionada no existe o está inactiva.");
        var movement = fund.AddMovement(new PettyCashMovement(request.Type, request.Amount, request.Concept, category?.Name,
            request.Document, request.Beneficiary, request.Observation, currentUser.UserName, clock.UtcNow, cycleId: fund.CurrentCycleId, categoryId: category?.Id));
        Audit(request.Type == PettyCashMovementType.Replenishment ? "Reposición de caja chica" : "Gasto de caja chica",
            null, movement.Id, request.Document, request.Amount, null, request.Observation);
        await db.SaveChangesAsync(ct);
    }

    public async Task SettlePettyCashAsync(PettyCashSettlementRequest request, CancellationToken ct = default)
    {
        var fund = await GetFundEntityAsync(ct);
        var cycle = await db.PettyCashCycles.SingleOrDefaultAsync(x => x.Id == fund.CurrentCycleId, ct) ?? throw new DomainRuleException("No existe un ciclo abierto.");
        var settlement = new PettyCashSettlement(fund.Id, fund.Balance, request.CountedCash, currentUser.UserName, clock.UtcNow, request.Observation);
        fund.Close(cycle, request.CountedCash, currentUser.UserId, currentUser.UserName, clock.UtcNow, request.Observation);
        db.PettyCashSettlements.Add(settlement);
        Audit("Liquidación de caja chica", null, settlement.Id, null, request.CountedCash, null,
            $"Esperado: {fund.Balance:0.00}; diferencia: {settlement.Difference:0.00}. {request.Observation}");
        await db.SaveChangesAsync(ct);
    }

    public async Task CancelPettyCashMovementAsync(Guid movementId, string reason, string authorizerUserName, string authorizerPassword, CancellationToken ct = default)
    {
        var fund = await GetFundEntityAsync(ct);
        fund.CancelMovement(movementId, reason, currentUser.UserName, currentUser.UserName, clock.UtcNow);
        Audit("Anulación de movimiento de caja chica", null, movementId, null, 0m, currentUser.UserName, reason);
        await db.SaveChangesAsync(ct);
    }

    public async Task CancelPayableAsync(Guid id, CancellationToken ct = default)
    {
        var account = await db.PayableAccounts.Include(x => x.Payments).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Cuenta no encontrada.");
        account.Cancel(); Audit("Anulación de cuenta por pagar", null, account.Id, account.InvoiceNumber, account.Balance, null, null); await db.SaveChangesAsync(ct);
    }

    public async Task CancelReceivableAsync(Guid id, CancellationToken ct = default)
    {
        var account = await db.ReceivableAccounts.Include(x => x.Payments).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Cuenta no encontrada.");
        account.Cancel(); Audit("Anulación de cuenta por cobrar", null, account.Id, account.SaleNumber, account.Balance, null, null); await db.SaveChangesAsync(ct);
    }

    private async Task<CashSession> CurrentSessionAsync(CancellationToken ct) =>
        await db.CashSessions.FirstOrDefaultAsync(x => x.Status == CashSessionStatus.Active &&
            ((currentUser.UserId != null && x.CashierUserId == currentUser.UserId) || x.CashierUserName == currentUser.UserName), ct)
        ?? throw new DomainRuleException("Debe tener una caja abierta para realizar esta operación en efectivo.");

    private static AccountStatus CurrentStatus(ReceivableAccount account, DateTimeOffset now)
    {
        if (account.Balance == 0) return AccountStatus.Paid;
        if (account.Status == AccountStatus.Cancelled) return AccountStatus.Cancelled;
        var today = now.ToLocalTime().Date;
        var dueDate = account.DueDateUtc.ToLocalTime().Date;
        if (today > dueDate.AddDays(account.GraceDays)) return AccountStatus.Delinquent;
        if (today > dueDate) return AccountStatus.Overdue;
        return account.Paid > 0 ? AccountStatus.PartiallyPaid : AccountStatus.Pending;
    }

    private async Task<PettyCashFund> GetAssignedFundAsync(CancellationToken ct)
    {
        var fund = await db.PettyCashFunds.Include(x => x.Movements).FirstOrDefaultAsync(x => x.IsActive &&
            ((currentUser.UserId.HasValue && x.ResponsibleUserId == currentUser.UserId) || x.ResponsibleUserName == currentUser.UserName), ct);
        return fund ?? throw new DomainRuleException("No tiene una Caja chica asignada.");
    }

    private async Task<PettyCashFund> GetFundEntityAsync(CancellationToken ct)
    {
        var fund = await GetAssignedFundAsync(ct); fund.EnsureResponsible(currentUser.UserId, currentUser.UserName); return fund;
    }

    private async Task<string?> ResolveMovementAuthorizationAsync(string? user, string? password, CancellationToken ct)
    {
        if (await authorization.HasPermissionAsync(currentUser.UserName, PermissionCodes.CashMovementsWithoutAuthorization, ct)) return null;
        return await authorization.ValidateCredentialsAsync(user ?? "", password ?? "", PermissionCodes.CashMovementsAuthorize, ct);
    }

    private void Audit(string action, Guid? sessionId, Guid documentId, string? reference, decimal amount, string? authorizer, string? comment) =>
        db.FinancialAuditEntries.Add(new FinancialAuditEntry { OccurredAtUtc = clock.UtcNow, UserName = currentUser.UserName, Action = action,
            CashSessionId = sessionId, DocumentType = "Finance", DocumentId = documentId, DocumentReference = reference,
            Amount = amount, AuthorizedByUserName = authorizer, Comment = comment });

    private async Task<PettyCashDto> MapAsync(PettyCashFund f, CancellationToken ct)
    {
        var visibleCycleId = f.CurrentCycleId ?? await db.PettyCashCycles.AsNoTracking().Where(x => x.PettyCashFundId == f.Id).OrderByDescending(x => x.OpenedAtUtc).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        return new(f.Id, f.Name, f.ResponsibleUserName, f.FundAmount, f.Balance, f.IsActive,
            f.CurrentCycleId, f.OpenedAtUtc, f.ClosedAtUtc, f.Movements.Where(x => x.CycleId == visibleCycleId).OrderByDescending(x => x.OccurredAtUtc).Select(x => new PettyCashMovementDto(x.Id, x.Type, x.Amount,
                x.Concept, x.Category, x.Document, x.Beneficiary, x.Observation, x.UserName, x.OccurredAtUtc, x.AuthorizedByUserName, x.IsCancelled, x.CancellationReason)).ToList(),
            await db.PettyCashCategories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new PettyCashCategoryDto(x.Id, x.Name)).ToListAsync(ct));
    }
}
