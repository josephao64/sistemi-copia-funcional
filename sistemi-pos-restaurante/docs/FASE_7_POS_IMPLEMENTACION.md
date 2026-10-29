# Fase 7: diagnóstico y avance de implementación

## Integración con la rama remota `dev`

Esta sección describe el estado actual y reemplaza las afirmaciones de avance del diagnóstico inicial conservado debajo. La integración no certifica que toda la fase 7 esté terminada.

- Se conservan los módulos de recetas, producción, restaurante, cocina, mesas y delivery recibidos de `dev`.
- El flujo POS de `/pedidos` utiliza `PosOrders` y `PosOrderLines`; el restaurante de `/restaurante` conserva `RestaurantOrders` y `RestaurantOrderLines`. No se copian ni reinterpretan pedidos históricos entre ambos módulos. El cobro del flujo POS no convierte automáticamente pedidos del módulo Restaurante.
- Los movimientos POS usan `PosOrderConsumption = 14`. Se conservan los valores de producción y restaurante ya definidos en `dev`; el ledger distingue sus orígenes al devolver existencias.
- La captura POS acepta las versiones `RecipeSnapshot` de la fase 5 y conserva las versiones específicas de subrecetas referidas por la versión publicada. La fórmula expandida se guarda en el pedido y no se recalcula al cobrar.
- El cobro POS conserva cantidades vendidas, pagos mixtos, división de cuenta, pagos configurados, devoluciones y reembolsos por pago original; mantiene el flujo de caja y los permisos de sucursal. Los preparados del flujo POS se cobran desde el pedido preparado.
- Se conservan las reservas de stock y lotes del restaurante: el consumo POS no puede utilizar existencias reservadas.
- Las migraciones POS aún no desplegadas se ajustaron para crear tablas propias. `20261020225314_IntegratePhaseSevenPosWithRestaurant` completa moneda histórica de caja, terminal de venta, idempotencia de devoluciones/reembolsos y precisión de cantidades. No se ejecutaron migraciones en la base del negocio durante la integración.

### Límites pendientes

Quedan pendientes la unificación del cobro de pedidos de Restaurante con POS, la revisión del protocolo de sincronización del service worker frente al ticket firmado y la separación de su caché por identidad/sucursal. También debe reforzarse el vínculo entre autorización de descuento y pedido específico. No dar por certificado el funcionamiento offline completo ni la aceptación visual en PC, tableta y teléfono.

Las pruebas de persistencia se ejecutan en bases PostgreSQL temporales que se eliminan al finalizar. La prueba de producción verifica producción y planificación histórica de recetas; el cobro de preparados se verifica por el flujo POS de pedidos, conforme a las reglas aprobadas de la fase 7.

### Revisión manual tras desplegar las migraciones

Comprobar que `/restaurante` conserva sus pedidos, cocina y delivery. En `/pedidos`, crear una terminal compatible con la caja y bodega, confirmar y preparar un pedido, cobrarlo con pagos mixtos y revisar inventario, devolución, reembolso y corte. No capturar la misma operación como pedido independiente en ambos módulos.

### Referencias Git en Windows

El remoto contiene ramas distintas `Dev` y `dev`. Para integrar exactamente la segunda sin colisión de referencias locales, se utilizó `git fetch origin refs/heads/dev:refs/remotes/origin/integration-dev`. La publicación debe apuntar explícitamente a `HEAD:refs/heads/dev`.

## Diagnóstico inicial conservado como referencia

Fecha: 20 de octubre de 2026. Esta entrega es parcial; no certifica la fase 7 completa.

## Actualización: reglas aprobadas y bloque inicial implementado

El usuario aprobó expresamente las diez reglas de `FASE_7_REGLAS_PROPUESTAS.md`. Se implementó el primer bloque de dependencias; las secciones de diagnóstico inicial que siguen se conservan como referencia del punto de partida.

### Cambios integrados

