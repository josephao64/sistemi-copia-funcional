# Fase 6 — Implementación y guía de uso

Actualización de interfaz: [diseño y operación de las pantallas](FASE_6_DISENO_Y_OPERACION.md). Incluye la estructura visual, los diálogos de acciones y los recorridos manuales del rediseño.

Reporte explicativo y procedimientos para el personal: [Manual de usuario de Fase 6](FASE_6_REPORTE_Y_MANUAL_DE_USUARIO.md). También disponible en [Word editable](FASE_6_REPORTE_Y_MANUAL_DE_USUARIO.docx).

Entrega local: 19 de octubre de 2026. Alcance: `ORD-01` a `ORD-10` y recomendaciones del [plan aprobado](FASE_6_PEDIDOS_COCINA_DELIVERY_IMPLEMENTACION.md).

El código y la migración están incorporados. La base activa no se migró durante esta entrega y la sesión abierta del POS no se reinició. La aceptación funcional y visual corresponde al usuario.

## 1. Funcionalidades incorporadas

| Clave | Implementación | Acceso |
|---|---|---|
| ORD-01 | Canal independiente de salón/llevar/delivery; sucursal y bodega; referencia externa única por canal/sucursal. | Nuevo pedido y configuración. |
| ORD-02 | Borrador editable, secuencia `PED-00000001`, envío, revisiones, estados por línea, eventos e idempotencia. | Pedidos y detalle. |
| ORD-03 | Menú por sucursal con secciones propias de la carta, fotos, favoritos, precio local/promoción y disponibilidad base descontando reservas. | Nuevo pedido. |
| ORD-04 | Grupos con mínimos/máximos; opciones con precio y consumo adicional en unidad base. | Configuración y selección de productos. |
| ORD-05 | Exclusiones autorizadas de componentes directos en recetas elaboradas al pedido; sin rebaja automática de precio. | Configuración y selección de productos. |
| ORD-06 | Combos con grupos, elecciones, vigencia, extras y exclusiones; promociones con prioridad; importes y consumo históricos. | Configuración y captura. |
| ORD-07 | Áreas/mesas, un turno abierto por mesa, pedidos del turno, traslado a mesa libre y liberación operativa auditada. | Mesas y detalle del pedido. |
| ORD-08 | KDS por estación/canal, tiempos, preparación total o parcial, cambios recientes y refresco de 15 segundos. | Cocina. |
| ORD-09 | Zona/tarifa, dirección/contacto, motorista/ruta, asignación, salida, incidencia y entrega con actor/fecha. | Delivery y detalle. |
| ORD-10 | Reserva al enviar; consumo al preparar; liberación pendiente; merma de lo preparado; reintegro explícito de SKU intacto elegible. | Detalle, cocina, inventario e historial. |

## 2. Activación

1. Reiniciar la aplicación usando una compilación con estos cambios. La instancia que estaba abierta conservaba bloqueados los archivos de `bin/Debug`; la compilación técnica se realizó en `Phase6Verification`.
2. El arranque existente en `Program.cs` ejecuta `Database.MigrateAsync()` y actualiza los permisos, excepto bajo el entorno `Testing`. Por ello, al iniciar la nueva versión se aplicará `20261019170538_AddRestaurantOrdersKitchenDelivery` en la conexión configurada.
3. Iniciar sesión nuevamente si el usuario conserva permisos anteriores en la sesión.
4. Seleccionar la sucursal de trabajo y abrir **Restaurante → Configuración**.
5. Crear canales y estaciones; guardar. Después configurar menú, mesas y delivery según el paso siguiente.

La migración agrega ocho tablas (`RestaurantSetups`, `RestaurantOrders`, `RestaurantOrderLines`, `RestaurantEvents`, `RestaurantCommands`, `RestaurantTableTurns`, `OrderReservations`, `OrderWaste`), una secuencia, `InventoryStocks.ReservedQuantity` y `InventoryMovements.OriginalMovementId`. Incluye claves foráneas, restricciones de cantidades, referencia externa única, comando único y turno activo único. No transforma ventas, órdenes de compra ni órdenes de producción anteriores. No se ejecutó `database update` contra la base activa durante el desarrollo.

