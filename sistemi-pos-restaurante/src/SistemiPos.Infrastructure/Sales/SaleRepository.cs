using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Security;
using SistemiPos.Application.Sales;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Customers;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Sales;
using SistemiPos.Domain.Finance;
using SistemiPos.Domain.Audit;
using SistemiPos.Domain.Organizations;
using SistemiPos.Infrastructure.Persistence;
using SistemiPos.Infrastructure.Security;

namespace SistemiPos.Infrastructure.Sales;

public sealed class SaleRepository(AppDbContext dbContext, SistemiPos.Application.Abstractions.IOperationContextAccessor? operationContext = null) : ISaleRepository
{
    public Task<SaleRefund?> GetRefundByOperationAsync(Guid saleId, string operationId, CancellationToken ct = default) =>
        dbContext.SaleRefunds.AsNoTracking().SingleOrDefaultAsync(x => x.SaleId == saleId && x.OperationId == operationId.Trim(), ct);
    public Task<SaleReturn?> GetReturnByOperationAsync(Guid saleId, string operationId, CancellationToken ct = default) =>
        dbContext.SaleReturns.AsNoTracking().SingleOrDefaultAsync(x => x.SaleId == saleId && x.OperationId == operationId.Trim(), ct);
    public Task<List<PaymentMethodDefinition>> GetPaymentDefinitionsAsync(CancellationToken ct = default) =>
        dbContext.PaymentMethodDefinitions.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
    public Task<decimal> GetRefundedPaymentTotalAsync(Guid paymentId, CancellationToken cancellationToken = default) =>
        dbContext.SaleRefunds.Where(x => x.PaymentId == paymentId).SumAsync(x => x.Amount, cancellationToken);
    public Task<List<Sale>> GetSalesAsync(CancellationToken cancellationToken = default)
    {
        var currentBranchId = operationContext?.CurrentBranchId;
        var query = dbContext.Sales
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .Include(x => x.AccountParts)
            .Include(x => x.Customer)
            .AsNoTracking()
            .AsSplitQuery();

        if (currentBranchId.HasValue)
        {
            var sessionIdsInBranch = dbContext.CashSessions
                .Where(s => s.BranchId == currentBranchId.Value)
                .Select(s => s.Id);

            query = query.Where(s => sessionIdsInBranch.Contains(s.CashSessionId));
        }

        return query.ToListAsync(cancellationToken);
    }