- Terminal operativa diferenciada de POS bancario, con sucursal, código de caja y bodega activa compatible. La creación de pedidos valida caja activa del usuario, sucursal y código de caja de la terminal.
- Pedidos en borrador y confirmación con captura de precios e impuestos desde el servidor y fórmula histórica JSONB, conservando subrecetas expandidas. Confirmar vuelve a consultar el catálogo; el precio provisional del borrador no se toma como histórico definitivo.
- Preparación, finalización y despacho por cantidades parciales, con permisos separados. Cancelación de cantidades pendientes o en preparación/preparadas con motivo y registro de cantidades consumidas; no cancela cantidades ya despachadas ni genera entradas automáticas.
- Consumo de ingredientes y empaques de receta al iniciar preparación; consumo de reventa al despachar. Finalización/despacho de preparados no repite el consumo. Cada salida tiene FK al evento y un índice único evento/producto.
- Transacción serializable, control `xmin` del agregado y stock, historial persistente y reintentos idempotentes. Un identificador reutilizado con otro tipo, línea, cantidad o motivo se rechaza. Las fallas limpian el seguimiento y revierten la transacción.
- Existencias, movimientos, saldos y asignaciones de lotes con seis decimales y `numeric(22,6)`; se conserva la capacidad entera anterior. Se reutiliza el ledger FIFO/FEFO.
- Interfaz Razor de pedidos con acceso en Ventas, alta de terminal mediante modal existente, captura de varias líneas, acciones e historial. No se hicieron pruebas visuales ni de navegación.

### Archivos y endpoints principales

- `src/SistemiPos.Domain/Orders/`: terminal, agregado de pedido, líneas y eventos.
- `src/SistemiPos.Application/Orders/`: contratos, autorización, captura/expansión de receta y coordinación transaccional.
- `src/SistemiPos.Infrastructure/Orders/OrderRepository.cs`, configuraciones EF, `AppDbContext`, registro DI y permisos.
- `src/SistemiPos.Web/Controllers/OrdersController.cs`, modelos, `Views/Orders`, `orders.js` y navegación existente.
- Entidades/configuraciones de existencias, movimientos y lotes: precisión de cantidades y vínculo al evento.
- GET `/pedidos`, GET/POST `/pedidos/nuevo`, GET `/pedidos/{id}`, POST `/pedidos/{id}/operaciones`, GET/POST `/pedidos/terminales/nueva`. Las mutaciones MVC requieren antiforgery; los permisos y reglas críticas se validan en Application.

Se reutilizan `restaurant.orders.view`, `.create`, `.update` y `kitchen.orders.status`; se agregan `restaurant.orders.dispatch`, `restaurant.orders.cancel` y `pos.terminals.manage`. Deben asignarse a los usuarios responsables mediante la administración existente.

### Persistencia y despliegue

Migración PostgreSQL generada: `20261020205151_AddRestaurantOrderWorkflow`. Crea terminales, pedidos, líneas y eventos; agrega la referencia del movimiento al evento, índices/FK y amplía la escala de cantidades sin reducir los dígitos enteros. No se aplicó a una base real. Su reversión elimina las tablas nuevas y reduce la escala: no usar Down después de registrar pedidos o consumos fraccionarios que deban conservarse.

Aplicación mediante el procedimiento existente: `dotnet ef database update --project src/SistemiPos.Infrastructure --startup-project src/SistemiPos.Web`, usando la conexión del entorno correspondiente. No operar el nuevo módulo antes de aplicar la migración.

### Validación del bloque

- Compilación de la solución y de los proyectos afectados: correcta.
- Pruebas unitarias filtradas de pedidos, ventas e inventario por lotes: 30 aprobadas.
- Pruebas de modelo EF de pedidos y recetas: 2 aprobadas; incluyen consistencia del snapshot de migraciones existente.
- 3 pruebas PostgreSQL omitidas por ausencia de `SISTEMI_TEST_CONNECTION`: dos nuevas de pedidos (persistencia/rollback/idempotencia y preparación concurrente del último ingrediente) y una existente de ledger/concurrencia. No se certifica persistencia, migración o concurrencia real con resultados de modelo.

### Revisión manual y límites actuales

Después de aplicar la migración, asignar permisos y seleccionar sucursal: crear una terminal con el código exacto de una caja abierta y su bodega; crear pedido con preparado y reventa; confirmar; preparar una parte; finalizarla y despacharla. Verificar Kardex, lotes y cantidades. Cambiar la receta después de confirmar y comprobar que el pedido mantiene su fórmula. Probar falta de stock, caja/sucursal incompatible, cancelación con motivo y permisos insuficientes.

