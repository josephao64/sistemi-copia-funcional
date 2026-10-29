# Revisión general de coherencia y roles de SISTEMI POS ERP

Fecha: 21 de octubre de 2026. Zona horaria: America/Guatemala.

## Resultado de la revisión

**El proyecto contiene implementación de las fases, pero todavía no cumple el recorrido integral del plan ni aplica de forma uniforme el aislamiento por ubicación y los permisos.** El bloqueo principal está entre el pedido de restaurante utilizado por mesero y cocina y el pedido que acepta caja para cobrar. También hay consultas y operaciones administrativas que pueden alcanzar documentos de otras sucursales.

La revisión identifica **24 hallazgos**: 14 de prioridad P1 y 10 de prioridad P2. P1 significa corregir antes de aceptar la operación integrada o ampliar el uso entre ubicaciones. P2 significa corregir la capacidad operativa, la interfaz o la consistencia indicada. La prioridad se basa en el comportamiento del código; no demuestra que alguien haya explotado el problema ni que existan datos afectados.

Se comparó el archivo `C:/Users/Elizabeth/Downloads/Plan_Implementacion_SISTEMI_POS_ERP.docx` con el código activo de `src/`, las plantillas Razor, JavaScript, permisos iniciales, servicios, repositorios, configuraciones de persistencia y pruebas relacionadas. El documento se trató como referencia de requisitos. La copia anidada del repositorio no se usó como implementación activa.

**Validación obtenida:** compilación Release correcta, con cero errores y cero advertencias; 92 pruebas unitarias y 24 pruebas de componentes/modelo aprobadas, sin fallos ni omisiones en esos grupos. Además, se confirmaron 12 observaciones de contratos mediante un script local, incluidas simulaciones del JavaScript original del service worker con transporte y caché ficticios. Estas últimas no equivalen a pruebas de aceptación del navegador.

La revisión de vistas fue de su lógica Razor y JavaScript. No se abrieron sesiones reales de los ocho roles, no se aplicaron migraciones ni se operó sobre la base del negocio. Por tanto, no se certifican apariencia en dispositivos, datos actuales, concurrencia PostgreSQL ni despliegue efectivo.

## Relación entre las fases

| Fase del plan | Implementación encontrada | Resultado al conectarla con el resto |
| --- | --- | --- |
| 0 Estabilización | Seguridad de acceso, correlativos de venta, idempotencia y pruebas | Las compras conservan correlativos basados en último número más uno; el modo offline presenta contratos distintos entre página y service worker. |
| 1 Fundación empresarial | Grupo, empresa, sucursal, bodega, asignaciones y selector | Los alcances no se aplican en todos los módulos; el selector del gerente exige un permiso adicional no asignado. |
| 2 Catálogo y unidades | Tipos, unidades, conversiones, variantes y ajustes locales | Hay base de catálogo compartido y configuración por sucursal; la copia del menú rompe referencias internas y la moneda del catálogo POS tiene un literal fijo. |
| 3 Inventario por ubicación | Existencias, Kardex, lotes, traslados y conteos | La consulta general de Kardex puede reunir ubicaciones; las exportaciones permiten parámetros sin validar alcance. Bodega no recibe el permiso que exige el conteo físico. |
| 4 Compras | Destinos, órdenes, recepción y relación financiera | Las lecturas por ID y varias operaciones sobre documentos existentes no verifican sucursal autorizada. |
| 5 Recetas y producción | Recetas versionadas, consumo y producción | Hay reglas y pruebas específicas; el costo de consumo histórico no se usa de forma consistente en todos los informes. Cocina y Bodega no reciben producción en su perfil inicial; debe decidirse quién produce. |
| 6 Restaurante | Menú, modificadores, extras, exclusiones, mesas, KDS, delivery y merma | Su pedido no tiene conversión al cobro de fase 7. El KDS tiene datos de demostración, acciones confusas y actualización que se detiene en ciertos estados. |
| 7 POS y caja | Otro agregado de pedidos, cobro, pagos mixtos, divisiones, devoluciones y offline | El cobro opera con `PosOrders`; el flujo de restaurante utiliza `RestaurantOrders`. Los permisos iniciales tampoco forman un recorrido completo para los operadores habituales. |
| 8 Control | Reportes por alcance, auditoría, expediente fiscal y respaldos | El centro nuevo valida permisos y alcance, pero las rutas anteriores no siguen siempre las mismas reglas. FEL/NIT real y recuperación demostrada siguen pendientes. |

No se asigna un porcentaje de avance: compilar o contar archivos no permite medir el cumplimiento de los criterios de aceptación del plan.

## Pantallas y capacidades de cada rol

Esta matriz refleja los permisos que `SecurityBootstrapper` establece al arrancar. No es un inventario de los usuarios actualmente guardados en la base. Cada cuenta nueva admite un solo rol; las funciones combinadas requieren un rol personalizado adecuado.

