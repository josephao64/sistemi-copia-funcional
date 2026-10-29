using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Reports;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Sales;
using SistemiPos.Domain.Purchases;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Reports;

public sealed class ReportRepository(AppDbContext dbContext, SistemiPos.Application.Abstractions.IOperationContextAccessor? context = null) : IReportRepository
{
    private IQueryable<Sale> SalesInScope() => dbContext.Sales.AsNoTracking().Where(x => context == null
        || (context.CurrentBranchId.HasValue && dbContext.CashSessions.Any(s => s.Id == x.CashSessionId && s.BranchId == context.CurrentBranchId)));
    private IQueryable<SistemiPos.Domain.Inventory.InventoryStock> StocksInScope() => dbContext.InventoryStocks.AsNoTracking()
        .Where(x => context == null || (context.CurrentBranchId.HasValue && x.Warehouse != null && x.Warehouse.BranchId == context.CurrentBranchId));
    private IQueryable<SistemiPos.Domain.Inventory.InventoryMovement> MovementsInScope() => dbContext.InventoryMovements.AsNoTracking()
        .Where(x => context == null || (context.CurrentBranchId.HasValue && x.Warehouse != null && x.Warehouse.BranchId == context.CurrentBranchId));
    public async Task<IReadOnlyList<ProductSalesRankingRow>> GetProductSalesRankingAsync(ProductSalesRankingFilter filter, CancellationToken cancellationToken = default)
    {
        var query =
            from sale in SalesInScope()
            join item in dbContext.SaleItems.AsNoTracking() on sale.Id equals item.SaleId
            join product in dbContext.Products.AsNoTracking() on item.ProductId equals product.Id
            join category in dbContext.Categories.AsNoTracking() on product.CategoryId equals category.Id
            where sale.Status == SaleStatus.Confirmed
            select new { sale, item, product, category };

        if (filter.FromUtc.HasValue)
        {
            query = query.Where(x => x.sale.SoldAtUtc >= filter.FromUtc.Value);
        }

        if (filter.ToUtc.HasValue)
        {
            query = query.Where(x => x.sale.SoldAtUtc < filter.ToUtc.Value);
        }

        if (filter.ProductId.HasValue)
        {
            query = query.Where(x => x.item.ProductId == filter.ProductId.Value);
        }

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(x => x.product.CategoryId == filter.CategoryId.Value);
        }

        return await query
            .GroupBy(x => new { x.item.ProductId, x.item.ProductName, CategoryName = x.category.Name })
            .Select(x => new ProductSalesRankingRow(
                x.Key.ProductId,
                x.Key.ProductName,
                x.Key.CategoryName,
                x.Sum(item => item.item.Quantity),
                x.Sum(item => item.item.Subtotal)))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CashierSalesReportRow>> GetCashierSalesReportAsync(SalesReportFilter filter, CancellationToken cancellationToken = default)
    {
        var query =
            from sale in SalesInScope()
            join item in dbContext.SaleItems.AsNoTracking() on sale.Id equals item.SaleId
            join product in dbContext.Products.AsNoTracking() on item.ProductId equals product.Id
            where sale.Status == SaleStatus.Confirmed
            select new { sale, item, product };

        if (filter.FromUtc.HasValue)
        {
            query = query.Where(x => x.sale.SoldAtUtc >= filter.FromUtc.Value);
        }

        if (filter.ToUtc.HasValue)
        {
            query = query.Where(x => x.sale.SoldAtUtc < filter.ToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.CashierUserName))
        {
            query = query.Where(x => x.sale.CashierUserName == filter.CashierUserName.Trim());
        }

        if (filter.PaymentMethod.HasValue)
        {
            query = query.Where(x => filter.PaymentMethod == PaymentMethodType.Mixed ? x.sale.Payments.Count > 1
                : x.sale.Payments.Any(p => p.PaymentMethod == filter.PaymentMethod.Value));
        }

        if (filter.ProductId.HasValue)
        {
            query = query.Where(x => x.item.ProductId == filter.ProductId.Value);
        }

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(x => x.product.CategoryId == filter.CategoryId.Value);
        }

        var salesWithItems = await query.ToListAsync(cancellationToken);