Este bloque ya consume inventario. No combinarlo con una venta directa del mismo preparado o reventa despachada: la conversión a venta todavía no está conectada y podría producir un segundo descuento. Usarlo para validación controlada hasta completar ese enlace.

Pendientes de fase 7: conversión parcial a ventas con asignaciones persistentes y conservación del histórico (POS-02); modificadores, revisiones posteriores a confirmar y empaques adicionales; pagos mixtos (POS-05); división de cuenta (POS-06); integración de descuentos de pedido (POS-07); restricciones de sesiones por terminal y cortes X/Z/cierre ciego (POS-08/09); devoluciones físicas separadas del reembolso y del pedido (POS-10); aislamiento offline por ubicación y restricción efectiva a reventa (POS-11); integración táctil final (POS-12). No se declaran completos esos criterios.

## Base técnica y dependencias

Se trabaja sobre `src/` y `tests/` de la solución raíz: .NET 10, MVC/Razor, Application/Domain/Infrastructure/Web, EF Core y PostgreSQL. No se modifica la copia anidada `sistemi-pos-restaurante` ni sus cambios locales.

Al iniciar el diagnóstico existían ventas directas, recetas versionadas, existencias por bodega, caja, aprobaciones de descuentos, devoluciones y código offline, sin pedidos de restaurante, preparación o despacho. El bloque descrito arriba incorpora sus entidades y transiciones iniciales. Los pedidos de compra no sustituyen ORD-02, ORD-07 ni ORD-10.

Decisión expresa del usuario: se autorizan ventas offline exclusivamente en POS y se incluyen las dependencias de pedidos, preparación y despacho. Las diez reglas de `FASE_7_REGLAS_PROPUESTAS.md` ya están aprobadas; AGENTS.md y DEVELOPMENT_PLAN.md reflejan el alcance.

## Diagnóstico inicial por historia — previo al bloque de pedidos

### POS-01 — Terminal y bodega de consumo

Antes: la caja conserva sucursal y código; ventas resuelve una bodega activa por sucursal, con respaldo al contexto si no encuentra una. `PosDevice` representa un dispositivo de cobro con tarjeta; no debe asumirse que equivale a una terminal de venta.

Después: `SaleService.CreateSaleAsync` rechaza una caja de otra sucursal operativa antes de procesar incluso un reintento. Si la caja tiene sucursal, exige una bodega activa de esa misma sucursal y no permite el respaldo a otra bodega. La venta continúa conservando `WarehouseId`.

Archivos: `src/SistemiPos.Application/Sales/SaleService.cs` y `tests/SistemiPos.UnitTests/Sales/SaleServiceTests.cs`. Integración existente: `SalesController.Create`, POST `/pos`; sin endpoints nuevos, dependencias nuevas ni migraciones.

Pruebas: dos casos nuevos cubren caja de otra sucursal y bodega ausente/incompatible, comprobando que no se creen ventas ni movimientos, no cambie stock y no se invoque SaveChanges. Los 17 casos de SaleServiceTests pasan. Criterio parcialmente cubierto: falta modelar/configurar la terminal de venta y su vinculación explícita con caja y bodega.

### POS-02 — Venta desde pedido

Antes y después: existe venta directa con precios e información monetaria histórica, pero no pedido de restaurante ni vínculo pedido/venta. Pendiente implementar primero ORD-02 y la captura histórica de recetas y modificadores; después conversión transaccional y unicidad por pedido. No verificado ni implementado en esta entrega.

### POS-03 — Consumo por tipo

Antes y después: la venta descuenta el producto; se rechazan ingredientes para venta directa. Existen recetas, pero no coordinación de consumo por preparación, despacho, empaque o venta. Pendiente definir el evento de consumo de cada tipo y desarrollar sus dependencias antes de conectar movimientos. No se considera implementado.

### POS-04 — Concurrencia de inventario