| Rol inicial | Entrada y pantallas previstas por código | Capacidad encontrada y puntos que impiden aceptarla |
| --- | --- | --- |
| Super Administrador, 123 permisos | Dashboard, organización, seguridad, operación, reportes, fiscal y respaldos | Tiene las herramientas más amplias, pero sigue afectado por la separación restaurante/cobro, configuración global y reportes inconsistentes. Su perfil puede ocultar carencias que sí sufren los demás roles. |
| Administrador, 76 permisos | Dashboard, productos, recetas, producción, compras, inventario, cuentas, control de caja y restaurante | Tiene permisos de usuarios y roles, pero esos controladores exigen además el rol Super Administrador. No recibe por defecto ver ventas, administrar terminales, cortes X/Z, consolidación/exportación, auditoría operativa ni gestión fiscal/respaldos. Parte de la administración queda oculta en navegación. |
| Gerente, 31 permisos | Dashboard, inventario, compras de consulta, cuentas, aprobaciones y control de caja | El selector de sucursal se muestra, pero el POST resulta incompatible con su matriz inicial. Tiene `UsersView` sin acceso al controlador de usuarios. La supervisión financiera encuentra consultas que no aíslan sucursal. |
| Punto de Venta, 14 permisos | `/pos`, barra superior especial, ventas, caja propia, clientes y pedidos de restaurante | Puede vender artículos admitidos por venta directa. No puede crear ni confirmar pedidos POS con su perfil inicial; los preparados deben cobrarse desde esos pedidos. Tiene anulación/devolución solicitada, pero los supervisores iniciales no reciben las autorizaciones específicas correspondientes. |
| Cajero, 12 permisos | `/pos`, pero conserva la barra lateral general | Comparte cobro y caja con Punto de Venta, con otra navegación y menos acciones. No recibe crear/confirmar pedidos POS, corte de cierre ni bandera de conteo ciego. Debe definirse si son dos puestos diferentes o dos presentaciones del mismo puesto. |
| Mesero, 8 permisos | Pedidos de restaurante, mesas, consulta de productos/categorías y entrega | Puede tomar y enviar el pedido del restaurante sin abrir caja, lo cual separa correctamente atención y cobro. Sin embargo, ese pedido no llega al cobro integrado. También ve el acceso a `/pedidos`, cuyo alta exige caja propia que su rol no puede abrir. |
| Cocina, 3 permisos | KDS de restaurante y consulta de pedidos | Puede preparar y marcar listo el pedido de restaurante. El KDS no muestra los pedidos de `PosOrders`; para estos se usan otras pantallas. No tiene cancelación/merma ni producción; esas responsabilidades necesitan asignación explícita. Los botones y mensajes del KDS presentan las inconsistencias R18–R20. |
| Bodega, 10 permisos | Productos de consulta, inventario, lotes, caducidad y traslados | Tiene ajuste de lotes y traslados. El conteo físico exige administrar productos y queda bloqueado. `InventoryEntry` e `InventoryExit` no conectan por sí solos con un flujo general de ingreso/salida. Compras y producción no se asignan a este rol inicial. |

Referencias: [SecurityBootstrapper.cs:19](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Security/SecurityBootstrapper.cs:19>), [AccountController.cs:235](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/AccountController.cs:235>), [_Layout.cshtml:28](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Views/Shared/_Layout.cshtml:28>), [LoginAuthorizationContext.cs:11](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Application/Security/LoginAuthorizationContext.cs:11>).

## Hallazgos y correcciones recomendadas

### R01 P1 El pedido del mesero y cocina no se puede cobrar por el flujo integrado

`/restaurante` utiliza el agregado de `Domain.Restaurant` guardado en `RestaurantOrders`; `/pedidos` y `CheckoutController` utilizan `Domain.Orders`, guardado en `PosOrders`. El contrato `RestaurantService.CheckoutAsync` solo devuelve una instantánea y no se consume en el servicio que confirma ventas. La vista del pedido de restaurante tampoco ofrece el cobro conectado al checkout actual. Es una limitación reconocida en la documentación de fase 7.

**Efecto:** tomar una pizza con extras y tarifa de delivery, prepararla en el KDS y cobrar ese mismo pedido conservando sus datos no constituye hoy un recorrido completo. Recapturarla como otro pedido implica otra identidad y otro circuito de consumo. **Corrección:** acordar un pedido operativo canónico o un puente transaccional explícito que preserve líneas, cantidades ya cobradas, extras, tarifa, moneda, consumos y devoluciones. No duplicar pedidos ni consumos para resolver la interfaz.

Fuentes: [RestaurantRepository.cs:27](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Restaurant/RestaurantRepository.cs:27>), [RestaurantOrderConfiguration.cs:28](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Persistence/Configurations/RestaurantOrderConfiguration.cs:28>), [RestaurantService.cs:345](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Application/Restaurant/RestaurantService.cs:345>), [CheckoutController.cs:23](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/CheckoutController.cs:23>), [FASE_7_POS_IMPLEMENTACION.md:17](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/docs/FASE_7_POS_IMPLEMENTACION.md:17>).

### R02 P1 Inventario y Kardex permiten consultar otras ubicaciones