## 3. Configuración inicial

### Catálogo e inventario

- Los productos deben estar activos, tener unidad base activa y estar habilitados/visibles en la sucursal. Elegir variantes concretas, no el producto padre que administra variantes.
- Los preparados requieren receta activa. Las recetas al pedido consumen sus componentes; las de producción consumen el SKU terminado, sin volver a explotar sus ingredientes.
- La bodega elegida debe pertenecer a la sucursal. Registrar stock real y lotes de productos con vencimiento antes de enviar pedidos.
- Para artículos con control de lote y sin vencimiento, el faltante de lotes de saldo inicial se registra con el mismo criterio del ledger existente al reservar. Si hay diferencias incompatibles, se exige conciliación.

### Configuración del restaurante

1. **Canales:** por ejemplo Propio y PedidosYa. Marcar como plataforma cuando corresponda. La captura de pedidos externos es manual.
2. **Estaciones:** por ejemplo Cocina, Barra y Despacho. Cada producto de menú debe tener una.
3. **Menú:** seleccionar producto, estación, favorito y orden. El producto conserva categoría, imagen y precio del catálogo de sucursal.
4. **Opciones:** crear grupo con mínimo/máximo y opciones. Para extra queso de 40 g, registrar `40` si su unidad base es g, o `0.040` si es kg y admite esa precisión. El precio del extra se configura por unidad vendida.
5. **Exclusiones:** seleccionar únicamente componentes directos de la receta elaborada al pedido. No se permiten exclusiones de un producto ya producido.
6. **Combos:** establecer precio base, ventana de vigencia y grupos de selección. Los productos deben estar en el menú. Sus opciones/exclusiones también se validan; los extras se suman al precio del combo.
7. **Promociones:** porcentaje, vigencia y prioridad. Se aplica una por producto: prioridad mayor, luego porcentaje mayor, y finalmente ID como desempate estable. No se acumula con un precio de combo.
8. **Mesas:** área, nombre y estado. No se puede retirar una mesa ocupada ni desactivar una estación con trabajo abierto.
9. **Delivery:** zonas y tarifas, motoristas activos. La ruta es texto operativo.

Guardar conserva una revisión de configuración. Dos administradores con formularios abiertos no pueden sobrescribirse silenciosamente; el segundo debe recargar si cambió la revisión.

## 4. Flujo operativo

### Capturar y enviar

1. Abrir **Nuevo pedido**; elegir canal, servicio y bodega. Salón exige mesa; delivery exige dirección, teléfono y zona.
2. Agregar productos o combos y sus selecciones. La disponibilidad mostrada es la de la composición base; opciones y exclusiones se comprueban al enviar.
3. Guardar borrador y revisar el total que calcula el servidor, incluida la tarifa. Antes del envío se pueden editar productos, ubicación, contacto y servicio.
4. Pulsar **Enviar a cocina y reservar**. Si cambió receta, precio, promoción o tarifa, actualizar y revisar el borrador. Si falta stock/lote, la operación se rechaza completa.
5. Una vez enviado, agregar productos desde **Agregar productos como nueva revisión**. Las líneas anteriores conservan sus snapshots. Para quitar cantidades, cancelar con motivo.

La referencia externa no se puede reutilizar en otro pedido del mismo canal/sucursal; se informa el número existente. Los reintentos de un comando con igual identificador y contenido devuelven el resultado previo; cambiar su contenido usando el mismo identificador se rechaza.

### Cocina y preparación parcial

1. Filtrar por estación/canal en **Cocina**. La sucursal es la del contexto de trabajo.
2. Pulsar **Preparar cantidad**. La cantidad por defecto es toda la línea.
3. Si se prepara una parte, la línea original permanece como `Split`, y se crean dos líneas hijas: preparada y pendiente. Se distribuyen importes exactos y las mismas reservas sin duplicarlas. Una división que no respete las unidades de componentes se rechaza.
4. Al iniciar se descuenta físicamente la reserva con movimiento `OrderConsumption`. Marcar **Listo** no produce otra salida.
5. El pedido queda listo cuando todas sus líneas activas están listas. Confirmar entrega conserva la hora de entrega por línea.