        return salesWithItems
            .GroupBy(x => x.sale.CashierUserName)
            .Select(group =>
            {
                var sales = group.GroupBy(x => x.sale.Id).ToList();
                return new CashierSalesReportRow(
                    group.Key,
                    sales.Count,
                    group.Sum(x => x.item.Quantity),
                    sales.Sum(x => x.First().sale.Total));
            })
            .OrderByDescending(x => x.TotalSold)
            .ThenBy(x => x.CashierUserName)
            .ToList();
    }

    public async Task<IReadOnlyList<DailySalesTotal>> GetDailySalesHistoryAsync(TimeSpan localOffset, CancellationToken cancellationToken = default)
    {
        var sales = await SalesInScope()
            .AsNoTracking()
            .Where(x => x.Status == SaleStatus.Confirmed)
            .Select(x => new { x.SoldAtUtc, x.Total })
            .ToListAsync(cancellationToken);

        return sales
            .GroupBy(x =>
            {
                var localDate = x.SoldAtUtc.ToOffset(localOffset);
                return new { localDate.Year, localDate.Month, localDate.Day };
            })
            .OrderBy(x => x.Key.Year)
            .ThenBy(x => x.Key.Month)
            .ThenBy(x => x.Key.Day)
            .Select(x => new DailySalesTotal(x.Key.Year, x.Key.Month, x.Key.Day, x.Sum(sale => sale.Total)))
            .ToList();
    }

    public async Task<IReadOnlyList<SalesReportRow>> GetSalesReportAsync(SalesReportFilter filter, CancellationToken cancellationToken = default)
    {
        var query = SalesInScope()
            .AsNoTracking()
            .Include(x => x.Payments)
            .Include(x => x.AccountParts)
            .Include(x => x.Customer)
            .Include(x => x.Items)
            .AsQueryable();

        if (filter.FromUtc.HasValue)
        {
            query = query.Where(x => x.SoldAtUtc >= filter.FromUtc.Value);
        }

        if (filter.ToUtc.HasValue)
        {
            query = query.Where(x => x.SoldAtUtc < filter.ToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.CashierUserName))
        {
            query = query.Where(x => x.CashierUserName == filter.CashierUserName.Trim());
        }

        if (filter.PaymentMethod.HasValue)
        {
            query = query.Where(x => filter.PaymentMethod == PaymentMethodType.Mixed ? x.Payments.Count > 1
                : x.Payments.Any(p => p.PaymentMethod == filter.PaymentMethod.Value));
        }

        if (filter.ProductId.HasValue)
        {
            query = query.Where(x => x.Items.Any(i => i.ProductId == filter.ProductId.Value));
        }

        if (filter.CategoryId.HasValue)
        {
            var productIds = await dbContext.Products
                .Where(x => x.CategoryId == filter.CategoryId.Value)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            query = query.Where(x => x.Items.Any(i => productIds.Contains(i.ProductId)));
        }

        var sales = await query
            .OrderByDescending(x => x.SoldAtUtc)
            .ToListAsync(cancellationToken);

        return sales.Select(x => new SalesReportRow(
            x.Id,
            x.Number,
            x.SoldAtUtc,
            x.CashierUserName,
            x.CustomerId.HasValue ? x.Customer?.Name ?? "Cliente" : "Consumidor final",
            x.Payment.PaymentMethod,
            x.Status,
            x.Total,
            x.DiscountTotal,
            x.TaxTotal,
            x.Items.Sum(i => i.Quantity),
            x.Items.Sum(i => i.CostAtSale * i.Quantity),
            x.Items.Select(i => new SalesReportProductRow(i.ProductId, i.ProductName, i.Quantity)).ToList())).ToList();
    }

    public async Task<InventoryReport> GetInventoryReportAsync(CancellationToken cancellationToken = default)
    {
        var stocksData = await (
            from stock in StocksInScope()
            join product in dbContext.Products.AsNoTracking() on stock.ProductId equals product.Id
            join category in dbContext.Categories.AsNoTracking() on product.CategoryId equals category.Id
            where !product.ManagesVariants
            select new
            {
                stock.ProductId,
                product.InternalCode,
                ProductName = product.VariantValue == null ? product.Name : product.Name + " " + product.VariantValue,
                CategoryName = category.Name,
                stock.CurrentStock,
                MinimumStock = stock.Warehouse == null ? product.MinimumStock :
                    product.BranchSettings.Where(b => b.BranchId == stock.Warehouse.BranchId)
                        .Select(b => b.MinimumStock).FirstOrDefault() ?? product.MinimumStock
            })
            .OrderBy(x => x.ProductName)
            .ToListAsync(cancellationToken);

        var stocks = stocksData
            .Select(x => new InventoryStockReportRow(
                x.ProductId,
                x.InternalCode,
                x.ProductName,
                x.CategoryName,
                x.CurrentStock,
                x.MinimumStock))
            .ToList();

        var movements = await MovementsInScope()
            .OrderByDescending(x => x.OccurredAtUtc)
            .Select(x => new InventoryMovementReportRow(
                x.OccurredAtUtc,
                x.Product.VariantValue == null ? x.Product.Name : x.Product.Name + " " + x.Product.VariantValue,
                x.Type,
                x.Quantity,
                x.PreviousStock,
                x.NewStock,
                x.UserName,
                x.SourceDocument))
            .ToListAsync(cancellationToken);

        return new InventoryReport(
            stocks,
            stocks.Where(x => x.CurrentStock <= x.MinimumStock).ToList(),
            stocks.Where(x => x.CurrentStock <= 0).ToList(),
            movements.Where(x => x.Type is InventoryMovementType.PurchaseEntry or InventoryMovementType.ReturnEntry or InventoryMovementType.PositiveAdjustment).ToList(),
            movements.Where(x => x.Type is InventoryMovementType.SaleExit or InventoryMovementType.NegativeAdjustment or InventoryMovementType.PurchaseReversal).ToList(),
            movements.Where(x => x.Type is InventoryMovementType.PositiveAdjustment or InventoryMovementType.NegativeAdjustment).ToList(),
            movements);
    }

    public async Task<ProductReport> GetProductReportAsync(CancellationToken cancellationToken = default)
    {
        var soldProducts = await dbContext.SaleItems
            .AsNoTracking().Where(x => SalesInScope().Select(s => s.Id).Contains(x.SaleId))
            .GroupBy(x => new { x.ProductId, x.ProductName })
            .Select(x => new ProductReportRow(x.Key.ProductId, string.Empty, x.Key.ProductName, x.Sum(y => y.Quantity), "Ventas"))
            .ToListAsync(cancellationToken);

        var products = await dbContext.Products
            .AsNoTracking()
            .Where(x => !x.ManagesVariants)
            .Select(x => new { x.Id, x.InternalCode, x.Name, x.VariantValue })
            .ToListAsync(cancellationToken);

        var withCodes = soldProducts
            .Select(x =>
            {
                var product = products.FirstOrDefault(p => p.Id == x.ProductId);
                return x with { InternalCode = product?.InternalCode ?? string.Empty };
            })
            .ToList();

        var movementProductIds = await MovementsInScope()
            .Select(x => x.ProductId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new ProductReport(
            withCodes.OrderByDescending(x => x.Quantity).Take(10).ToList(),
            withCodes.OrderBy(x => x.Quantity).Take(10).ToList(),
            products
                .Where(x => !movementProductIds.Contains(x.Id))
                .Select(x => new ProductReportRow(x.Id, x.InternalCode,
                    x.VariantValue == null ? x.Name : x.Name + " " + x.VariantValue,
                    0m, "Sin movimiento"))
                .ToList());
    }

    public async Task<IReadOnlyList<PurchaseReportRow>> GetPurchaseReportAsync(PurchaseReportFilter filter, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Purchases
            .AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.Items)
            .Where(x => context == null || (context.CurrentBranchId.HasValue && x.BranchId == context.CurrentBranchId))
            .AsQueryable();

        if (filter.FromUtc.HasValue)
        {
            query = query.Where(x => x.PurchaseDateUtc >= filter.FromUtc.Value);
        }

        if (filter.ToUtc.HasValue)
        {
            query = query.Where(x => x.PurchaseDateUtc <= filter.ToUtc.Value);
        }

        if (filter.SupplierId.HasValue)
        {
            query = query.Where(x => x.SupplierId == filter.SupplierId.Value);
        }

        if (filter.ProductId.HasValue)
        {
            query = query.Where(x => x.Items.Any(i => i.ProductId == filter.ProductId.Value));
        }

        return await query
            .OrderByDescending(x => x.PurchaseDateUtc)
            .Select(x => new PurchaseReportRow(
                x.Id,
                x.Number,
                x.PurchaseDateUtc,
                x.Supplier.TradeName ?? x.Supplier.LegalName,
                x.UserName,
                x.Status,
                x.Total))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CashReportRow>> GetCashReportAsync(CancellationToken cancellationToken = default) =>
        await dbContext.CashSessions
            .AsNoTracking().Where(x => context == null || (context.CurrentBranchId.HasValue && x.BranchId == context.CurrentBranchId))
            .OrderByDescending(x => x.OpenedAtUtc)
            .Select(x => new CashReportRow(
                x.Id,
                x.RegisterCode,
                x.CashierUserName,
                x.OpenedAtUtc,
                x.ClosedAtUtc,
                x.OpeningAmount,
                x.TotalSold,
                x.TotalCashSales,
                x.TotalTransferSales,
                x.TotalPosSales,
                x.OtherIncomeTotal,
                x.ExpenseTotal,
                x.TotalCashRefunds,
                x.TotalTransferRefunds,
                x.TotalPosRefunds,
                x.ExpectedCash,
                x.Difference))
            .ToListAsync(cancellationToken);

    public async Task<DashboardSummary> GetDashboardSummaryAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var dayStartUtc = new DateTimeOffset(now.Date, now.Offset).ToUniversalTime();
        var nowUtc = now.ToUniversalTime();

        var saleTotals = await (
            from sale in SalesInScope()
            join cashSession in dbContext.CashSessions.AsNoTracking() on sale.CashSessionId equals cashSession.Id
            where sale.SoldAtUtc >= dayStartUtc
                && sale.SoldAtUtc <= nowUtc
                && sale.Status != SaleStatus.Cancelled
            select new { cashSession.Id, cashSession.RegisterCode, sale.Total })
            .ToListAsync(cancellationToken);

        var refundTotals = await (
            from refund in dbContext.SaleRefunds.AsNoTracking()
            join cashSession in dbContext.CashSessions.AsNoTracking() on refund.CashSessionId equals cashSession.Id
            where refund.OccurredAtUtc >= dayStartUtc && refund.OccurredAtUtc <= nowUtc
                && (context == null || (context.CurrentBranchId.HasValue && cashSession.BranchId == context.CurrentBranchId))
            select new { cashSession.Id, cashSession.RegisterCode, refund.Amount })
            .ToListAsync(cancellationToken);

        var registerCodes = saleTotals.Select(x => x.RegisterCode)
            .Concat(refundTotals.Select(x => x.RegisterCode))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
        var salesByRegister = registerCodes.Select(registerCode => new RegisterSalesRow(
            registerCode,
            saleTotals.Where(x => x.RegisterCode == registerCode).Sum(x => x.Total)
                - refundTotals.Where(x => x.RegisterCode == registerCode).Sum(x => x.Amount)))
            .ToList();

        var stocks = await (
            from product in dbContext.Products.AsNoTracking()
            join stock in StocksInScope() on product.Id equals stock.ProductId
            join category in dbContext.Categories.AsNoTracking() on product.CategoryId equals category.Id
            where product.IsActive && !product.ManagesVariants
            orderby product.Name, product.VariantValue
            select new InventoryStockReportRow(
                product.Id,
                product.InternalCode,
                product.VariantValue == null ? product.Name : product.Name + " " + product.VariantValue,
                category.Name,
                stock.CurrentStock,
                stock.Warehouse == null ? product.MinimumStock :
                    product.BranchSettings.Where(b => b.BranchId == stock.Warehouse.BranchId)
                        .Select(b => b.MinimumStock).FirstOrDefault() ?? product.MinimumStock))
            .ToListAsync(cancellationToken);

        var pendingOrders = await dbContext.PurchaseOrders
            .AsNoTracking()
            .Where(x => context == null || (context.CurrentBranchId.HasValue && x.BranchId == context.CurrentBranchId))
            .Where(x => x.Status == PurchaseOrderStatus.Requested || x.Status == PurchaseOrderStatus.PartiallyReceived)
            .OrderBy(x => x.RequestedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.Number,
                SupplierName = x.Supplier.TradeName ?? x.Supplier.LegalName,
                x.RequestedAtUtc,
                x.Status
            })
            .ToListAsync(cancellationToken);

        var productData = await (
            from product in dbContext.Products.AsNoTracking()
            join stock in StocksInScope() on product.Id equals stock.ProductId into productStocks
            from stock in productStocks.DefaultIfEmpty()
            where product.IsActive && !product.ManagesVariants
            select new
            {
                product.Id,
                product.InternalCode,
                ProductName = product.VariantValue == null ? product.Name : product.Name + " " + product.VariantValue,
                CurrentStock = stock == null ? 0m : stock.CurrentStock,
                product.CreatedAtUtc,
                product.NoMovementDaysOverride
            })
            .ToListAsync(cancellationToken);
        var lastSales = await (
            from item in dbContext.SaleItems.AsNoTracking()
            join sale in SalesInScope() on item.SaleId equals sale.Id
            where sale.Status != SaleStatus.Cancelled
            group sale by item.ProductId into sales
            select new { ProductId = sales.Key, LastSaleAtUtc = sales.Max(x => x.SoldAtUtc) })
            .ToDictionaryAsync(x => x.ProductId, x => x.LastSaleAtUtc, cancellationToken);
        var initialEntries = await MovementsInScope()
            .Where(x => x.Type == InventoryMovementType.PurchaseEntry || x.Type == InventoryMovementType.PositiveAdjustment)
            .GroupBy(x => x.ProductId)
            .Select(x => new { ProductId = x.Key, FirstEntryAtUtc = x.Min(y => y.OccurredAtUtc) })
            .ToDictionaryAsync(x => x.ProductId, x => x.FirstEntryAtUtc, cancellationToken);

        var productsWithoutMovement = productData.Where(product => product.NoMovementDaysOverride.HasValue).Select(product =>
            {
                var hasSale = lastSales.TryGetValue(product.Id, out var lastSaleAtUtc);
                var referenceDate = hasSale
                    ? lastSaleAtUtc
                    : initialEntries.TryGetValue(product.Id, out var firstEntryAtUtc)
                        ? firstEntryAtUtc
                        : product.CreatedAtUtc;
                var days = Math.Max(0, (int)Math.Floor((nowUtc - referenceDate).TotalDays));
                var configuredDays = product.NoMovementDaysOverride!.Value;
                return new NoMovementProductRow(
                    product.Id,
                    product.InternalCode,
                    product.ProductName,
                    product.CurrentStock,
                    hasSale ? lastSaleAtUtc : null,
                    days,
                    configuredDays);
            })
            .Where(x => x.DaysWithoutMovement >= x.ConfiguredDays)
            .OrderByDescending(x => x.DaysWithoutMovement)
            .ThenBy(x => x.ProductName)
            .ToList();

        var currencySymbol = await dbContext.SystemSettings.AsNoTracking()
            .Select(x => x.CurrencySymbol)
            .FirstOrDefaultAsync(cancellationToken) ?? "Q";

        return new DashboardSummary(
            now,
            currencySymbol,
            saleTotals.Sum(x => x.Total) - refundTotals.Sum(x => x.Amount),
            salesByRegister,
            stocks.Where(x => x.CurrentStock > 0 && x.CurrentStock <= x.MinimumStock).ToList(),
            stocks.Where(x => x.CurrentStock == 0).ToList(),
            pendingOrders.Select(x => new PendingPurchaseOrderRow(
                x.Id,
                x.Number,
                x.SupplierName,
                x.RequestedAtUtc,
                Math.Max(0, (now.Date - x.RequestedAtUtc.ToOffset(now.Offset).Date).Days),
                x.Status)).ToList(),
            productsWithoutMovement);
    }
}