`InventoryController.Kardex` calcula la sucursal, pero envía al repositorio únicamente `warehouseId`. Al no seleccionar bodega, el repositorio no aplica sucursal y consulta todos los movimientos. Si se recibe una bodega ajena, el controlador puede convertirla en null y ampliar la consulta. `Index` tampoco valida que la bodega enviada pertenezca a la sucursal; el repositorio prioriza `warehouseId`. La exportación permite enviar sucursal o bodega sin una validación equivalente de alcance.

**Efecto:** un usuario con `InventoryView` puede obtener existencias o movimientos fuera de su ubicación. **Corrección:** exigir contexto válido, validar cada bodega contra el alcance y aplicar el filtro en la consulta del repositorio; una ubicación inválida debe rechazarse, sin ampliar la selección.

Fuentes: [InventoryController.cs:147](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/InventoryController.cs:147>), [InventoryController.cs:182](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/InventoryController.cs:182>), [InventoryRepository.cs:82](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Inventory/InventoryRepository.cs:82>), [InventoryRepository.cs:33](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Inventory/InventoryRepository.cs:33>).

### R03 P1 Compras no aplica alcance en documentos existentes

`PurchaseRepository` lista compras y órdenes globalmente y obtiene documentos por ID sin sucursal. El servicio valida destino al crear o editar ciertos documentos, pero confirmar una compra carga el ID y afecta su inventario sin verificar que la ubicación pertenezca al usuario. No hay un filtro global de EF que complete esa autorización.

**Efecto:** el permiso de compras puede dar consulta y, con permiso de confirmación, operación sobre documentos ajenos. **Corrección:** filtrar listados y búsquedas por alcance y validar el recurso al confirmar, recibir, modificar y anular; conservar su ubicación histórica.

Fuentes: [PurchaseRepository.cs:14](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Purchases/PurchaseRepository.cs:14>), [PurchaseRepository.cs:37](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Purchases/PurchaseRepository.cs:37>), [PurchaseRepository.cs:132](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Purchases/PurchaseRepository.cs:132>), [PurchaseService.cs:210](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Application/Purchases/PurchaseService.cs:210>).

### R04 P1 Cuentas por pagar y cobrar operan sin ubicación autorizada

`FinancialService` no recibe contexto organizacional. Sus consultas de cuentas, pagos, abonos y anulaciones trabajan por ID global. El efectivo se conecta a la caja activa del usuario sin comprobar que corresponda a la sucursal del documento de origen.

**Efecto:** un usuario con acceso financiero puede consultar cuentas ajenas y un operador autorizado para pagar o cobrar puede afectar otra sucursal usando su caja. **Corrección:** derivar el alcance de la compra o venta de origen y exigir compatibilidad de caja, empresa y sucursal antes de cualquier asiento.

Fuentes: [FinancialService.cs:14](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Finance/FinancialService.cs:14>), [FinancialService.cs:42](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Finance/FinancialService.cs:42>), [FinancialService.cs:131](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Finance/FinancialService.cs:131>), [FinancialService.cs:207](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Finance/FinancialService.cs:207>).

### R05 P1 La administración organizacional no limita los recursos por alcance

`OrganizationController` verifica permisos de acción, pero el servicio y repositorio de organización devuelven grupos, empresas, sucursales y bodegas sin filtrar las asignaciones del usuario. Las modificaciones cargan recursos por ID. El rol Administrador recibe `OrganizationsView` y `OrganizationsManage`, aunque su cuenta pueda estar asignada solo a una sucursal.

**Efecto:** la ubicación asignada a un administrador no limita por sí sola la estructura que puede consultar o cambiar mediante URL. **Corrección:** definir y aplicar el alcance administrativo de cada acción sobre la entidad destino; reservar la administración global a quien tenga ese alcance explícito.

Fuentes: [OrganizationController.cs:17](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/OrganizationController.cs:17>), [OrganizationService.cs:9](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Organizations/OrganizationService.cs:9>), [OrganizationRepository.cs:36](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Organizations/OrganizationRepository.cs:36>).

### R06 P1 Control de caja omite restricciones por acción y sucursal

`CashControlController` se protege con acceso general. Sus detalles no exigen los permisos específicos para ver cajas ajenas, arqueos, diferencias o reportes. `Movements` carga los últimos 1,000 movimientos globales y `Audit` los últimos 2,000 registros con caja, sin filtrar sucursal. La vista de detalles muestra información financiera sin aplicar cada permiso de consulta.

**Efecto:** los permisos finos que aparecen al configurar roles no restringen toda la información que prometen restringir. **Corrección:** aplicar filtros antes de materializar datos y validar cada capacidad tanto en backend como al construir el modelo y mostrar secciones.

Fuentes: [CashControlController.cs:14](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/CashControlController.cs:14>), [CashControlController.cs:60](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/CashControlController.cs:60>), [CashControlController.cs:85](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/CashControlController.cs:85>), [CashControlController.cs:92](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/CashControlController.cs:92>), [Details.cshtml:25](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Views/CashControl/Details.cshtml:25>).

### R07 P1 Cualquier usuario autenticado puede consultar el historial global de caja chica

`PettyCashController.History` y `HistoryDetails` heredan solo `[Authorize]`. Leen ciclos cerrados y movimientos por ID sin comprobar `PettyCashViewHistory`, responsable del fondo ni ubicación.