KDS muestra adiciones y cancelaciones recientes y enlaza al historial completo. Los tiempos se calculan desde marcas del servidor. Si falla una acción, se muestra el error y el refresco automático se detiene para conservarlo.

### Mesas

- El primer borrador de salón abre el turno. Pedidos posteriores de esa mesa se vinculan al mismo turno.
- **Cambiar mesa** mueve el turno completo y sus pedidos a una mesa libre de la misma sucursal; exige motivo y permiso. El origen/destino quedan en el evento.
- **Liberar mesa** exige que todos los pedidos del turno estén entregados o cancelados. Es liberación operativa; el cierre financiero de la cuenta se integrará en Fase 7.

### Delivery

- Asignar motorista activo y ruta cuando el pedido esté listo.
- Registrar salida para pasar a **En ruta**.
- Registrar incidencia cuando falla un intento; el pedido sigue en ruta y no aparece entregado.
- Confirmar entrega una vez realizada. Cambiar zona/tarifa se permite en borrador; después del envío queda congelada.

### Cancelación, merma y retorno intacto

| Situación | Resultado físico |
|---|---|
| Cancelación de borrador | Total activo cero; no hubo reserva ni consumo. |
| Cancelación de cantidad pendiente | Libera la reserva correspondiente; sin salida ni merma. |
| Cancelación de cantidad preparada | Conserva la salida y registra merma con costo, motivo y lotes/movimientos originales. |
| Cancelación de SKU intacto elegible | Reintegra físicamente al stock/lote original mediante `OrderReturn`; no lo clasifica como merma. |

El retorno intacto requiere **cancelar pedidos** y **ajustar inventario**. Solo está habilitado para una línea consumida de un SKU sin receta ni componentes extra. Se rechazan lotes vencidos y líneas de varios lotes cuyo retorno físico no puede identificarse desde esta pantalla. Una devolución posterior a entrega/cobro corresponde al flujo financiero y de devoluciones de Fase 7.

## 5. Permisos

| Capacidad | Permiso |
|---|---|
| Ver/capturar/editar/enviar | `restaurant.orders.view/create/update` |
| Configurar restaurante | `restaurant.configure` |
| Cancelar/registrar merma | `restaurant.orders.cancel` |
| Confirmar entrega | `restaurant.orders.deliver` |
| Mesas | `restaurant.tables.view/manage` |
| Cocina | `kitchen.orders.view/status` |
| Asignación/salida/incidencias delivery | `restaurant.delivery.manage` |
| Reintegro de SKU intacto | `restaurant.orders.cancel` y `inventory.adjust` |
| Consultar contrato de cobro | `restaurant.orders.view` y `sales.manage` |

Administrador recibe las capacidades operativas nuevas. Mesero conserva captura/mesas y puede entregar; cocina conserva consulta y estados de preparación. Punto de Venta/Cajero pueden consultar pedidos. Los permisos específicos pueden asignarse desde la administración existente; todas las mutaciones vuelven a comprobar permiso y sucursal en backend.

## 6. Archivos principales

- Dominio: `src/SistemiPos.Domain/Restaurant/RestaurantSetup.cs`, `RestaurantOrder.cs`.
- Aplicación: `src/SistemiPos.Application/Restaurant/RestaurantService.cs`, `RestaurantPricing.cs`, `RestaurantPreparation.cs`, `RestaurantContracts.cs`.
- Persistencia: `src/SistemiPos.Infrastructure/Restaurant/RestaurantRepository.cs`, `Persistence/Configurations/RestaurantConfiguration.cs`, `Persistence/AppDbContext.cs` y migración `20261019170538_AddRestaurantOrdersKitchenDelivery`.
- Inventario compartido: `InventoryStock`, `InventoryMovement`, `InventoryMovementType`, `InventoryLotAllocation`, `InventoryLotLedger`.
- MVC: `src/SistemiPos.Web/Controllers/RestaurantController.cs`, `Views/Restaurant`, `wwwroot/js/restaurant.js`, `restaurant-config.js`, `wwwroot/css/restaurant.css`.
- Accesos: `PermissionCodes`, `SecurityBootstrapper`, `AccountController`, `Views/Shared/_Layout.cshtml`.

