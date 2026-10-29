namespace SistemiPos.Application.Security;

public static class PermissionCodes
{
    public const string DashboardView = "dashboard.view";
    public const string UsersView = "security.users.view";
    public const string UsersManage = "security.users.manage";
    public const string RolesView = "security.roles.view";
    public const string RolesManage = "security.roles.manage";
    public const string PermissionsView = "security.permissions.view";
    public const string CategoriesView = "catalog.categories.view";
    public const string CategoriesManage = "catalog.categories.manage";
    public const string ProductsView = "catalog.products.view";
    public const string ProductsManage = "catalog.products.manage";
    public const string RecipesView = "recipes.view";
    public const string ProductionView = "production.view";
    public const string ProductionManage = "production.manage";
    public const string ProductionConfirm = "production.confirm";
    public const string RecipesManage = "recipes.manage";
    public const string OrdersDispatch = "restaurant.orders.dispatch";
    public const string PosTerminalsManage = "pos.terminals.manage";
    public const string SuppliersView = "suppliers.view";
    public const string SuppliersManage = "suppliers.manage";
    public const string PurchasesView = "purchases.view";
    public const string PurchasesManage = "purchases.manage";
    public const string PurchasesConfirm = "purchases.confirm";
    public const string PurchasesCancel = "purchases.cancel";
    public const string InventoryView = "inventory.view";
    public const string CustomersView = "customers.view";
    public const string CustomersManage = "customers.manage";
    public const string SalesView = "sales.view";
    public const string SalesManage = "sales.manage";
    public const string DiscountsApply = "sales.discounts.apply";
    public const string DiscountsApplyWithoutAuthorization = "sales.discounts.without-authorization";
    public const string DiscountsAuthorize = "sales.discounts.authorize";
    public const string SalesModifyPrices = "sales.prices.modify";
    public const string SalesCancel = "sales.cancel";
    public const string SalesCancelWithoutAuthorization = "sales.cancel.without-authorization";
    public const string SalesCancelAuthorize = "sales.cancel.authorize";
    public const string SalesReturn = "sales.return";
    public const string SalesReturnWithoutAuthorization = "sales.return.without-authorization";
    public const string SalesReturnAuthorize = "sales.return.authorize";
    public const string SalesRefundDifferentMethod = "sales.refund.different-method";
    public const string TransfersVerify = "sales.transfers.verify";
    public const string RefundsAuthorize = "sales.refunds.authorize";
    public const string CashView = "cash.view";
    public const string CashManage = "cash.manage";
    public const string CashOpen = "cash.open";
    public const string CashCloseOwn = "cash.close.own";
    public const string CashOpenRequirePassword = "cash.open.require-password";
    public const string CashCloseRequirePassword = "cash.close.require-password";
    public const string CashCount = "cash.count";
    public const string CashCountHideExpected = "cash.count.hide-expected";
    public const string CashCloseDifferenceWithoutAuthorization = "cash.close-difference.without-authorization";
    public const string CashDifferenceAuthorize = "cash.difference.authorize";
    public const string CashClosingReport = "cash.closing-report";
    public const string CashMovementsAddIncome = "cash.movements.income";
    public const string CashMovementsAddExpense = "cash.movements.expense";
    public const string CashMovementsWithoutAuthorization = "cash.movements.without-authorization";
    public const string CashMovementsAuthorize = "cash.movements.authorize";
    public const string CashMovementsView = "cash.movements.view";
    public const string CashControlAccess = "cash.control.access";
    public const string CashControlViewAll = "cash.control.view-all";
    public const string CashControlViewOtherUsers = "cash.control.view-other-users";
    public const string CashControlViewSales = "cash.control.sales";
    public const string CashControlViewMovements = "cash.control.movements";
    public const string CashControlViewCounts = "cash.control.counts";
    public const string CashControlViewPosClosings = "cash.control.pos-closings";
    public const string CashControlViewDifferences = "cash.control.differences";
    public const string CashControlViewReports = "cash.control.reports";
    public const string CashControlExport = "cash.control.export";
    public const string CashAuditAccess = "cash.audit.access";
    public const string CashAuditView = "cash.audit.view";
    public const string CashAuditViewAuthorizations = "cash.audit.authorizations";
    public const string CashAuditExport = "cash.audit.export";
    public const string PettyCashViewBalance = "petty-cash.balance.view";
    public const string PettyCashAddExpense = "petty-cash.expense";
    public const string PettyCashAuthorizeExpense = "petty-cash.expense.authorize";
    public const string PettyCashAddIncome = "petty-cash.income";
    public const string PettyCashCancelMovement = "petty-cash.movement.cancel";
    public const string PettyCashAuthorizeCancellation = "petty-cash.cancellation.authorize";
    public const string PettyCashSettle = "petty-cash.settle";
    public const string PettyCashViewHistory = "petty-cash.history";
    public const string PettyCashReports = "petty-cash.reports";
    public const string AccountsPayableView = "accounts-payable.view";
    public const string AccountsPayablePay = "accounts-payable.pay";
    public const string AccountsPayableCancel = "accounts-payable.cancel";
    public const string AccountsReceivableView = "accounts-receivable.view";
    public const string AccountsReceivableCollect = "accounts-receivable.collect";
    public const string AccountsReceivableReceiveCash = "accounts-receivable.receive-cash";
    public const string AccountsReceivableReceiveTransfer = "accounts-receivable.receive-transfer";
    public const string AccountsReceivableCancel = "accounts-receivable.cancel";
    public const string AccountsReceivableAuthorizeCancellation = "accounts-receivable.cancellation.authorize";
    public const string AccountsReceivableViewOverdue = "accounts-receivable.overdue";
    public const string AccountsReceivableExport = "accounts-receivable.export";
    public const string SettingsView = "settings.view";
    public const string SettingsManage = "settings.manage";
    public const string ReportsView = "reports.view";
    public const string ReportsConsolidate = "reports.consolidate";
    public const string ReportsExport = "reports.export";
    public const string FiscalManage = "fiscal.manage";
    public const string OperationsAuditView = "audit.operations.view";
    public const string BackupsManage = "backups.manage";
    public const string OrganizationsView = "organizations.view";
    public const string OrganizationsManage = "organizations.manage";
    public const string BranchSwitch = "organizations.branch.switch";
    public const string UserScopesManage = "security.user-scopes.manage";
    public const string SecurityAuditView = "security.audit.view";
    public const string UsersResetPassword = "security.users.reset-password";
    public const string TablesView = "restaurant.tables.view";
    public const string TablesManage = "restaurant.tables.manage";
    public const string OrdersView = "restaurant.orders.view";
    public const string OrdersCreate = "restaurant.orders.create";
    public const string OrdersUpdate = "restaurant.orders.update";
    public const string KitchenOrdersView = "kitchen.orders.view";
    public const string KitchenOrdersStatus = "kitchen.orders.status";
    public const string RestaurantConfigure = "restaurant.configure";
    public const string OrdersCancel = "restaurant.orders.cancel";
    public const string OrdersDeliver = "restaurant.orders.deliver";
    public const string DeliveryManage = "restaurant.delivery.manage";
    public const string InventoryEntry = "inventory.entry";
    public const string InventoryExit = "inventory.exit";
    public const string InventoryAdjust = "inventory.adjust";
    public const string InventoryTransfersView = "inventory.transfers.view";
    public const string InventoryTransfersCreate = "inventory.transfers.create";
    public const string InventoryTransfersDispatch = "inventory.transfers.dispatch";
    public const string InventoryTransfersReceive = "inventory.transfers.receive";