**Efecto:** una cuenta de cocina o mesero puede acceder por URL al historial financiero de fondos ajenos. **Corrección:** exigir el permiso correspondiente y filtrar por responsabilidad y alcance autorizado, incluyendo el detalle por ID.

Fuente: [PettyCashController.cs:43](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/PettyCashController.cs:43>).

### R08 P1 La configuración empresarial no se usa en caja y venta

Existe `EnterpriseSettingsResolver`, con herencia empresa/sucursal, pero no tiene consumidores productivos fuera de su registro de dependencias. `SaleRepository`, `CashRepository`, el checkout y la impresión obtienen `SystemSettings` globales.

**Efecto:** configurar moneda, impuestos, medios de pago o identidad comercial por empresa/sucursal no asegura que la operación y el comprobante usen esos valores. **Corrección:** resolver la configuración efectiva del contexto en venta, caja y comprobantes, y guardar las instantáneas que deban permanecer históricas.

Fuentes: [EnterpriseSettingsResolver.cs:8](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Application/Organizations/EnterpriseSettingsResolver.cs:8>), [SaleRepository.cs:145](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Sales/SaleRepository.cs:145>), [CashRepository.cs:12](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Cash/CashRepository.cs:12>), [SalesController.cs:391](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/SalesController.cs:391>).

### R09 P1 Las sesiones no comprueban la marca de seguridad al validar la cookie

`Program.cs` reemplaza `OnValidatePrincipal` con una comprobación de existencia y estado activo. No llama al validador de `SecurityStamp`. Cambiar rol o restablecer contraseña actualiza esa marca, pero la cookie existente no la compara. Algunas comprobaciones consultan roles en base y otras usan los roles guardados en la cookie.

**Efecto:** puede conservarse una sesión y un rol anterior después de un cambio de seguridad. Por ejemplo, una cookie que todavía contiene Super Administrador combinada con los permisos de un Administrador puede superar restricciones que dependían del nombre del rol. **Corrección:** conservar la validación de Identity y regenerar o revocar la sesión cuando cambie la marca o el contexto de autorización.

Fuentes: [Program.cs:54](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Program.cs:54>), [UserAccountService.cs:61](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Security/UserAccountService.cs:61>), [UsersController.cs:359](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/UsersController.cs:359>).

### R10 P1 Los permisos personalizados de los roles iniciales se pierden al reiniciar

Todos los roles iniciales se crean o actualizan con `enforcePermissions: true`. Al arrancar, `EnsureRoleAsync` elimina permisos fuera de la lista fija y vuelve a agregar los de esa lista. La pantalla de permisos permite guardar cambios en esos mismos roles y muestra confirmación de éxito.

**Efecto:** una corrección de permisos realizada desde administración puede desaparecer en el siguiente arranque. **Corrección:** separar la creación inicial de roles de una actualización controlada de su matriz, y conservar las decisiones guardadas por el administrador.

Fuentes: [SecurityBootstrapper.cs:271](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Security/SecurityBootstrapper.cs:271>), [RolesController.cs:62](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/RolesController.cs:62>), [Program.cs:120](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Program.cs:120>).

### R11 P1 La sincronización offline en segundo plano no cumple el contrato del servidor

La página `pos-sync.js` envía `offlineTicket` y `expectedTotal`; el serializador del service worker omite ambos. Su POST tampoco envía el token antifalsificación, aunque el endpoint lleva `ValidateAntiForgeryToken`. El worker selecciona pendientes de todos los contextos, mientras la página filtra por identidad y sucursal.

**Efecto:** el reintento automático en segundo plano puede ser rechazado y no equivale a sincronizar con la página abierta. No se afirma pérdida de operaciones: se conservan en cola con estado de error. **Corrección:** compartir el protocolo de sincronización, obtener un token válido para la sesión, y seleccionar únicamente operaciones del contexto autorizado. El servidor debe conservar sus validaciones.

Fuentes: [service-worker.js:106](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/wwwroot/service-worker.js:106>), [service-worker.js:154](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/wwwroot/service-worker.js:154>), [pos-sync.js:389](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/wwwroot/js/pos-sync.js:389>), [OfflinePosController.cs:77](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/Api/OfflinePosController.cs:77>). Confirmado ejecutando las funciones originales con datos ficticios.

### R12 P1 La caché de páginas POS no distingue usuario y sucursal

El service worker guarda HTML autenticado con las claves `/pos` y `/Pos/Offline`, dentro de una caché compartida. Al fallar la red devuelve la página guardada por esa ruta, sin identificar quién inició la sesión ni dónde opera.

**Efecto:** un equipo compartido puede mostrar información o contexto del usuario anterior al operar sin conexión. Las validaciones del servidor no evitan esa exposición local. **Corrección:** separar o evitar la caché de HTML autenticado, invalidarla al cambiar de identidad y ubicación y cargar el contexto mediante un contrato verificable.

Fuente: [service-worker.js:73](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/wwwroot/service-worker.js:73>). Confirmado con la función original y una caché ficticia.

### R13 P1 Existen permisos de control financiero que no se hacen efectivos