    public Task<Sale?> GetSaleAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Sales
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .Include(x => x.AccountParts)
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id && (operationContext == null || !operationContext.CurrentBranchId.HasValue
                || dbContext.CashSessions.Any(s => s.Id == x.CashSessionId && s.BranchId == operationContext.CurrentBranchId)), cancellationToken);

    public Task<bool> ExistsClientOperationIdAsync(string clientOperationId, CancellationToken cancellationToken = default) =>
        dbContext.Sales.AnyAsync(x => x.ClientOperationId == clientOperationId.Trim(), cancellationToken);
    public Task<Sale?> GetByClientOperationIdAsync(string clientOperationId, CancellationToken ct = default) =>
        dbContext.Sales.Include(x => x.Items).Include(x => x.Payments).Include(x => x.AccountParts).AsSplitQuery().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ClientOperationId == clientOperationId.Trim() && (operationContext == null || !operationContext.CurrentBranchId.HasValue
                || dbContext.CashSessions.Any(s => s.Id == x.CashSessionId && s.BranchId == operationContext.CurrentBranchId)), ct);

    public async Task<string> GenerateSaleNumberAsync(CancellationToken cancellationToken = default)
        => await GenerateNumberAsync(
            dbContext.Sales.Select(x => x.Number),
            "V-",
            cancellationToken);

    public async Task<string> GenerateCancellationNumberAsync(CancellationToken cancellationToken = default)
        => await GenerateNumberAsync(
            dbContext.SaleCancellations.Select(x => x.Number),
            "A-",
            cancellationToken);

    public async Task<string> GenerateReturnNumberAsync(CancellationToken cancellationToken = default)
        => await GenerateNumberAsync(
            dbContext.SaleReturns.Select(x => x.Number),
            "D-",
            cancellationToken);

    public Task<CashSession?> GetCashSessionAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.CashSessions.Include(x => x.Movements).Include(x => x.AssignedPosDevices).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Customer?> GetActiveCustomerByNormalizedNitAsync(string normalizedNit, CancellationToken cancellationToken = default) =>
        dbContext.Customers.FirstOrDefaultAsync(
            x => x.IsActive && (x.NormalizedNit == normalizedNit
                || (x.Nit != null && x.Nit.Replace("-", "").Replace(" ", "").ToUpper() == normalizedNit)),
            cancellationToken);

    public async Task<Customer?> GetActiveCustomerByPhoneAsync(string phone, CancellationToken cancellationToken = default)
    {
        var normalizedPhone = NormalizePhone(phone);
        if (normalizedPhone.Length == 0) return null;

        var candidates = await dbContext.Customers
            .Where(x => x.IsActive && x.Phone != null)
            .ToListAsync(cancellationToken);
        return candidates.FirstOrDefault(x => NormalizePhone(x.Phone) == normalizedPhone);
    }

    private static string NormalizePhone(string? value) =>
        string.Concat((value ?? string.Empty).Where(char.IsDigit));

    public Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken = default) =>
        dbContext.Customers.AddAsync(customer, cancellationToken).AsTask();

    public Task<Product?> GetProductAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Products
            .Include(x => x.VariantOptions).ThenInclude(x => x.VariantOption).ThenInclude(x => x.VariantType)
            .Include(x => x.Category)
            .Include(x => x.PriceRules)
            .Include(x => x.BranchSettings)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<InventoryStock?> GetStockAsync(Guid productId, CancellationToken cancellationToken = default) =>
        dbContext.InventoryStocks.FirstOrDefaultAsync(x => x.ProductId == productId
            && (operationContext == null || !operationContext.CurrentWarehouseId.HasValue || x.WarehouseId == operationContext.CurrentWarehouseId), cancellationToken);

    public Task<InventoryStock?> GetStockAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default) =>
        dbContext.InventoryStocks.FirstOrDefaultAsync(x => x.ProductId == productId && x.WarehouseId == warehouseId, cancellationToken);

    public async Task<Warehouse?> GetDefaultWarehouseForBranchAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        var warehouse = await dbContext.Warehouses.FirstOrDefaultAsync(x => x.BranchId == branchId && x.IsDefault && x.IsActive, cancellationToken);
        return warehouse ?? await dbContext.Warehouses.FirstOrDefaultAsync(x => x.BranchId == branchId && x.IsActive, cancellationToken);
    }

    public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default) =>
        dbContext.InventoryMovements.AddAsync(movement, cancellationToken).AsTask();

    public Task<List<InventoryExpiryLot>> GetExpiryLotsAsync(Guid productId, CancellationToken cancellationToken = default) =>
        dbContext.InventoryExpiryLots.Where(x => x.ProductId == productId && x.AvailableQuantity > 0)
            .OrderBy(x => x.ExpiryDate).ToListAsync(cancellationToken);

    public Task AddCashMovementAsync(CashMovement movement, CancellationToken cancellationToken = default) =>
        dbContext.CashMovements.AddAsync(movement, cancellationToken).AsTask();

    public async Task<SystemSetting> GetSystemSettingAsync(CancellationToken cancellationToken = default)
    {
        var setting = await dbContext.SystemSettings.FirstOrDefaultAsync(cancellationToken);
        if (setting is not null)
        {
            return setting.WithOperationalCurrency(await SistemiPos.Infrastructure.Cash.CashCurrency.ResolveAsync(dbContext, operationContext?.CurrentBranchId, cancellationToken));
        }

        setting = SystemSetting.CreateDefault();
        await dbContext.SystemSettings.AddAsync(setting, cancellationToken);
        return setting.WithOperationalCurrency(await SistemiPos.Infrastructure.Cash.CashCurrency.ResolveAsync(dbContext, operationContext?.CurrentBranchId, cancellationToken));
    }

    public Task<BankAccount?> GetBankAccountAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.BankAccounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task AddSaleAsync(Sale sale, CancellationToken cancellationToken = default) =>
        dbContext.Sales.AddAsync(sale, cancellationToken).AsTask();

    public Task AddDiscountApprovalAsync(DiscountApprovalRequest request, CancellationToken cancellationToken = default) =>
        dbContext.DiscountApprovalRequests.AddAsync(request, cancellationToken).AsTask();

    public Task<DiscountApprovalRequest?> GetDiscountApprovalAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.DiscountApprovalRequests.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id
            && (operationContext == null || !operationContext.CurrentBranchId.HasValue
                || dbContext.CashSessions.Any(s => s.Id == x.CashSessionId && s.BranchId == operationContext.CurrentBranchId)), cancellationToken);

    public Task<List<DiscountApprovalRequest>> GetPendingDiscountApprovalsAsync(CancellationToken cancellationToken = default) =>
        dbContext.DiscountApprovalRequests.Include(x => x.Items)
            .Where(x => x.Status == DiscountApprovalStatus.Pending && (operationContext == null || !operationContext.CurrentBranchId.HasValue
                || dbContext.CashSessions.Any(s => s.Id == x.CashSessionId && s.BranchId == operationContext.CurrentBranchId)))
            .OrderBy(x => x.RequestedAtUtc).ToListAsync(cancellationToken);

    public Task<PosDevice?> GetPosDeviceAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.PosDevices.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken);

    public Task AddReceivableAccountAsync(ReceivableAccount account, CancellationToken cancellationToken = default) =>
        dbContext.ReceivableAccounts.AddAsync(account, cancellationToken).AsTask();

    public Task<CashSession?> GetActiveCashSessionByUserAsync(string userName, CancellationToken cancellationToken = default) =>
        dbContext.CashSessions.Include(x => x.Movements).FirstOrDefaultAsync(x => x.Status == CashSessionStatus.Active && x.CashierUserName == userName, cancellationToken);

    public Task<ReceivableAccount?> GetReceivableAccountBySaleAsync(Guid saleId, CancellationToken cancellationToken = default) =>
        dbContext.ReceivableAccounts.Include(x => x.Payments).FirstOrDefaultAsync(x => x.SaleId == saleId, cancellationToken);

    public Task AddFinancialAuditAsync(FinancialAuditEntry entry, CancellationToken cancellationToken = default) =>
        dbContext.FinancialAuditEntries.AddAsync(entry, cancellationToken).AsTask();

    public Task<bool> IsPaymentMethodEnabledAsync(PaymentMethodType paymentMethod, CancellationToken cancellationToken = default)
    {
        if (paymentMethod == PaymentMethodType.Cash) return Task.FromResult(true);
        var behavior = paymentMethod switch
        {
            PaymentMethodType.Cash => PaymentFinancialBehavior.Cash,
            PaymentMethodType.Transfer => PaymentFinancialBehavior.Transfer,
            PaymentMethodType.Pos => PaymentFinancialBehavior.Pos,
            PaymentMethodType.Credit => PaymentFinancialBehavior.Credit,
            _ => PaymentFinancialBehavior.OtherNonCash
        };
        return dbContext.PaymentMethodDefinitions.AnyAsync(x => x.Behavior == behavior && x.IsActive, cancellationToken);
    }

    public Task AddCancellationAsync(SaleCancellation cancellation, CancellationToken cancellationToken = default) =>
        dbContext.SaleCancellations.AddAsync(cancellation, cancellationToken).AsTask();

    public Task AddReturnAsync(SaleReturn saleReturn, CancellationToken cancellationToken = default) =>
        dbContext.SaleReturns.AddAsync(saleReturn, cancellationToken).AsTask();

    public Task AddRefundAsync(SaleRefund refund, CancellationToken cancellationToken = default) =>
        dbContext.SaleRefunds.AddAsync(refund, cancellationToken).AsTask();

    public Task<Dictionary<Guid, decimal>> GetReturnedQuantitiesByProductAsync(Guid saleId, CancellationToken cancellationToken = default) =>
        dbContext.SaleReturnItems
            .Where(x => dbContext.SaleReturns.Where(r => r.SaleId == saleId).Select(r => r.Id).Contains(x.SaleReturnId))
            .GroupBy(x => x.ProductId)
            .Select(x => new { x.Key, Quantity = x.Sum(y => y.Quantity) })
            .ToDictionaryAsync(x => x.Key, x => x.Quantity, cancellationToken);

    public async Task<decimal> GetRefundedTotalAsync(Guid saleId, CancellationToken cancellationToken = default) =>
        await dbContext.SaleRefunds
            .Where(x => x.SaleId == saleId)
            .Select(x => (decimal?)x.Amount)
            .SumAsync(cancellationToken)
        ?? 0m;

    public async Task<decimal?> GetUserDiscountLimitAsync(string userName, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserName == userName, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var userSetting = await dbContext.UserDiscountSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == user.Id, cancellationToken);
        if (userSetting is not null)
        {
            return userSetting.MaxDiscountPercent;
        }

        var roleIds = await dbContext.UserRoles
            .Where(x => x.UserId == user.Id)
            .Select(x => x.RoleId)
            .ToListAsync(cancellationToken);

        if (roleIds.Count == 0)
        {
            return null;
        }

        return await dbContext.RoleDiscountSettings
            .Where(x => roleIds.Contains(x.RoleId))
            .Select(x => (decimal?)x.MaxDiscountPercent)
            .MaxAsync(cancellationToken);
    }

    public async Task<bool> UserHasPermissionAsync(string userName, string permissionCode, CancellationToken cancellationToken = default)
    {
        var userId = await dbContext.Users
            .Where(x => x.UserName == userName)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!userId.HasValue)
        {
            return false;
        }

        var roleIds = await dbContext.UserRoles
            .Where(x => x.UserId == userId.Value)
            .Select(x => x.RoleId)
            .ToListAsync(cancellationToken);

        if (roleIds.Count == 0)
        {
            return false;
        }

        return await dbContext.RolePermissions
            .Include(x => x.Permission)
            .AnyAsync(
                x => roleIds.Contains(x.RoleId) && x.Permission.Code == permissionCode,
                cancellationToken);
    }

    private static async Task<string> GenerateNumberAsync(
        IQueryable<string> numbers,
        string prefix,
        CancellationToken cancellationToken)
    {
        var lastNumber = await numbers
            .OrderByDescending(x => x)
            .FirstOrDefaultAsync(cancellationToken);

        var nextSequence = 1;
        if (!string.IsNullOrWhiteSpace(lastNumber) && lastNumber.StartsWith(prefix, StringComparison.Ordinal))
        {
            var suffix = lastNumber[prefix.Length..];
            if (int.TryParse(suffix, out var parsed))
            {
                nextSequence = parsed + 1;
            }
        }

        return $"{prefix}{nextSequence:000000}";
    }
}
