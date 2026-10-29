using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Exports;
using SistemiPos.Application.Reports;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Sales;
using SistemiPos.Domain.Purchases;
using SistemiPos.Domain.Restaurant;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Reports;

public sealed class ControlReportService(AppDbContext db, IOperationContextAccessor context,
    ICurrentUserAccessor user, IOperationAuthorizationService auth, IDateTimeProvider clock,
    IExcelExportService excel, ITablePdfWriter pdf) : IControlReportService
{
    private const int Size = 100;
    private async Task Demand(string permission, CancellationToken ct)
    {
        if (!user.UserId.HasValue || !await auth.HasPermissionAsync(user.UserName, permission, ct))
            throw new DomainRuleException("No tienes permiso para consultar esta información.");
    }
    public async Task<ControlReport> GetAsync(ControlQuery query, CancellationToken ct = default)
    {
        await Demand(PermissionCodes.ReportsView, ct);
        if (query.Report == "audit") await Demand(PermissionCodes.OperationsAuditView, ct);
        var today = DateOnly.FromDateTime(clock.UtcNow.ToOffset(TimeSpan.FromHours(-6)).Date);
        query = query with { From = query.From ?? new DateOnly(today.Year, today.Month, 1), To = query.To ?? today, Page = Math.Clamp(query.Page, 1, 1000000) };
        if (query.To < query.From || query.To.Value.DayNumber - query.From.Value.DayNumber > 366)
            throw new DomainRuleException("Selecciona un rango válido de hasta un año.");
        var authorized = (await context.GetAuthorizedBranchesAsync(ct)).Where(x => x.IsActive).ToList();
        var consolidate = await auth.HasPermissionAsync(user.UserName, PermissionCodes.ReportsConsolidate, ct);
        var branches = authorized.Where(x => (!query.CompanyId.HasValue || x.CompanyId == query.CompanyId)
            && (!query.BranchId.HasValue || x.Id == query.BranchId)).ToList();
        if (query.BranchId.HasValue && branches.Count == 0) throw new DomainRuleException("La sucursal no pertenece a tu alcance.");
        if (query.CompanyId.HasValue && branches.Count == 0) throw new DomainRuleException("La empresa no pertenece a tu alcance.");
        if (!consolidate)
        {
            if (query.BranchId.HasValue && query.BranchId != context.CurrentBranchId)
                throw new DomainRuleException("La consolidación de otras sucursales requiere autorización.");
            branches = branches.Where(x => x.Id == context.CurrentBranchId).ToList();
        }
        if (branches.Count == 0) throw new DomainRuleException("Selecciona una sucursal autorizada.");
        var ids = branches.Select(x => x.Id).ToArray();
        var start = new DateTimeOffset(query.From.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(-6)).ToUniversalTime();
        var end = new DateTimeOffset(query.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(-6)).ToUniversalTime();
        var warehouses = db.Warehouses.AsNoTracking().Where(x => ids.Contains(x.BranchId));
        if (query.WarehouseId.HasValue)
        {
            warehouses = warehouses.Where(x => x.Id == query.WarehouseId);
            if (!await warehouses.AnyAsync(ct)) throw new DomainRuleException("Bodega fuera del alcance seleccionado.");
        }
        var warehouseIds = warehouses.Select(x => x.Id);
        var cash = db.CashSessions.AsNoTracking().Where(x => x.BranchId.HasValue && ids.Contains(x.BranchId.Value));
        if (!string.IsNullOrWhiteSpace(query.Register)) cash = cash.Where(x => x.RegisterCode == query.Register.Trim());
        var sales = db.Sales.AsNoTracking().Where(x => cash.Select(c => c.Id).Contains(x.CashSessionId)
            && x.SoldAtUtc >= start && x.SoldAtUtc < end);
        if (!string.IsNullOrWhiteSpace(query.User)) sales = sales.Where(x => x.CashierUserName == query.User.Trim());
        if (query.WarehouseId.HasValue) sales = sales.Where(x => x.WarehouseId == query.WarehouseId);
        if (query.ProductId.HasValue) sales = sales.Where(x => x.Items.Any(i => i.ProductId == query.ProductId));
        var rows = new List<ControlRow>(); var metrics = new List<ControlMetric>(); var alerts = new List<ControlAlert>();
        var columns = new List<ExcelColumn>(); var title = ""; string? note = null; int count = 0;
        var skip = (query.Page - 1) * Size;
        string AlertUrl(string report, bool onlyOutOfStock = false) => "/control/reportes?report="+report
            + (query.CompanyId.HasValue ? "&companyId="+query.CompanyId : "")
            + (query.BranchId.HasValue ? "&branchId="+query.BranchId : "")
            + (query.WarehouseId.HasValue ? "&warehouseId="+query.WarehouseId : "")
            + (onlyOutOfStock ? "&outOfStock=true" : "");
        string Branch(Guid? id) => branches.FirstOrDefault(x => x.Id == id)?.Name ?? "Histórico";
        switch (query.Report)
        {
            case "sales":
                title = "Ventas y cobros";
                columns = [new("Documento"), new("Fecha"), new("Sucursal"), new("Caja"), new("Usuario"), new("Moneda"),
                    new("Estado"), new("Total", ExcelCellType.Decimal), new("Reembolsos vinculados", ExcelCellType.Decimal)];
                count = await sales.CountAsync(ct);
                var data = await (from s in sales join c in cash on s.CashSessionId equals c.Id
                    orderby s.SoldAtUtc descending, s.Id
                    select new { Sale = s, c.BranchId, c.RegisterCode,
                        Refund = db.SaleRefunds.Where(r => r.SaleId == s.Id).Sum(r => (decimal?)r.Amount) ?? 0 })
                    .Skip(skip).Take(Size).ToListAsync(ct);
                foreach (var x in data) rows.Add(new([x.Sale.Number, Local(x.Sale.SoldAtUtc), Branch(x.BranchId),
                    x.RegisterCode, x.Sale.CashierUserName, x.Sale.CurrencyCode, Status(x.Sale.Status), x.Sale.Total, x.Refund], "/Sales/Details/" + x.Sale.Id));
                var amounts = await sales.Where(s => s.Status != SaleStatus.Cancelled).GroupBy(s => s.CurrencyCode)
                    .Select(g => new { Currency = g.Key, Total = g.Sum(x => x.Total), Tax = g.Sum(x => x.TaxTotal) }).ToListAsync(ct);
                foreach (var x in amounts) { metrics.Add(new("Importe de ventas sin anuladas", x.Total, x.Currency)); metrics.Add(new("Impuesto original de ventas", x.Tax, x.Currency)); }
                var paymentTotals = await (from p in db.SalePayments
                    join s in sales.Where(s=>s.Status!=SaleStatus.Cancelled) on p.SaleId equals s.Id
                    group p by new { p.PaymentMethod, Currency=s.CurrencyCode } into g
                    select new { g.Key.Currency,g.Key.PaymentMethod,Amount=g.Sum(p=>p.TotalAmount) }).ToListAsync(ct);
                foreach (var x in paymentTotals) metrics.Add(new("Cobro original " + Label(x.PaymentMethod.ToString()), x.Amount, x.Currency));
                note = "Los reembolsos son los vinculados a las ventas de este rango, incluso si se procesaron después. Los pagos mixtos se desglosan por su medio original; las monedas no se suman entre sí.";
                break;
            case "purchases":
                title = "Compras por ubicación";
                columns = [new("Documento"), new("Fecha"), new("Sucursal"), new("Proveedor"), new("Factura"), new("Estado"), new("Total", ExcelCellType.Decimal)];
                var purchases = db.Purchases.AsNoTracking().Where(p => p.BranchId.HasValue && ids.Contains(p.BranchId.Value)
                    && p.PurchaseDateUtc >= start && p.PurchaseDateUtc < end && p.Status != DocumentStatus.Draft);
                if (query.WarehouseId.HasValue) purchases = purchases.Where(p => p.WarehouseId == query.WarehouseId);
                if (query.SupplierId.HasValue) purchases = purchases.Where(p => p.SupplierId == query.SupplierId);
                if (query.ProductId.HasValue) purchases = purchases.Where(p => p.Items.Any(i => i.ProductId == query.ProductId));
                if (!string.IsNullOrWhiteSpace(query.User)) purchases = purchases.Where(p => p.UserName == query.User.Trim());
                count = await purchases.CountAsync(ct);
                var purchaseData = await purchases.Include(x => x.Supplier).OrderByDescending(x => x.PurchaseDateUtc).ThenBy(x => x.Id).Skip(skip).Take(Size).ToListAsync(ct);
                foreach (var x in purchaseData) rows.Add(new([x.Number, Local(x.PurchaseDateUtc), Branch(x.BranchId), x.Supplier.LegalName, x.InvoiceNumber, Label(x.Status.ToString()), x.Total], "/Purchases/Details/" + x.Id));
                var pending = db.PurchaseOrders.AsNoTracking().Where(x => x.BranchId.HasValue && ids.Contains(x.BranchId.Value)
                    && (x.Status == PurchaseOrderStatus.Requested || x.Status == PurchaseOrderStatus.PartiallyReceived));
                alerts.Add(new("Órdenes pendientes de recepción", await pending.CountAsync(ct), AlertUrl("purchase-orders")));
                note = "Incluye compras físicas confirmadas y anuladas; no duplica órdenes de compra con sus recepciones. Los importes de compras no se consolidan como una moneda común.";
                break;
            case "purchase-orders":
                title = "Órdenes de compra pendientes";
                columns=[new("Documento"),new("Sucursal"),new("Proveedor"),new("Fecha de solicitud"),new("Estado"),new("Líneas pendientes")];
                var openOrders=db.PurchaseOrders.AsNoTracking().Where(x=>x.BranchId.HasValue&&ids.Contains(x.BranchId.Value)
                    && (x.Status==PurchaseOrderStatus.Requested||x.Status==PurchaseOrderStatus.PartiallyReceived));
                if(query.SupplierId.HasValue)openOrders=openOrders.Where(x=>x.SupplierId==query.SupplierId);
                count=await openOrders.CountAsync(ct);
                foreach(var x in await openOrders.Include(x=>x.Supplier).Include(x=>x.Items).OrderBy(x=>x.RequestedAtUtc).ThenBy(x=>x.Id).Skip(skip).Take(Size).ToListAsync(ct))
                    rows.Add(new([x.Number,Branch(x.BranchId),x.Supplier.LegalName,Local(x.RequestedAtUtc),Label(x.Status.ToString()),
                        x.Items.Count(i=>i.PendingQuantity>0)],"/Purchases/OrderDetails/"+x.Id));
                note="Pendientes actuales, incluidos los solicitados antes del rango. No se suman como compras hasta recibirlos.";
                break;
            case "purchase-costs":
                title="Costos de compras por producto";
                columns=[new("Compra"),new("Proveedor"),new("Producto"),new("Cantidad base",ExcelCellType.Decimal),new("Costo unitario",ExcelCellType.Decimal),new("Subtotal",ExcelCellType.Decimal)];
                var costs=from purchase in db.Purchases.AsNoTracking() join item in db.PurchaseItems on purchase.Id equals item.PurchaseId
                    join product in db.Products on item.ProductId equals product.Id
                    where purchase.BranchId.HasValue&&ids.Contains(purchase.BranchId.Value)&&purchase.Status==DocumentStatus.Confirmed
                        && purchase.PurchaseDateUtc>=start&&purchase.PurchaseDateUtc<end
                        && (!query.SupplierId.HasValue||purchase.SupplierId==query.SupplierId)
                        && (!query.ProductId.HasValue||item.ProductId==query.ProductId)
                        && (!query.WarehouseId.HasValue||purchase.WarehouseId==query.WarehouseId)
                    select new { purchase,item,product.Name,Supplier=purchase.Supplier.LegalName };
                count=await costs.CountAsync(ct);
                foreach(var x in await costs.OrderByDescending(x=>x.purchase.PurchaseDateUtc).ThenBy(x=>x.item.Id).Skip(skip).Take(Size).ToListAsync(ct))
                    rows.Add(new([x.purchase.Number,x.Supplier,x.Name,x.item.Quantity,x.item.UnitCost,x.item.Subtotal],"/Purchases/Details/"+x.purchase.Id));
                note="Costos históricos de recepciones confirmadas. Las anuladas se consultan en Compras y no forman este costo de entrada.";
                break;
            case "inventory":
                title = "Existencias por bodega";
                columns = [new("Producto"), new("Bodega"), new("Sucursal"), new("Unidad"), new("Físico", ExcelCellType.Decimal), new("Reservado", ExcelCellType.Decimal), new("Disponible", ExcelCellType.Decimal)];
                var stocks = db.InventoryStocks.AsNoTracking().Where(x => x.WarehouseId.HasValue && warehouseIds.Contains(x.WarehouseId.Value));
                if (query.OutOfStock) stocks=stocks.Where(x=>x.CurrentStock<=0);
                if (query.ProductId.HasValue) stocks = stocks.Where(x => x.ProductId == query.ProductId);
                count = await stocks.CountAsync(ct);
                var stockData = await stocks.Include(x => x.Product).Include(x => x.Warehouse).OrderBy(x => x.Product.Name).ThenBy(x => x.Id).Skip(skip).Take(Size).ToListAsync(ct);
                foreach (var x in stockData) rows.Add(new([x.Product.DisplayName, x.Warehouse!.Name, Branch(x.Warehouse.BranchId), x.Product.UnitOfMeasure,
                    x.CurrentStock, x.ReservedQuantity, x.CurrentStock - x.ReservedQuantity], "/Inventory"));
                note = "Saldos actuales, no saldos históricos al cierre de la fecha. Nunca se mezclan gramos, mililitros y unidades en un total de cantidad.";
                break;
            case "movements":
                title = "Kardex y trazabilidad";
                columns = [new("Fecha"), new("Producto"), new("Bodega"), new("Movimiento"), new("Cantidad", ExcelCellType.Decimal), new("Anterior", ExcelCellType.Decimal), new("Nuevo", ExcelCellType.Decimal), new("Origen"), new("Usuario")];
                var movements = db.InventoryMovements.AsNoTracking().Where(x => x.WarehouseId.HasValue && warehouseIds.Contains(x.WarehouseId.Value)
                    && x.OccurredAtUtc >= start && x.OccurredAtUtc < end);
                if (query.ProductId.HasValue) movements = movements.Where(x => x.ProductId == query.ProductId);
                if (!string.IsNullOrWhiteSpace(query.User)) movements = movements.Where(x => x.UserName == query.User.Trim());
                count = await movements.CountAsync(ct);
                var moves = await movements.Include(x => x.Product).Include(x => x.Warehouse).OrderByDescending(x => x.OccurredAtUtc).ThenBy(x => x.Id).Skip(skip).Take(Size).ToListAsync(ct);
                foreach (var x in moves) rows.Add(new([Local(x.OccurredAtUtc), x.Product.DisplayName, x.Warehouse!.Name, Label(x.Type.ToString()),
                    x.Quantity, x.PreviousStock, x.NewStock, x.SourceDocument, x.UserName], "/Inventory/Kardex?warehouseId=" + x.WarehouseId));
                break;
            case "profit":
                title = "Rentabilidad histórica por línea";
                columns = [new("Venta"), new("Producto histórico"), new("Moneda"), new("Cantidad", ExcelCellType.Decimal), new("Venta sin impuesto ajustada", ExcelCellType.Decimal),
                    new("Costo histórico neto físico", ExcelCellType.Decimal), new("Margen bruto", ExcelCellType.Decimal),new("Origen del costo")];
                var items = from s in sales.Where(x => x.Status != SaleStatus.Cancelled)
                    join i in db.SaleItems.AsNoTracking() on s.Id equals i.SaleId
                    where !query.ProductId.HasValue || i.ProductId == query.ProductId select new { Sale = s, Item = i };
                count = await items.CountAsync(ct);
                var itemData = await items.OrderByDescending(x => x.Sale.SoldAtUtc).ThenBy(x => x.Item.Id).Skip(skip).Take(Size).ToListAsync(ct);
                var saleIds = itemData.Select(x => x.Sale.Id).Distinct().ToArray();
                var refunds = await db.SaleRefunds.Where(r => saleIds.Contains(r.SaleId)).GroupBy(r => r.SaleId).Select(g => new { Id = g.Key, Amount = g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.Id, x => x.Amount, ct);
                var restored = await (from ri in db.SaleReturnItems join ret in db.SaleReturns on ri.SaleReturnId equals ret.Id
                    where saleIds.Contains(ret.SaleId) && ri.ReturnedToInventory select new { ret.SaleId, ri.ProductId, ri.Quantity }).ToListAsync(ct);
                var quantities = await db.SaleItems.Where(i => saleIds.Contains(i.SaleId)).GroupBy(i => new { i.SaleId, i.ProductId })
                    .Select(g => new { g.Key.SaleId, g.Key.ProductId, Quantity = g.Sum(x => x.Quantity) }).ToListAsync(ct);
                foreach (var x in itemData)
                {
                    var totalQuantity = quantities.First(q => q.SaleId == x.Sale.Id && q.ProductId == x.Item.ProductId).Quantity;
                    var costKnown = x.Item.ConsumedUnitCostAtSale.HasValue || (x.Item.RecipeJson is null && x.Item.CostAtSale > 0);
                    var profit = HistoricalProfitCalculator.Calculate(x.Item.TaxableBaseAmount,x.Item.ConsumedUnitCostAtSale??x.Item.CostAtSale,x.Item.Quantity,
                        x.Sale.Total,refunds.GetValueOrDefault(x.Sale.Id),totalQuantity,
                        restored.Where(r => r.SaleId == x.Sale.Id && r.ProductId == x.Item.ProductId).Sum(r => r.Quantity));
                    rows.Add(new([x.Sale.Number, x.Item.ProductName, x.Sale.CurrencyCode, x.Item.Quantity,
                        profit.RevenueWithoutTax,costKnown?profit.Cost:null,costKnown?profit.GrossMargin:null,
                        x.Item.ConsumedUnitCostAtSale.HasValue?"Consumo documentado":costKnown?"Catálogo al vender":"Consumo histórico no documentado"], "/Sales/Details/" + x.Sale.Id));
                }
                note = "Usa costo capturado al vender, nunca la receta o costo actual. Un empaque agregado después del cobro permanece en Kardex y no reescribe esa venta. Reembolsos se prorratean sobre la venta; el costo solo disminuye por devolución física registrada. Un costo cero histórico debe revisarse y no se interpreta como costo conocido. No es utilidad contable neta.";
                break;
            case "waste":
                title = "Mermas de restaurante";
                columns = [new("Fecha"), new("Pedido"), new("Cantidad", ExcelCellType.Decimal), new("Costo histórico", ExcelCellType.Decimal), new("Motivo"), new("Usuario")];
                var wasteQuery = from w in db.OrderWaste.AsNoTracking() join o in db.RestaurantOrders on w.OrderId equals o.Id
                    where ids.Contains(o.BranchId) && w.AtUtc >= start && w.AtUtc < end
                        && (!query.WarehouseId.HasValue || o.WarehouseId == query.WarehouseId)
                    select new { w, o.Number };
                count = await wasteQuery.CountAsync(ct);
                foreach(var x in await wasteQuery.OrderByDescending(x=>x.w.AtUtc).ThenBy(x=>x.w.Id).Skip(skip).Take(Size).ToListAsync(ct))
                    rows.Add(new([Local(x.w.AtUtc),x.Number,x.w.Quantity,x.w.Cost,x.w.Reason,x.w.Actor],"/restaurante/"+x.w.OrderId));
                note = "La merma documenta un consumo ya registrado. Este reporte no descuenta inventario ni suma cantidades de productos de unidades distintas.";
                break;
            case "production":
                title = "Rendimiento de producción";
                columns = [new("Documento"),new("Lote"),new("Fecha"),new("Teórico",ExcelCellType.Decimal),new("Real",ExcelCellType.Decimal),new("Merma",ExcelCellType.Decimal),new("Costo histórico",ExcelCellType.Decimal),new("Motivo")];
                var production = db.ProductionOrders.AsNoTracking().Where(x=>warehouseIds.Contains(x.OriginWarehouseId)
                    && x.ConfirmedAtUtc>=start && x.ConfirmedAtUtc<end);
                count = await production.CountAsync(ct);
                foreach(var x in await production.OrderByDescending(x=>x.ConfirmedAtUtc).ThenBy(x=>x.Id).Skip(skip).Take(Size).ToListAsync(ct))
                    rows.Add(new([x.Number,x.LotNumber,Local(x.ConfirmedAtUtc!.Value),x.PlannedQuantity,x.ActualQuantity,x.WasteQuantity,x.TotalCost,x.VarianceReason],"/Production/Details/"+x.Id));
                note="Compara rendimiento teórico y real del lote. Los costos son los capturados al confirmar la producción.";
                break;
            case "orders":
                title = "Pedidos de restaurante pendientes";
                columns = [new("Pedido"),new("Sucursal"),new("Creado"),new("Estado"),new("Canal"),new("Servicio")];
                var orders = db.RestaurantOrders.AsNoTracking().Where(x=>ids.Contains(x.BranchId) && (!query.WarehouseId.HasValue||x.WarehouseId==query.WarehouseId)
                    && x.Status!=RestaurantOrderStatus.Delivered && x.Status!=RestaurantOrderStatus.Cancelled);
                count = await orders.CountAsync(ct);
                foreach(var x in await orders.OrderBy(x=>x.CreatedAtUtc).ThenBy(x=>x.Id).Skip(skip).Take(Size).ToListAsync(ct))
                    rows.Add(new([x.Number,Branch(x.BranchId),Local(x.CreatedAtUtc),Label(x.Status.ToString()),x.ChannelName,Label(x.ServiceType.ToString())],"/restaurante/"+x.Id));
                note="Cola activa actual; incluye pedidos abiertos antes del rango de fechas. El flujo POS independiente sigue disponible en Pedidos.";
                break;
            case "counts":
                title="Diferencias de conteo físico";
                columns=[new("Toma"),new("Producto"),new("Unidad"),new("Fecha"),new("Teórico de referencia",ExcelCellType.Decimal),new("Contado",ExcelCellType.Decimal),new("Diferencia",ExcelCellType.Decimal),new("Ajuste aplicado",ExcelCellType.Decimal),new("Usuario")];
                var counts=from c in db.PhysicalInventoryCounts.AsNoTracking()
                    join i in db.PhysicalInventoryCountItems on c.Id equals i.PhysicalInventoryCountId
                    where c.WarehouseId.HasValue&&warehouseIds.Contains(c.WarehouseId.Value)
                        && i.CountedAtUtc>=start&&i.CountedAtUtc<end
                        && (!query.ProductId.HasValue||i.ProductId==query.ProductId)
                        && (query.User==null||query.User==""||i.CountedBy==query.User)
                    select new { c,i,Product=i.Product.Name,Unit=i.Product.UnitOfMeasure };
                count=await counts.CountAsync(ct);
                foreach(var x in await counts.OrderByDescending(x=>x.i.CountedAtUtc).ThenBy(x=>x.i.Id).Skip(skip).Take(Size).ToListAsync(ct))
                    rows.Add(new([x.c.Name,x.Product,x.Unit,Local(x.i.CountedAtUtc!.Value),x.i.ReferenceStock,x.i.CountedStock,x.i.Difference,x.i.AppliedAdjustment,x.i.CountedBy],"/PhysicalInventoryCounts?id="+x.c.Id));
                note="La diferencia compara el conteo con su referencia histórica; el ajuste aplicado se muestra aparte para conservar movimientos ocurridos durante la toma.";
                break;
            case "pos-orders":
                title="Seguimiento de líneas del POS";
                columns=[new("Pedido"),new("Sucursal"),new("Fecha"),new("Producto"),new("Cantidad"),new("En preparación"),new("Preparado"),new("Despachado"),new("Cobrado"),new("Cancelado después de preparar")];
                var posLines=from o in db.Set<SistemiPos.Domain.Orders.RestaurantOrder>().AsNoTracking()
                    join l in db.Set<SistemiPos.Domain.Orders.RestaurantOrderLine>() on o.Id equals l.OrderId
                    where ids.Contains(o.BranchId)&&o.CreatedAtUtc>=start&&o.CreatedAtUtc<end
                        && (!query.WarehouseId.HasValue||o.WarehouseId==query.WarehouseId)
                        && (!query.ProductId.HasValue||l.ProductId==query.ProductId)
                    select new {o,l};
                count=await posLines.CountAsync(ct);
                foreach(var x in await posLines.OrderByDescending(x=>x.o.CreatedAtUtc).ThenBy(x=>x.l.Id).Skip(skip).Take(Size).ToListAsync(ct))
                    rows.Add(new([x.o.Id,Branch(x.o.BranchId),Local(x.o.CreatedAtUtc),x.l.ProductName,x.l.Quantity,x.l.PreparingQuantity,x.l.PreparedQuantity,x.l.DispatchedQuantity,x.l.SoldQuantity,x.l.CancelledAfterPreparationQuantity],"/pedidos/"+x.o.Id));
                note="Pedidos independientes del POS por fecha de creación. Las cancelaciones después de preparar conservan el consumo; no se mezclan con los pedidos del módulo Restaurante.";
                break;
            case "audit":
                title = "Auditoría de operaciones";
                columns = [new("Fecha"), new("Sucursal"), new("Usuario"), new("Módulo"), new("Acción"), new("Entidad"), new("Documento"), new("Terminal"), new("Cambios")];
                var audit = db.AuditLogs.AsNoTracking().Where(x => x.BranchId.HasValue && ids.Contains(x.BranchId.Value)
                    && x.OccurredAtUtc >= start && x.OccurredAtUtc < end);
                if (!string.IsNullOrWhiteSpace(query.User)) audit = audit.Where(x => x.UserName == query.User.Trim());
                count = await audit.CountAsync(ct);
                var events = await audit.OrderByDescending(x => x.OccurredAtUtc).ThenBy(x => x.Id).Skip(skip).Take(Size).ToListAsync(ct);
                foreach (var x in events) rows.Add(new([Local(x.OccurredAtUtc), Branch(x.BranchId), x.UserName, x.Module, x.Action, x.RecordType,
                    x.DocumentNumber ?? x.RecordId, x.TerminalId, x.ChangesJson]));
                note = "Los históricos sin ubicación no se asignan artificialmente a una sucursal. Las credenciales y secretos de nuevas auditorías están protegidos.";
                break;
            default: throw new DomainRuleException("Selecciona un reporte válido.");
        }
        if (query.Report != "audit")
        {
            alerts.Add(new("Pedidos de restaurante abiertos hace más de 30 minutos",
                await db.RestaurantOrders.CountAsync(x => ids.Contains(x.BranchId) && x.Status != RestaurantOrderStatus.Delivered
                    && x.Status != RestaurantOrderStatus.Cancelled && (!query.WarehouseId.HasValue||x.WarehouseId==query.WarehouseId) && x.CreatedAtUtc < clock.UtcNow.AddMinutes(-30), ct), AlertUrl("orders")));
            alerts.Add(new("Productos agotados",
                await db.InventoryStocks.CountAsync(x => x.WarehouseId.HasValue && warehouseIds.Contains(x.WarehouseId.Value) && x.CurrentStock <= 0, ct), AlertUrl("inventory",true)));
        }
        var scope = string.Join(", ",branches.Select(x=>x.CompanyTradeName+" / "+x.Name));
        return new(query, title, authorized, columns, rows, count, Size, metrics, alerts, clock.UtcNow, note,
            await db.Warehouses.AsNoTracking().Where(x=>ids.Contains(x.BranchId)).OrderBy(x=>x.Name).Select(x=>new ControlWarehouse(x.Id,x.Name)).ToListAsync(ct),scope);
    }
    public async Task<byte[]> ExportExcelAsync(ControlQuery query, CancellationToken ct = default)
    {
        await Demand(PermissionCodes.ReportsExport, ct);
        var report = await GetAsync(query, ct);
        return excel.CreateWorkbook("Control", report.Columns, report.Rows.Select(x => x.Cells).ToList(),
            [new("Reporte", report.Title), new("Rango", report.Query.From + " a " + report.Query.To),
                new("Página", report.Query.Page), new("Alcance", report.ScopeDescription), new("Bodega",report.Query.WarehouseId), new("Usuario",report.Query.User),
                new("Caja",report.Query.Register),new("Producto",report.Query.ProductId),new("Proveedor",report.Query.SupplierId),new("Solo agotados",report.Query.OutOfStock),
                new("Nota", "Exportación de la página visible; " + report.Note)]);
    }
    public async Task<byte[]> ExportPdfAsync(ControlQuery query, CancellationToken ct = default)
    {
        await Demand(PermissionCodes.ReportsExport, ct);
        var r = await GetAsync(query, ct);
        return pdf.Write(r.Title, $"{r.Query.From} a {r.Query.To} | Página {r.Query.Page} | {r.TotalCount} registros. Solo página visible. Alcance: {r.ScopeDescription}. Bodega: {r.Query.WarehouseId}; usuario: {r.Query.User}; caja: {r.Query.Register}; producto: {r.Query.ProductId}; proveedor: {r.Query.SupplierId}. {r.Note}",
            r.Columns.Select(x => x.Header).ToList(), r.Rows.Select(x => (IReadOnlyList<string>)x.Cells.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "").ToList()).ToList());
    }
    private static string Label(string value) => value switch {
        "Confirmed"=>"Confirmada", "Cancelled"=>"Anulada", "Draft"=>"Borrador", "Closed"=>"Cerrada", "Inactive"=>"Inactiva",
        "Requested"=>"Solicitada", "PartiallyReceived"=>"Recibida parcialmente", "Received"=>"Recibida",
        "PurchaseEntry"=>"Entrada por compra", "SaleExit"=>"Salida por venta", "PositiveAdjustment"=>"Ajuste positivo", "NegativeAdjustment"=>"Ajuste negativo",
        "ReturnEntry"=>"Entrada por devolución", "CancellationReversal"=>"Reversión por anulación", "TransferExit"=>"Salida por traslado", "TransferEntry"=>"Entrada por traslado",
        "PurchaseReversal"=>"Reversión de compra", "ProductionConsumption"=>"Consumo de producción", "ProductionEntry"=>"Entrada de producción",
        "OrderConsumption"=>"Consumo de restaurante", "OrderReturn"=>"Retorno de restaurante", "PosOrderConsumption"=>"Consumo de pedido POS",
        "Cash"=>"Efectivo", "Card"=>"Tarjeta", "Transfer"=>"Transferencia", "Mixed"=>"Mixto",
        "Taken"=>"Tomado", "Sent"=>"Enviado", "Preparing"=>"En preparación", "Ready"=>"Listo", "Dispatched"=>"Despachado",
        "DineIn"=>"Salón", "TakeAway"=>"Para llevar", "Delivery"=>"Delivery", "Delivered"=>"Entregado",
        _=>value
    };
    private static string Local(DateTimeOffset value) => value.ToOffset(TimeSpan.FromHours(-6)).ToString("dd/MM/yyyy HH:mm");
    private static string Status(SaleStatus value) => value switch { SaleStatus.Confirmed => "Confirmada", SaleStatus.Cancelled => "Anulada", SaleStatus.PartiallyReturned => "Devolución parcial", _ => "Devuelta" };
}