Los permisos `CashOpenRequirePassword` y `CashCloseRequirePassword` se definen, pero no se consultan al abrir o cerrar. `AccountsReceivableReceiveCash` y `AccountsReceivableReceiveTransfer` tampoco se validan al registrar el medio de un abono. Caja chica permite registrar movimientos, liquidar y cancelar basándose en el responsable del fondo, sin aplicar los permisos específicos anunciados; la cancelación recibe credenciales de autorizador y no las valida. Los movimientos manuales de caja se autorizan mediante `CashManage`, sin distinguir los permisos de entrada y salida.

**Efecto:** quitar una casilla en permisos no garantiza prohibir la operación correspondiente. **Corrección:** vincular cada permiso con su acción real y acordar qué controles dependen de asignación de fondo y cuáles de autorización adicional. La asignación no debe interpretarse silenciosamente como todos los permisos financieros.

Fuentes: [PermissionCodes.cs:50](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Application/Security/PermissionCodes.cs:50>), [CashService.cs:58](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Application/Cash/CashService.cs:58>), [FinancialService.cs:163](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Finance/FinancialService.cs:163>), [FinancialService.cs:187](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Finance/FinancialService.cs:187>), [CashController.cs:193](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/CashController.cs:193>).

### R14 P2 El gerente ve el cambio de sucursal pero la ruta lo bloquea

Gerente recibe `BranchSwitch`, y la navegación muestra el selector cuando tiene varias sucursales. El controlador exige también `OrganizationsView` a nivel de clase, permiso que Gerente no recibe.

**Efecto:** el cambio termina en acceso denegado aunque la sucursal esté asignada. **Corrección:** proteger el selector con su permiso y validación de alcance, sin añadirle accidentalmente todos los accesos de administración organizacional.

Fuentes: [SecurityBootstrapper.cs:90](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Security/SecurityBootstrapper.cs:90>), [OrganizationController.cs:12](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/OrganizationController.cs:12>), [OrganizationController.cs:35](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/OrganizationController.cs:35>).

### R15 P2 Bodega no puede realizar el conteo físico con sus permisos iniciales

El rol tiene `InventoryView` y `InventoryAdjust`; las pantallas y POST del conteo físico requieren `ProductsManage`. No recibe ese permiso. Los permisos de entrada/salida tampoco constituyen por sí mismos un flujo de registro de inventario.

**Efecto:** el encargado de inventario no puede completar el conteo y otorgarle administración del catálogo amplía funciones innecesariamente. **Corrección:** asociar el conteo a permisos de inventario adecuados, aplicarlos en controlador y servicio y asignar la función al puesto aprobado.

Fuentes: [InventoryController.cs:90](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/InventoryController.cs:90>), [SecurityBootstrapper.cs:153](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Security/SecurityBootstrapper.cs:153>).

### R16 P2 La matriz inicial no completa las tareas de operadores y supervisores

Punto de Venta y Cajero no reciben `OrdersCreate` ni `OrdersUpdate`. Mesero sí los recibe, pero el alta de `/pedidos` exige caja propia; Mesero no puede abrirla. `OrdersDispatch` y `PosTerminalsManage` no se asignan a los roles habituales distintos de Super Administrador. Los cajeros tampoco reciben `CashClosingReport` ni `CashCountHideExpected`. Administrador/Gerente no reciben varias capacidades nuevas de fase 8 ni las autorizaciones específicas de anulación/devolución que solicita Punto de Venta.

**Efecto:** existen pantallas y permisos, pero los perfiles habituales no completan todas las funciones que sus descripciones sugieren. El aviso de arqueo ciego no basta para demostrar que esa política esté activada. **Corrección:** aprobar una matriz por puesto y recorrido, incluyendo supervisor, despachador y responsable de producción; después aplicarla de forma persistente. Los controladores reservados a Super Administrador deben reflejar claramente esa reserva en la administración de permisos.

Fuentes: [SecurityBootstrapper.cs:107](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Security/SecurityBootstrapper.cs:107>), [OrdersController.cs:47](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/OrdersController.cs:47>), [OrderService.cs:140](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Application/Orders/OrderService.cs:140>), [CashService.cs:49](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Application/Cash/CashService.cs:49>).

### R17 P2 El catálogo del POS muestra una moneda fija

La fila del producto imprime `Q` y dos decimales, aunque la vista ya recibe símbolo y decimales configurados. El resto del cobro usa otras funciones de formato.

**Efecto:** con otra moneda o precisión, catálogo y total pueden mostrar convenciones diferentes. **Corrección:** usar una misma configuración efectiva para todas las cantidades monetarias de la pantalla y conservar la histórica en documentos.

Fuente: [Form.cshtml:58](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Views/Sales/Form.cshtml:58>).

### R18 P2 El KDS conserva datos de demostración y altera la prioridad real

La vista contiene reglas para números concretos como `PED-00000045` a `PED-00000049`, el nombre fijo `Somaline`, una mesa predeterminada `Mesa 4`, opciones especiales para productos de demostración y una tarjeta vacía con aviso de retraso. Después del orden especial, ordena por minutos transcurridos ascendentes, dando precedencia a comandas más recientes.