Antes y después: `InventoryStockConfiguration` configura versión de concurrencia y unicidad producto/bodega; `SaleConfiguration` tiene índice único de ClientOperationId. Son mecanismos existentes, no evidencia de aceptación de toda la historia. Pendiente prueba con dos conexiones PostgreSQL vendiendo el último producto, rollback integral y reintentos idempotentes. No se ejecutó prueba concurrente real.

### POS-05 — Pagos mixtos

Antes y después: `Sale.Payment` y `CreateSaleRequest.Payment` admiten un pago individual. Pendiente evolucionar a colección de pagos con migración de históricos, suma exacta, cambio solo sobre efectivo y adaptación de caja, comprobantes, reportes y reembolsos. No implementado ni verificado.

### POS-06 — División de cuenta

Antes y después: no hay particiones de cuenta ni asignación de pagos a ellas. Depende de POS-05 y ORD-07. Pendiente precisar reparto de cantidades, centavos, descuentos/impuestos y personas; luego persistencia y prevención de cobros duplicados. No implementado.

### POS-07 — Descuentos autorizados

Antes y después: `SaleService` usa permisos, `DiscountApprovalRequest`, validación de aprobación y auditoría. Los casos existentes del servicio forman parte de las 17 pruebas aprobadas; esto no certifica el alcance por pedido o terminal. Pendiente conectar pedidos y alcance de ubicación y verificar autorizaciones de extremo a extremo.

### POS-08 — Caja por sucursal

Antes y después: `CashSession`, `CashService` y `CashController` ofrecen apertura, responsable, sucursal, movimientos y estado. Pendiente vinculación de terminal de venta y garantía concurrente de restricciones de sesiones activas. No se declara completa ni se ejecutaron pruebas nuevas de caja.

### POS-09 — Cierre ciego y cortes

Antes y después: existen arqueos por denominación, cierre de dispositivos de tarjeta, esperado/declarado, diferencias, conciliación y protección de caja cerrada. No se ha certificado confidencialidad del esperado antes de declarar, cortes X/Z ni autorización completa de diferencias. Pendiente contrastar esos flujos con la política aprobada y probarlos.

### POS-10 — Anulaciones y devoluciones

Antes y después: `SalesController.Cancel/Return/Refund` y `SaleService` contienen operaciones y auditoría existentes. Pendiente separar sus efectos de pedido, preparación e inventario según ORD-10, evitando restituir ingredientes ya preparados. Las pruebas de SaleService no sustituyen la verificación de esa dependencia ausente.

### POS-11 — Offline por ubicación

Antes y después: existen `OfflinePosController`, vistas, identificadores de operación y sincronización. Se autorizó expresamente offline exclusivamente en POS y se resolvió la contradicción documental. El código no cambia en esta actualización. Pendiente verificar aislamiento por ubicación, límites, política de stock/conflictos y reconexión; también definir su interacción con pedidos y preparación. No certificado.

### POS-12 — Interfaz táctil final

Antes y después: se conserva `/pos` y la interfaz Razor existente. Pendiente integrar pedidos, modificadores, pagos mixtos, división y terminales una vez completos los casos de uso. No se realizaron verificaciones visuales ni de navegación; la revisión manual corresponde al usuario.

## Validación inicial — previa al bloque de pedidos

`dotnet test tests/SistemiPos.UnitTests/SistemiPos.UnitTests.csproj --no-restore --filter FullyQualifiedName~SaleServiceTests --verbosity minimal`

Resultado: 17 aprobadas, 0 fallidas, 0 omitidas. Compiló Domain, Application y UnitTests. No se ejecutaron suite completa, migraciones, servidor web, PostgreSQL real ni pruebas visuales. No se cambió el esquema de base de datos.

## Revisión manual y decisiones necesarias

- Probar venta directa válida en una caja con bodega activa de su sucursal.
- Comprobar que una caja de otra sucursal y una sucursal sin bodega activa rechazan la venta sin registrar movimientos.
- Revisar configuraciones antiguas: las cajas con sucursal requieren ahora bodega activa; corregir su configuración antes de operar.
- Offline exclusivo del POS y las dependencias ORD/preparación están autorizados; las reglas de consumo, restitución, estado vendible y división de cuenta de `FASE_7_REGLAS_PROPUESTAS.md` fueron aprobadas. Los pendientes de implementación actuales se enumeran al inicio del documento.