## 7. Contrato para Fase 7

`GET /restaurante/{id}/checkout` entrega ID y revisión, sucursal/bodega, tarifa, total y líneas activas con snapshot, cantidad activa, indicador de consumo y movimientos originales. Está disponible para pedidos enviados y activos, aunque la cocina aún no haya terminado.

Esta lectura no cobra, no consume reservas y no crea una venta. La implementación de Fase 7 debe:

1. Releer pedido y revisión dentro de la transacción de venta.
2. Vincular venta y líneas al pedido con unicidad/idempotencia del cobro.
3. Respetar cantidades activas y prorrateo del snapshot en cancelaciones parciales; conservar tarifa, descuentos, impuestos y agrupación `ComboSequence`.
4. Reconocer movimientos ya consumidos. Para líneas todavía reservadas, consumir exactamente una vez en coordinación con cocina.
5. Integrar pagos/anulaciones/devoluciones y el cierre financiero de mesa.

Hasta esa integración, volver a capturar estos mismos productos como una venta directa ejecutaría el flujo anterior de inventario. No es un mecanismo de cobro de pedidos de Fase 6.

## 8. Aceptación manual pendiente

Validación técnica realizada sobre la entrega:

- Compilación de `SistemiPos.Web.csproj` y dependencias con `--no-restore -c Phase6Verification -m:1 -nr:false -p:UseSharedCompilation=false`: **0 errores, 0 advertencias**.
- `node --check` de `restaurant.js` y `restaurant-config.js`: sin errores de sintaxis.
- Generación de migración EF completada; `migrations has-pending-model-changes --no-build` confirmó que no hay diferencias pendientes entre modelo y migración.
- `git diff --check`: sin errores de formato detectados.
- No se ejecutaron pruebas automatizadas ni navegación de interfaz durante este cierre. Los resultados anteriores no sustituyen la aceptación funcional ni validan el comportamiento contra PostgreSQL en ejecución.

| Caso | Resultado que debe comprobarse |
|---|---|
| Pedido de salón con pizza y bebida | Cada línea va a su estación; la bebida lista no termina la pizza. |
| Extra de queso y exclusión de cebolla | Total y consumo correctos; solo se acepta exclusión configurada. |
| Dos combos iguales con selecciones distintas | Agrupación independiente, extras correctos y suma exacta en centavos. |
| Promoción o tarifa modificada tras crear borrador | Se exige actualizar y revisar antes de enviar; pedido enviado conserva valores anteriores. |
| Dos operadores sobre último stock/mesa/pedido | No hay sobreventa, turnos duplicados ni sobrescritura; el conflicto muestra un error recuperable. |
| Preparar 1 de 3 unidades | Una línea preparada y otra pendiente; mismo total y reservas consistentes. |
| Cancelar pendiente y luego preparada | La primera libera reserva; la segunda registra merma sin devolver ingredientes ni descontar de nuevo. |
| Retorno de bebida intacta de un lote | Reversión ligada al consumo/lote, permiso y motivo requeridos. |
| Delivery con intento fallido | Incidencia con fecha/actor; sigue en ruta hasta confirmación real. |
| Reenvío tras pérdida de conexión | Un solo comando, reserva y consumo. |
| Usuario de otra sucursal o sin permiso | Consulta/mutación bloqueada en backend. |
| Venta o traslado directo mientras hay reservas | No puede utilizar stock o lotes comprometidos en pedidos. |

Las verificaciones de concurrencia real, rollback PostgreSQL, migración sobre históricos y navegación no se han ejecutado contra la base activa. No se ha validado rendimiento con un historial grande de pedidos; las consultas actuales de listas cargan los pedidos de la sucursal.