**Efecto:** una pantalla operativa puede atribuir una persona o mesa incorrecta, mostrar un retraso ficticio y priorizar mal las comandas. **Corrección:** retirar los casos de demostración de la vista y establecer una regla de prioridad basada en datos y tiempos reales; usar estados explícitos cuando falte información.

Fuente: [Kitchen.cshtml:110](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Views/Restaurant/Kitchen.cshtml:110>), [Kitchen.cshtml:138](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Views/Restaurant/Kitchen.cshtml:138>).

### R19 P2 Las acciones y actualización del KDS no corresponden del todo a su presentación

`Completar Comanda` primero inicia la preparación de la primera línea pendiente; después marca lista solo la primera línea en preparación. No completa toda la tarjeta. La variable `canManageStatus` se calcula, pero no oculta ni bloquea los botones para un usuario que solo consulta. Al quedar una tarjeta lista, el botón lleva `disabled` y la clase `kds-btn-done`; el temporizador interpreta todo botón deshabilitado sin clase `is-done` como operación en curso y deja de recargar.

**Efecto:** el usuario puede creer que completó toda la comanda, recibir un rechazo por un botón que no debería estar disponible o dejar de ver pedidos nuevos mientras permanezca una tarjeta lista. **Corrección:** definir acciones por línea o tarjeta, nombrarlas según su efecto, aplicar permisos a la interfaz y separar el estado ocupado del estado final.

Fuentes: [Kitchen.cshtml:318](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Views/Restaurant/Kitchen.cshtml:318>), [Kitchen.cshtml:325](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Views/Restaurant/Kitchen.cshtml:325>), [Kitchen.cshtml:9](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Views/Restaurant/Kitchen.cshtml:9>), [restaurant.js:269](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/wwwroot/js/restaurant.js:269>).

### R20 P2 Notificar al mesero solo muestra una confirmación local

El botón de notificación construye un mensaje y lo muestra en el mismo navegador. No envía solicitud al servidor ni comunica el evento al usuario indicado.

**Efecto:** cocina recibe el mensaje «Notificación enviada» sin que se haya enviado una notificación. **Corrección:** conectar un evento real con destinatario y estado de entrega, o cambiar el texto y alcance de la acción para que describa lo que hace actualmente.

Fuente: [restaurant.js:197](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/wwwroot/js/restaurant.js:197>).

### R21 P2 Copiar menú entre sucursales rompe las referencias a estaciones y secciones

`CopyBranchMenuAsync` genera identificadores nuevos de estaciones y secciones, pero conserva los antiguos `StationId` y `SectionId` de las entradas del menú. También conserva combos/promociones después de filtrar productos compatibles. La validación exige que las referencias existan en el nuevo conjunto.

**Efecto:** guardar el menú copiado puede fallar con una estación o sección inexistente, y los combos pueden referirse a productos retirados. **Corrección:** construir un mapeo de identificadores y reescribir todas las referencias, validando combos y promociones con el menú resultante antes de presentarlo como copia válida.

Fuentes: [RestaurantService.cs:79](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Application/Restaurant/RestaurantService.cs:79>), [RestaurantSetup.cs:61](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Domain/Restaurant/RestaurantSetup.cs:61>).

### R22 P1 Los reportes comerciales anteriores no cuadran con pagos mixtos y costo de consumo

`ReportRepository.GetSalesReportAsync` calcula costo con `CostAtSale`; el centro nuevo usa también `ConsumedUnitCostAtSale` y ajustes documentados. `SalesIndicatorSummary` suma efectivo, transferencia y tarjeta solo cuando la venta completa tiene ese método. Una venta con Q50 en efectivo y Q39 en tarjeta tiene método resumen `Mixed` y queda fuera de ambos subtotales, aunque sí cuente en el total vendido.

**Efecto:** dos pantallas pueden informar distinto margen sobre la misma venta y los subtotales por forma de pago no concilian las ventas mixtas. **Corrección:** definir un cálculo común de ingresos/costos históricos y sumar los pagos individuales con sus reembolsos y moneda. Las diferencias de alcance temporal deben explicarse en la interfaz.

Fuentes: [ReportRepository.cs:200](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Reports/ReportRepository.cs:200>), [ReportModels.cs:93](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Application/Reports/ReportModels.cs:93>), [Sale.cs:68](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Domain/Sales/Sale.cs:68>), [ControlReportService.cs:184](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Reports/ControlReportService.cs:184>).

### R23 P2 Las exportaciones anteriores omiten el nuevo permiso de exportar

El centro de control exige `ReportsExport`, pero las exportaciones de inventario y dashboard heredan solo el permiso de consulta de su controlador. Un usuario al que se retire exportación puede seguir descargando desde esas rutas.

**Efecto:** la misma política de exportación cambia según la pantalla de entrada. **Corrección:** decidir si el permiso nuevo cubre todas las exportaciones de reportes y aplicarlo uniformemente, siempre con validación de alcance y filtros en backend.

Fuentes: [ControlReportsController.cs:28](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/ControlReportsController.cs:28>), [InventoryController.cs:182](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/InventoryController.cs:182>), [HomeController.cs:76](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/HomeController.cs:76>).