    public static readonly IReadOnlyList<(string Code, string Module, string Action, string Description)> All =
    [
        (ReportsConsolidate, "Reportes", "Consolidar sucursales", "Consultar varias sucursales dentro del alcance autorizado."),
        (ReportsExport, "Reportes", "Exportar reportes", "Exportar los mismos filtros y permisos de la consulta."),
        (FiscalManage, "Facturación", "Gestionar certificación FEL", "Certificar y conciliar documentos de empresas autorizadas."),
        (OperationsAuditView, "Auditoría", "Consultar auditoría operativa", "Consultar cambios por empresa, sucursal, usuario y documento."),
        (BackupsManage, "Administración", "Gestionar respaldos", "Crear respaldos y consultar su integridad y antigüedad."),
        (RestaurantConfigure, "Restaurante", "Configurar menú y operación", "Administrar canales, estaciones, mesas, opciones, combos, promociones y delivery por sucursal."),
        (OrdersCancel, "Restaurante", "Cancelar y registrar merma", "Cancelar pedidos o líneas con motivo, liberar reservas y registrar desperdicio."),
        (OrdersDeliver, "Restaurante", "Entregar pedidos", "Confirmar entrega de pedidos preparados."),
        (DeliveryManage, "Restaurante", "Gestionar delivery", "Asignar motorista, registrar salida e incidencias de entrega."),
        (DashboardView, "Inicio", "Ver dashboard", "Permite consultar el resumen operativo."),
        (UsersView, "Usuarios", "Ver usuarios", "Permite consultar usuarios."),
        (UsersManage, "Usuarios", "Administrar usuarios", "Permite crear, editar y activar usuarios."),
        (UserScopesManage, "Usuarios", "Administrar alcances de usuario", "Permite asignar sucursales y entidades autorizadas a los usuarios."),
        (RolesView, "Permisos", "Ver roles", "Permite consultar roles."),
        (RolesManage, "Permisos", "Administrar roles y permisos", "Permite crear roles y asignar permisos."),
        (PermissionsView, "Permisos", "Acceder", "Permite acceder a Administración > Permisos."),
        (OrganizationsView, "Organización", "Ver estructura empresarial", "Permite consultar grupos, empresas, sucursales y bodegas."),
        (OrganizationsManage, "Organización", "Administrar estructura empresarial", "Permite crear, editar y cambiar estado de grupos, empresas, sucursales y bodegas."),
        (BranchSwitch, "Organización", "Cambiar sucursal de trabajo", "Permite cambiar la sucursal activa de trabajo desde el selector del sistema."),
        (CategoriesView, "Catálogo", "Ver categorías", "Permite consultar categorías."),
        (CategoriesManage, "Catálogo", "Administrar categorías", "Permite crear, editar y desactivar categorías."),
        (ProductsView, "Catálogo", "Ver productos", "Permite consultar productos."),
        (ProductsManage, "Catálogo", "Administrar productos", "Permite crear, editar y desactivar productos."),
        (ProductionView, "Producción", "Consultar producción", "Consultar órdenes y rendimientos de sucursales autorizadas."),
        (ProductionManage, "Producción", "Planificar producción", "Crear órdenes de producción."),
        (ProductionConfirm, "Producción", "Confirmar producción", "Confirmar consumo, rendimiento real y merma."),
        (RecipesView, "Recetas", "Consultar recetas", "Permite consultar recetas, ingredientes, subrecetas y rendimiento."),
        (RecipesManage, "Recetas", "Administrar recetas", "Permite crear, editar, activar y desactivar recetas."),
        (OrdersDispatch, "Pedidos", "Despachar pedidos", "Registrar entrega y consumo de productos de reventa."),
        (PosTerminalsManage, "Pedidos", "Administrar terminales", "Vincular terminal con caja, sucursal y bodega de consumo."),
        (SuppliersView, "Compras", "Consultar proveedores", "Permite consultar proveedores."),
        (SuppliersManage, "Compras", "Administrar proveedores", "Permite crear, editar y desactivar proveedores."),
        (PurchasesView, "Compras", "Consultar compras", "Permite consultar compras."),
        (PurchasesManage, "Compras", "Registrar compras", "Permite registrar compras en borrador."),
        (PurchasesConfirm, "Compras", "Confirmar compras", "Permite confirmar compras y afectar inventario."),
        (PurchasesCancel, "Compras", "Anular compras", "Permite anular compras recibidas y revertir inventario y cuentas por pagar sin pagos."),
        (AccountsPayableView, "Cuentas por pagar", "Consultar", "Permite consultar cuentas por pagar."),
        (AccountsPayablePay, "Cuentas por pagar", "Registrar pagos", "Permite registrar pagos parciales o totales."),
        (AccountsPayableCancel, "Cuentas por pagar", "Anular", "Permite anular cuentas por pagar."),
        (InventoryView, "Inventario", "Consultar", "Permite consultar existencias, Kardex y stock bajo."),
        (InventoryTransfersView, "Inventario", "Ver traslados", "Permite consultar traslados de inventario entre bodegas."),
        (InventoryTransfersCreate, "Inventario", "Crear traslados", "Permite crear solicitudes de traslado entre bodegas en borrador."),
        (InventoryTransfersDispatch, "Inventario", "Despachar traslados", "Permite despachar traslados descontando existencias de la bodega origen."),
        (InventoryTransfersReceive, "Inventario", "Recibir traslados", "Permite recibir traslados aumentando existencias en la bodega destino."),
        (CustomersView, "Clientes", "Consultar", "Permite consultar clientes."),
        (CustomersManage, "Clientes", "Administrar", "Permite crear, editar y desactivar clientes."),
        (SalesManage, "Ventas", "Realizar ventas", "Permite crear y confirmar ventas."),
        (SalesView, "Ventas", "Consultar ventas", "Permite consultar ventas y comprobantes."),
        (DiscountsApply, "Ventas", "Aplicar descuentos", "Permite solicitar y aplicar descuentos."),
        (DiscountsApplyWithoutAuthorization, "Ventas", "Aplicar descuentos sin autorización", "Permite aplicar descuentos sin un autorizador."),
        (DiscountsAuthorize, "Ventas", "Autorizar descuentos", "Permite autorizar descuentos de otro usuario."),
        (SalesModifyPrices, "Ventas", "Modificar precios", "Permite modificar precios durante una venta."),
        (SalesCancel, "Ventas", "Anular ventas", "Permite solicitar anulaciones."),
        (SalesCancelWithoutAuthorization, "Ventas", "Anular sin autorización", "Permite anular sin un segundo usuario."),
        (SalesCancelAuthorize, "Ventas", "Autorizar anulaciones", "Permite autorizar anulaciones."),
        (SalesReturn, "Ventas", "Realizar devoluciones", "Permite registrar devoluciones."),
        (SalesReturnWithoutAuthorization, "Ventas", "Realizar devoluciones sin autorización", "Permite devolver sin un segundo usuario."),
        (SalesReturnAuthorize, "Ventas", "Autorizar devoluciones", "Permite autorizar devoluciones."),
        (SalesRefundDifferentMethod, "Ventas", "Permitir diferentes formas de reembolso", "Permite reembolsar mediante una forma distinta."),
        (TransfersVerify, "Ventas", "Verificar transferencias", "Permite verificar o rechazar transferencias."),
        (RefundsAuthorize, "Ventas", "Autorizar reembolsos", "Permiso heredado de compatibilidad."),
        (CashOpen, "Caja", "Abrir caja", "Permite abrir la caja propia."),
        (CashCloseOwn, "Caja", "Cerrar su caja", "Permite cerrar la caja propia."),
        (CashOpenRequirePassword, "Caja · Reglas", "Requerir contraseña al abrir", "Exige reautenticación al abrir."),
        (CashCloseRequirePassword, "Caja · Reglas", "Requerir contraseña al cerrar", "Exige reautenticación al cerrar."),
        (CashCount, "Caja", "Realizar arqueo", "Permite contar efectivo por denominación."),
        (CashCountHideExpected, "Caja · Reglas", "Ocultar efectivo esperado durante arqueo", "Oculta el esperado hasta finalizar el primer conteo."),
        (CashCloseDifferenceWithoutAuthorization, "Caja · Reglas", "Cerrar con diferencia sin autorización", "Permite cerrar con diferencia sin autorizador."),
        (CashDifferenceAuthorize, "Caja", "Autorizar diferencias", "Permite autorizar cierres con diferencia."),
        (CashClosingReport, "Caja", "Ver/imprimir reporte de cierre", "Permite consultar e imprimir el cierre."),
        (CashView, "Caja", "Ver cajas", "Permiso heredado de consulta."),
        (CashManage, "Caja", "Administrar caja", "Permiso heredado de operación."),
        (CashMovementsAddIncome, "Movimientos", "Registrar entradas", "Permite registrar entradas de efectivo."),
        (CashMovementsAddExpense, "Movimientos", "Registrar salidas", "Permite registrar salidas de efectivo."),
        (CashMovementsWithoutAuthorization, "Movimientos · Reglas", "Registrar sin autorización", "Permite movimientos manuales sin autorizador."),
        (CashMovementsAuthorize, "Movimientos", "Autorizar movimientos", "Permite autorizar movimientos."),
        (CashMovementsView, "Movimientos", "Consultar movimientos", "Permite consultar movimientos de caja de venta."),
        (CashControlAccess, "Control de cajas", "Acceder", "Permite acceder a Control > Cajas."),
        (CashControlViewAll, "Control de cajas", "Consultar cajas abiertas/cerradas", "Permite consultar sesiones."),
        (CashControlViewOtherUsers, "Control de cajas", "Consultar cajas de otros usuarios", "Permite supervisar cajas ajenas."),
        (CashControlViewSales, "Control de cajas", "Ver ventas", "Permite ver ventas de una sesión."),
        (CashControlViewMovements, "Control de cajas", "Ver movimientos", "Permite ver movimientos."),
        (CashControlViewCounts, "Control de cajas", "Ver arqueos", "Permite ver conteos y reconteos."),
        (CashControlViewPosClosings, "Control de cajas", "Ver cierres POS", "Permite ver conciliaciones POS."),
        (CashControlViewDifferences, "Control de cajas", "Ver diferencias", "Permite ver diferencias de efectivo y POS."),
        (CashControlViewReports, "Control de cajas", "Ver reportes", "Permite ver reportes de cierre."),
        (CashControlExport, "Control de cajas", "Imprimir/exportar", "Permite imprimir o exportar."),
        (CashAuditAccess, "Auditoría", "Acceder", "Permite acceder a la auditoría financiera."),
        (CashAuditView, "Auditoría", "Consultar", "Permite consultar trazabilidad de caja."),
        (CashAuditViewAuthorizations, "Auditoría", "Ver autorizaciones", "Permite consultar autorizaciones y rechazos."),
        (CashAuditExport, "Auditoría", "Exportar", "Permite exportar auditoría financiera."),
        (PettyCashViewBalance, "Caja chica", "Consultar saldo", "Permite consultar fondo y saldo."),
        (PettyCashAddExpense, "Caja chica", "Registrar gastos", "Permite registrar gastos."),
        (PettyCashAuthorizeExpense, "Caja chica", "Autorizar gastos", "Permite autorizar gastos."),
        (PettyCashAddIncome, "Caja chica", "Registrar entradas/reposiciones", "Permite reponer el fondo."),
        (PettyCashCancelMovement, "Caja chica", "Anular movimientos", "Permite solicitar anulaciones."),
        (PettyCashAuthorizeCancellation, "Caja chica", "Autorizar anulaciones", "Permite autorizar anulaciones."),
        (PettyCashSettle, "Caja chica", "Liquidar", "Permite registrar liquidaciones."),
        (PettyCashViewHistory, "Caja chica", "Consultar historial", "Permite consultar movimientos."),
        (PettyCashReports, "Caja chica", "Reportes", "Permite consultar reportes."),
        (AccountsReceivableView, "Cuentas por cobrar", "Consultar", "Permite consultar cuentas por cobrar."),
        (AccountsReceivableCollect, "Cuentas por cobrar", "Registrar abonos", "Permite registrar abonos."),
        (AccountsReceivableReceiveCash, "Cuentas por cobrar", "Recibir efectivo", "Permite recibir abonos en efectivo."),
        (AccountsReceivableReceiveTransfer, "Cuentas por cobrar", "Recibir transferencias", "Permite recibir transferencias."),
        (AccountsReceivableCancel, "Cuentas por cobrar", "Anular cobros", "Permite solicitar anulaciones."),
        (AccountsReceivableAuthorizeCancellation, "Cuentas por cobrar", "Autorizar anulaciones", "Permite autorizar anulaciones."),
        (AccountsReceivableViewOverdue, "Cuentas por cobrar", "Consultar vencidas", "Permite consultar cuentas vencidas."),
        (AccountsReceivableExport, "Cuentas por cobrar", "Imprimir/exportar", "Permite imprimir o exportar."),
        (SettingsView, "Configuración", "Ver configuración", "Permite consultar la configuración operativa."),
        (SettingsManage, "Configuración", "Administrar configuración", "Permite modificar la configuración operativa."),
        (ReportsView, "Reportes", "Ver reportes", "Permite consultar reportes y dashboard."),
        (SecurityAuditView, "Seguridad", "Ver auditoría", "Permite consultar registros de auditoría y accesos del sistema."),
        (UsersResetPassword, "Usuarios", "Restablecer contraseña", "Permite restablecer administrativamente la contraseña de un usuario."),
        (TablesView, "Mesas", "Consultar mesas", "Permite ver el mapa y estado de las mesas."),
        (TablesManage, "Mesas", "Administrar mesas", "Permite crear y configurar mesas del restaurante."),
        (OrdersView, "Pedidos", "Consultar pedidos", "Permite consultar comandas y pedidos."),
        (OrdersCreate, "Pedidos", "Crear pedidos", "Permite crear pedidos y asignarlos a mesas."),
        (OrdersUpdate, "Pedidos", "Actualizar pedidos", "Permite modificar pedidos y enviarlos a cocina."),
        (KitchenOrdersView, "Cocina", "Ver comandas de cocina", "Permite ver pedidos pendientes en cocina."),
        (KitchenOrdersStatus, "Cocina", "Cambiar estado de cocina", "Permite cambiar estado a pendiente, en preparación, listo o entregado."),
        (InventoryEntry, "Inventario", "Entrada de inventario", "Permite registrar ingresos de insumos y productos."),
        (InventoryExit, "Inventario", "Salida de inventario", "Permite registrar bajas o salidas de inventario."),
        (InventoryAdjust, "Inventario", "Ajustar inventario", "Permite realizar ajustes de inventario autorizados.")
    ];
}