### R24 P2 Compras y órdenes conservan correlativos no atómicos

`GeneratePurchaseNumberAsync` y el generador de órdenes leen el último número y suman uno. No usan el mecanismo atómico que exige la estabilización para operaciones concurrentes.

**Efecto:** dos operadores pueden elegir el mismo siguiente número. Un índice único puede impedir duplicarlo, pero deja una operación fallida que requiere tratamiento y reintento. **Corrección:** usar secuencia o contador transaccional con la política de numeración aprobada; comprobar concurrencia e idempotencia de recepción.

Fuentes: [PurchaseRepository.cs:99](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Purchases/PurchaseRepository.cs:99>), [PurchaseRepository.cs:112](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Purchases/PurchaseRepository.cs:112>).

## Otros puntos que requieren una decisión o aceptación

**Arqueo obligatorio.** La pantalla exige realizarlo antes de cerrar, mientras `CloseSessionAsync` acepta el importe de la solicitud cuando no hay arqueo guardado. Si la regla aprobada es que todo cierre tenga conteo por denominación, debe aplicarse en backend. Esta diferencia se documenta como decisión pendiente y no se cambia durante la revisión. Fuentes: [Details.cshtml:29](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Views/Cash/Details.cshtml:29>), [CashService.cs:137](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Application/Cash/CashService.cs:137>).

**Perfil de administración.** Los controladores de usuarios, roles, permisos y configuración están reservados por nombre de rol a Super Administrador. Asignar sus permisos a Administrador, Gerente o un rol personalizado no otorga acceso. Esa reserva aparece en pruebas existentes y puede ser intencional; conviene que las descripciones y la pantalla de permisos no sugieran lo contrario. Del mismo modo, `/pos` tiene una restricción adicional por nombres de rol aunque otras acciones se autoricen por permisos.

**Separación de puestos.** Debe decidirse quién prepara lotes de producción, quién recibe compras, quién despacha, quién registra merma, quién autoriza devolución y quién gestiona fiscal/respaldos. No se debe convertir Cocina o Bodega en administradores generales para hacer accesible una acción. La interfaz distinta de Cajero y Punto de Venta también necesita una definición de puesto.

**Historial de clientes.** El catálogo de clientes puede ser compartido, pero `CustomerRepository.GetCustomerHistoryAsync` consulta todas las ventas de ese cliente sin filtro por sucursal. Debe decidirse y autorizarse por separado la consulta de documentos de otras ubicaciones. Fuente: [CustomerRepository.cs:28](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Customers/CustomerRepository.cs:28>).

**Indicadores del dashboard.** El inicio usa vencidas de un servicio financiero global y lotes de una consulta sin bodega; el enlace de caducidad conduce a una vista filtrada por la bodega activa. Puede mostrar una cantidad distinta a su detalle. Debe reutilizarse el mismo alcance para indicador y destino. Fuentes: [HomeController.cs:36](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/HomeController.cs:36>), [ProductsController.cs:39](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/ProductsController.cs:39>).

**Cierre externo y operativo de fase 8.** El proveedor fiscal instalado es `UnconfiguredFiscalProvider`. La integración FEL real, el proveedor externo NIT, la homologación, los objetivos RPO/RTO y un simulacro real de restauración continúan pendientes según la documentación. No se generan certificaciones reales sin conector, lo cual es una protección correcta. Fuentes: [FiscalService.cs:12](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/src/SistemiPos.Infrastructure/Sales/FiscalService.cs:12>), [FASE_8_CONTROL_REPORTES_Y_RESPALDOS.md:22](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/docs/FASE_8_CONTROL_REPORTES_Y_RESPALDOS.md:22>).

## Controles que sí tienen una base coherente

- La separación de las capas del sistema se mantiene y la solución compila.
- El servicio de restaurante comprueba permiso, sucursal activa y asignación autorizada antes de operar; reserva, consume y registra eventos con control de versión e idempotencia.
- Los pedidos POS validan compatibilidad de terminal, caja y bodega, y permiten a cocina operar sin usar credenciales del cajero.
- Caja comprueba propiedad para operar y el modelo tiene índices para impedir sesiones activas duplicadas por usuario o caja física/sucursal.
- El servicio de venta comprueba caja activa, propietario, moneda, líneas de pedido y pagos; existen pruebas de pagos mixtos, división y reglas críticas.
- El nuevo centro de control verifica alcance autorizado, permiso de exportación y permiso adicional de auditoría. Sus exportaciones se presentan como página visible.
- Se conservan instantáneas de recetas y costos de consumo, y el proveedor fiscal deshabilitado no inventa una certificación.

Estas fortalezas corresponden a los componentes revisados; no eliminan los accesos alternativos descritos arriba.

## Orden propuesto de corrección

1. **Cerrar exposición y modificaciones fuera de alcance:** R02–R07, R09 y R12. Comprobar rechazo por URL y por servicio sobre documentos de otra sucursal.
2. **Completar el recorrido del negocio:** R01 y R08. Un solo pedido debe conservar su identidad desde mesero y cocina hasta venta, cobro, entrega, devolución y reporte.
3. **Hacer persistentes y efectivos los permisos:** R10, R13–R16 y R23. Alinear permiso mostrado, backend, datos entregados y navegación.
4. **Conciliar dinero y recuperación offline:** R11, R22 y R24. Revisar caja, cobros, reembolsos, costos, moneda, reintentos y concurrencia.
5. **Corregir operación de las vistas:** R17–R21, indicadores y mensajes. Después revisar la apariencia y uso táctil con cada puesto.
6. **Completar aceptación de despliegue:** migración ensayada, conciliación de datos, piloto, integración fiscal y recuperación demostrada.

## Recorridos de aceptación por usuario

Los siguientes casos son la aceptación pendiente después de corregir los hallazgos; no se presentan como ejecutados.

| Puesto | Recorrido que debe completarse | Resultado exigido |
| --- | --- | --- |
| Super Administrador | Crear empresa, sucursal, bodega, caja/terminal y usuarios; configurar roles; reiniciar | La configuración y permisos conservan lo guardado; cambios de rol o contraseña revocan la sesión correspondiente. |
| Administrador de una sucursal | Consultar y gestionar su operación; enviar URL/ID de otra sucursal | Acceso permitido dentro de su alcance y rechazo consistente fuera de él, incluidos organización, compras y finanzas. |
| Gerente | Cambiar entre dos sucursales asignadas, supervisar y autorizar | Selector funcional; reportes conciliados; permisos de supervisión aplicados; rechazo a una tercera sucursal no asignada. |
| Mesero | Abrir mesa, crear pizza con extra/exclusión, enviar, agregar línea y entregar | Mismo pedido en cocina y caja; datos y precios históricos conservados; sin necesidad de abrir una caja propia. |
| Cocina | Recibir varias líneas por estación, preparar parte, marcar listo, esperar pedidos nuevos y avisar | Estados reales y permisos correctos; no se detiene la actualización; aviso llega al destinatario si se ofrece como notificación. |
| Cajero / Punto de Venta | Abrir caja, cobrar el pedido anterior en Q50 efectivo y Q39 tarjeta, dividir cantidades y emitir comprobante | Una venta relacionada con el pedido; sin doble consumo; total, pagos, moneda, impuestos y caja coinciden. |
| Bodega | Recepción parcial, producción de 72 porciones de masa, traslado, conteo y caducidad según su responsabilidad | Cada movimiento afecta una sola ubicación; harina se consume una vez; permisos mínimos permiten las tareas asignadas. |
| Delivery y supervisor | Asignar motorista, registrar salida/entrega, cobrar tarifa, cancelar o reembolsar cuando corresponda | Trazabilidad de servicio, tarifa y cobro; restitución o merma según el estado; autorización válida y documentada. |
| Operador offline | Guardar reventa sin red; volver con otra cuenta/sucursal; sincronizar desde página y segundo plano | Sin datos ajenos en pantalla, sin recálculo silencioso, sin duplicados y con conflictos conservados para revisión. |
| Cierre y control | Arqueo, conciliación bancaria, corte X/Z y comparación de ambos centros de reportes | Pagos individuales, reembolsos, costo histórico, impuesto y saldo final concilian con documentos y contexto. |

El caso de compra → recepción → producción de masa → pizza → pedido con extra → venta/pago → cierre → reportes corresponde al caso integral del plan. Con R01 y los hallazgos de alcance abiertos no puede declararse aprobado.

## Evidencia y reproducibilidad

La compilación se ejecutó una vez:

```powershell
dotnet build SistemiPosRestaurante.slnx --no-restore -c Release -m:1 -nr:false -p:UseSharedCompilation=false
```

Las pruebas focalizadas abarcaron seguridad, caja, pedidos, ventas, reportes y resolución de configuración empresarial en UnitTests, y controles de fase 8 y modelos de pedidos/recetas en IntegrationTests. Los intentos dentro del entorno restringido no conectaron con `testhost` y ejecutaron cero casos. Las ejecuciones posteriores fuera de esa restricción aprobaron 92 y 24 casos; únicamente estas se contabilizan como aprobadas. No se ejecutó toda la suite ni pruebas de navegación.

- [review-unit-validated.trx:570](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/artifacts/revision-general/review-unit-validated.trx:570>): 92 aprobadas, 0 fallidas, 0 omitidas.
- [review-components-validated.trx:162](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/artifacts/revision-general/review-components-validated.trx:162>): 24 aprobadas, 0 fallidas, 0 omitidas.
- [coherence-evidence.json:340](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/artifacts/revision-general/coherence-evidence.json:340>): 12 observaciones confirmadas de contratos y simulaciones aisladas; matriz inicial de ocho roles.
- [verify-coherence.mjs:6](<C:/Users/Elizabeth/Downloads/sistema rest/sistemi-pos-restaurante/artifacts/revision-general/verify-coherence.mjs:6>): script reproducible sin red, navegador ni base de datos.

El script de contratos es una herramienta de diagnóstico y no sustituye pruebas automatizadas de rechazo de acceso, persistencia o flujo completo. La revisión no cambió código de la aplicación, datos, permisos ni migraciones. Se añadieron este informe y los archivos de evidencia.
