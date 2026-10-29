# SISTEMI POS — Documento de implementación de Fase 6
## Pedidos, cocina y delivery (`ORD-01` a `ORD-10`)

**Fuente de alcance:** tabla de Fase 6 entregada por el usuario el 19 de octubre de 2026.  
**Objetivo:** separar pedido, preparación, entrega y venta para operar un restaurante real.  
**Base técnica revisada:** rama local `dev`, commit `74975a3`; esta referencia no confirma el estado remoto actual.  
**Estado:** código incorporado al proyecto local. La activación de la migración y la aceptación funcional están pendientes. Consultar [guía de implementación y uso](FASE_6_IMPLEMENTACION_Y_USO.md) para archivos, configuración, límites y comprobaciones manuales.

## 1. Plan de implementación

1. **Normalizar alcance y vocabulario.** Incorporar `ORD-01` a `ORD-10` en `REQUIREMENTS.md`, `BUSINESS_RULES.md`, `ARCHITECTURE.md` y `DEVELOPMENT_PLAN.md`. La palabra *pedido* de esta fase significa pedido de cliente, no orden de compra ni orden de producción.
2. **Crear configuración operativa.** Canales, tipos de servicio, menú por sucursal, estaciones de cocina, zonas/tarifas de entrega, áreas/mesas y permisos.
3. **Modelar pedido y comanda.** Identidad, numeración, líneas inmutables por versión, estados, historial, concurrencia e idempotencia.
4. **Integrar catálogo y recetas.** Modificadores, exclusiones, combos y promociones con precio y consumo calculados en servidor y snapshots históricos, incluida la regla aplicada y su vigencia.
5. **Implementar reserva, preparación y merma.** Reservar por bodega/lote al enviar el pedido; consumir una sola vez al iniciar la preparación o al entregar según el tipo de línea; distinguir cancelación temprana de desperdicio real.
6. **Implementar operación.** Mesas y turnos, KDS por estación con revisiones explícitas e idempotencia, delivery con incidencias, asignación y tiempos, y flujo de entrega. Registrar métricas operativas de cola, preparación y entrega.
7. **Conectar con ventas.** Exponer un contrato de pedido listo para cobrar en Fase 7 sin descontar nuevamente el inventario consumido en Fase 6.
8. **Validar aceptación.** Revisar cada clave `ORD-01` a `ORD-10`, permisos, sucursales, concurrencia, históricos y migraciones.

**Secuencia recomendada:** `ORD-01/03` → `ORD-02` → `ORD-04/05/06` → reservas y merma → `ORD-07/08/09` → `ORD-10` → integración con Fase 7. El KDS no debe construirse antes de tener transiciones e identificadores estables de pedido y línea.

## 2. Alcance de la imagen y resultado esperado

| Clave | Alcance requerido | Resultado verificable |
|---|---|---|
| `ORD-01` | Canales y tipos de servicio: salón, llevar, delivery y plataformas. | Canal y ubicación quedan guardados en el pedido y su historial. |
| `ORD-02` | Pedido y comanda con estados, líneas y numeración segura. | Transiciones válidas y trazables desde creación hasta entrega. |
| `ORD-03` | Menú por sucursal con categorías, productos, fotos y favoritos. | Disponibilidad y precio correctos por sucursal. |
| `ORD-04` | Grupos de modificadores y extras con precio y consumo adicional. | El extra guarda precio y componentes históricos. |
| `ORD-05` | Exclusiones de componentes permitidos. | Solo se excluyen componentes configurados; el consumo se ajusta. |
| `ORD-06` | Combos y promociones con selección y vigencia. | Precio y consumo se desglosan históricamente. |
| `ORD-07` | Áreas, mesas, apertura y cambio controlado. | Una mesa no recibe dos cuentas abiertas incompatibles. |
| `ORD-08` | KDS por estación y medición de tiempos. | Cada estación ve sus líneas y cambios auditados. |
| `ORD-09` | Delivery con zona, tarifa, motorista, ruta y entrega. | Asignación, tarifa y tiempos quedan registrados. |
| `ORD-10` | Cancelación y desperdicio separados por reserva y preparación. | Inventario solo se revierte o consume según el estado físico real. |

La tabla original asigna prioridad crítica a `ORD-02`, `ORD-04` y `ORD-10`; esos tres flujos son el núcleo transaccional. `ORD-03/05/06/07/08/09` se apoyan en ellos. Las estimaciones de horas de la imagen son orientativas; verificar complejidad real tras el diseño de reservas y migraciones.

## 3. Estado inicial del repositorio y puntos de integración

Este diagnóstico corresponde al inicio del trabajo; los elementos que faltaban se incorporaron en la entrega descrita en la guía de uso.

- **Existe:** catálogo `Product` y configuración `ProductBranchSetting` con precio, habilitación y visibilidad por sucursal; recetas versionadas y `IRecipeOperationsService.PlanSaleAsync`; inventario por bodega/lote; permisos `restaurant.orders.*`, `restaurant.tables.*` y `kitchen.orders.*`; bodega marcada `IsKitchen`.
- **No se encontró:** entidad operativa de pedido de cliente, comanda/KDS, área/mesa/turno, delivery, modificadores, combos, reserva de stock ni movimiento de desperdicio. `PurchaseOrder` y `ProductionOrder` pertenecen a otros módulos.
- **Regla anterior a actualizar:** `REQUIREMENTS.md` enumera pedidos y promociones como fuera del MVP. La Fase 6 entregada por el usuario los incluye ahora. Conservar las reglas históricas que no choquen con esta ampliación.
- **Dependencia con Fase 7:** `SaleService` actual consume inventario al confirmar una venta. Cuando una línea de pedido haya consumido en preparación/entrega, la venta futura debe reconocer ese consumo y evitar una segunda salida. La integración se describe en la sección 9.

## 4. Reglas de dominio recomendadas para completar el alcance

Las diez claves de la imagen y las reglas complementarias de esta sección forman el alcance de implementación. Estas últimas completan los detalles operativos necesarios para que las claves funcionen juntas; documentarlas en `BUSINESS_RULES.md` junto con la fase.

### 4.1 Canal, servicio y origen

- **Canal** indica origen comercial: propio o plataforma externa identificada. **Tipo de servicio** indica cumplimiento: salón, llevar o delivery. Son conceptos separados: un pedido de plataforma puede ser delivery o para llevar.
- Guardar canal, tipo de servicio, sucursal, usuario, fecha y referencia externa como snapshots. No borrar un canal usado históricamente; desactivarlo.
- La referencia externa debe ser única por plataforma y sucursal cuando venga informada. Reintentar una importación devuelve el mismo pedido; no crea una segunda comanda.
- La Fase 6 permite capturar manualmente un pedido de plataforma y su referencia. Una integración automática con la API de esa plataforma requiere contrato técnico y credenciales específicos; no forma parte de este alcance.
- Cada pedido pertenece a una sola sucursal. La bodega de consumo se fija antes de enviarlo a preparación y debe pertenecer a esa sucursal.

### 4.2 Pedido y comanda

- Estados del pedido: `Draft`, `Submitted`, `InPreparation`, `Ready`, `OutForDelivery` cuando aplique, `Delivered`, `Cancelled`. La situación de pago se registra aparte (`Unpaid`, `PartiallyPaid`, `Paid`) en Fase 7 para no mezclar cocina con cobro.
- El pedido puede tener líneas en estados distintos. El estado agregado es una proyección de sus líneas y del cumplimiento; una bebida lista no marca lista una pizza pendiente.
- Numeración legible y única por sucursal, generada en backend. El identificador interno es `Guid`; la numeración nunca se usa como única clave técnica.
- Cada cambio de estado guarda actor, UTC, estado anterior/nuevo, motivo si aplica y versión. Rechazar transiciones inválidas y escrituras obsoletas.
- Una línea enviada a cocina conserva producto/variante, nombre, cantidad, precio, impuestos aplicables, receta/versiones, modificadores y exclusiones. Editar una comanda enviada crea una revisión o delta explícito; no sobrescribe silenciosamente lo que cocina ya vio.

### 4.3 Disponibilidad y precio

- El menú solo muestra artículos activos, vendibles, visibles y habilitados en la sucursal. El backend repite esta validación al enviar el pedido.
- Usar precio base local y reglas de multiprecio existentes; no duplicar el motor de precio en el menú. El precio elegido queda congelado al enviar el pedido y cualquier corrección posterior crea una nueva revisión auditada.
- Fotos y favoritos son presentación, no determinan disponibilidad ni prioridad fiscal. Un artículo sin foto debe poder pedirse.
- Una categoría visible vacía puede ocultarse en el menú operativo, pero permanece administrable.

### 4.4 Modificadores y exclusiones

- Un grupo define mínimo/máximo de opciones, selección única o múltiple y si es requerido. Cada opción define precio adicional y, cuando aplique, una lista de componentes inventariables y cantidades en unidad base.
- Un modificador con consumo adicional (por ejemplo, queso extra de 40 g) genera demanda propia y snapshot de costo/receta. Su precio adicional se suma en servidor.
- Una exclusión solo puede apuntar a un componente de la receta marcado como excluible. Reduce el consumo planificado de ese componente para esa línea y no cambia el precio salvo que exista una regla de precio explícita.
- Nunca permitir cantidad negativa, excluir más de la receta, excluir un componente obligatorio/no excluible, ni crear un ingrediente a partir de texto libre.
- Congelar la versión de receta y opciones al enviar; cambios de configuración posteriores no modifican pedidos antiguos.

### 4.5 Combos y promociones

- Un combo define grupos de selección, cantidades, productos elegibles, precio/regla de precio y ventana de vigencia por sucursal. Expandir el combo a líneas físicas para cocina e inventario, conservando una referencia a la línea comercial padre.
- Validar que toda selección cumple mínimos/máximos y que cada producto elegido está disponible en la sucursal. La misma materia prima se cuenta una sola vez por unidad vendida.
- El descuento o precio de combo se prorratea en centavos entre sus componentes para snapshots y futura devolución; la última línea absorbe el residuo. El total de componentes debe coincidir exactamente con el total comercial.
- **Regla incorporada:** no acumular dos promociones automáticas sobre la misma unidad. Si varias reglas coinciden, usar prioridad explícita y luego el beneficio mayor; registrar la regla aplicada. Un descuento manual de Fase 7 requiere su autorización habitual y no debe ocultar el descuento de combo.
- Una promoción vencida deja de ofrecerse a pedidos nuevos; un pedido ya enviado conserva su snapshot.

### 4.6 Mesas y turnos

- `DiningArea` agrupa mesas de una sucursal. `DiningTable` tiene código único en el área, capacidad opcional y estado activo. `TableTurn` representa ocupación desde apertura hasta liberación.
- Una mesa admite como máximo un turno abierto. Un turno puede agrupar uno o más pedidos, pero solo una cuenta activa hasta que Fase 7 implemente división. Al cerrar la cuenta, liberar la mesa mediante transición auditada.
- Mover un pedido de mesa exige ambas mesas de la misma sucursal, destino libre, permiso, motivo y control de concurrencia. Conservar mesa de origen y destino en historial.
- No borrar mesas con turnos históricos; desactivarlas.

### 4.7 Cocina y tiempos

- `KitchenStation` define estaciones por sucursal (por ejemplo cocina, barra, despacho). Producto o categoría tiene estación predeterminada configurable; el pedido guarda la estación resuelta por línea.
- El envío genera evento/comanda por estación con secuencia única de revisión. KDS consulta líneas pendientes y recibe actualizaciones por mecanismo compatible con MVC; un refresh periódico es suficiente si no existe canal en tiempo real. Toda acción se confirma en servidor.
- Estados de línea: `Queued`, `Preparing`, `Ready`, `HandedOff`, `Cancelled`. El reloj del servidor registra `queuedAt`, `startedAt`, `readyAt`, `handedOffAt`; duración no se calcula desde el reloj del navegador.
- Una corrección posterior al envío produce un delta visible: nueva línea, cantidad añadida o cancelación. La cocina debe poder distinguir revisión original y cambio.
- El mismo evento reintentado no crea comandas duplicadas ni repite consumo.
- Las consultas operativas calculan tiempo en cola, preparación y entrega a partir de marcas del servidor. Permitir filtros por sucursal, estación y canal sin alterar los eventos originales.

### 4.8 Delivery

- `DeliveryZone` pertenece a sucursal y tiene nombre, tarifa y estado; `DeliveryFeeSnapshot` se guarda en pedido. Dirección y referencia de entrega son obligatorias para delivery.
- `Driver`/motorista activo se asigna a un pedido listo; registrar asignador, asignado, salida, entrega, resultado e incidencias con marcas UTC. Un motorista inactivo no recibe asignaciones nuevas.
- La tarifa se calcula una vez al confirmar la zona, es visible antes de enviar el pedido y forma parte del total comercial. Cambiar zona antes de envío recalcula; después requiere revisión auditada.
- No asumir geocodificación ni cálculo automático de ruta externa. Ruta puede ser texto/observación y secuencia de entregas; integración con mapas sería ampliación aparte.
- Un pedido delivery pasa a `OutForDelivery` tras salida y `Delivered` con confirmación de entrega. Registrar quién confirma y hora. Una entrega fallida queda como incidencia, sin marcar entregado.

### 4.9 Reserva, consumo, cancelación y merma

- **Reserva lógica:** al enviar, calcular demanda de cada producto/bodega/lote según receta y opciones. `disponible = existencia física - reservas activas`. La operación crea reservas sin mover Kardex; se hace bajo concurrencia para impedir sobreventa entre pedidos.
- **Preparado elaborado al pedido:** cuando cocina inicia la línea, consumir la reserva de sus componentes reales mediante el ledger existente y registrar versión/costo/lotes. La línea pasa a `Preparing` en la misma transacción. No volver a consumir en `Ready`, entrega ni venta.
- **Producto producido con stock o reventa:** reservar el SKU terminado al enviar y descontarlo físicamente al iniciar preparación/despacho de esa línea. Evitar consumir además sus ingredientes internos de subreceta ya producida.
- **Cancelación antes del consumo:** liberar reserva, sin salida de inventario ni merma. **Cancelación después del consumo:** no devolver ingredientes crudos; registrar merma/desperdicio del consumo ya efectuado, con motivo, costo y usuario. Si un SKU intacto vuelve a inventario, usar una reversión física explícita con trazabilidad de lote, no una liberación de reserva.
- La merma no debe descontar stock dos veces. Es una clasificación y trazabilidad del movimiento de consumo ya hecho, o un movimiento de salida solo cuando todavía había stock físico que se destruye. Mantener referencia a pedido, línea y evento de cancelación.
- Una línea parcialmente preparada se separa en cantidad preparada y pendiente; cancelar la pendiente libera reserva y cancelar la preparada registra merma. No permitir que cualquiera de ambas supere la cantidad original.
- La Fase 7 debe cobrar un pedido con consumo ya registrado sin llamar de nuevo a salida de receta. En un pedido cancelado no se crea venta; si ya existió cobro, la corrección financiera corresponde a anulación/devolución de Fase 7.

## 5. Diseño de datos sugerido

| Entidad | Datos mínimos | Restricción importante |
|---|---|---|
| `OrderChannel`, `ServiceType` | Código, nombre, activo; plataforma y política de referencia externa cuando aplique. | Código único; históricos no se eliminan. |
| `RestaurantOrder` | ID, número, sucursal, bodega, canal, servicio, referencia externa, estado, pago futuro, cliente/contacto, totales y versión. | Índice único de número/sucursal y de referencia externa por plataforma/sucursal. |
| `RestaurantOrderLine` | Producto concreto, cantidad, precio/tasa/total snapshots, estación, estado, receta/versiones, cantidad reservada, consumida, cancelada antes de preparar y desperdiciada tras preparar. | `consumida + cancelada antes de preparar <= pedida`; `desperdiciada <= consumida`. |
| `OrderLineOption` / `OrderLineExclusion` | Opción/regla, precio, componentes, cantidades y snapshot histórico. | Solo opciones activas al crear; el histórico queda legible tras desactivar. |
| `InventoryReservation` | Pedido/línea, producto, bodega, lote si aplica, cantidad, estado, vencimiento/versión. | Activa única por evento; liberar/consumir una sola vez. |
| `OrderStateEvent` | Pedido/línea, de/a, actor, momento, motivo, versión e ID de operación. | Append only; operación idempotente. |
| `MenuBranchSetting`, `ModifierGroup`, `ModifierOption`, `ComboDefinition`, `PromotionRule` | Sucursal/vigencia, selecciones, componentes, precios y prioridad. | Validar intervalos, pertenencia a sucursal y referencias. |
| `DiningArea`, `DiningTable`, `TableTurn` | Sucursal, código, apertura/cierre, operador. | Una mesa con un turno activo. |
| `KitchenStation`, `KitchenTicket` | Estación, línea/revisión, secuencia, tiempos y estado. | Una comanda por línea/revisión/estación. |
| `DeliveryZone`, `Driver`, `DeliveryAssignment` | Zona/tarifa histórica, dirección, motorista, salida/entrega/incidencias. | Asignación activa única por pedido. |
| `OrderWaste` | Pedido/línea, cantidad, costo, componente/lote, causa y movimiento original. | Merma respaldada por consumo; no doble salida. |

Usar `decimal` para dinero y unidades base; respetar seis decimales de inventario/recetas y la precisión de la moneda para precios. Guardar fechas UTC y mostrar hora local. Incorporar tokens de concurrencia `xmin` o mecanismo equivalente según los patrones PostgreSQL ya usados. Las claves de idempotencia se protegen también con índices únicos en base de datos.

## 6. Casos de uso y contratos de aplicación

### 6.1 Servicios

- `IOrderService`: crear borrador, editar, enviar, consultar, agregar delta, cancelar total/parcial, cambiar canal/servicio antes del envío y obtener historial.
- `IMenuService`: menú por sucursal, disponibilidad, favoritos, opciones, combos y promociones vigentes. Reutiliza servicio central de precios y `ProductBranchSetting`.
- `IOrderInventoryService`: calcular demanda, reservar, consumir, liberar y registrar merma usando el ledger de lotes. Entrega un snapshot de consumo por línea para Fase 7.
- `IKitchenService`: consultar colas por estación, iniciar, marcar listo y entregar con control de versión e idempotencia.
- `ITableService`: abrir/cerrar turno, asociar pedido y mover mesa con control de concurrencia.
- `IDeliveryService`: calcular tarifa, asignar motorista, registrar salida, incidencia y entrega.

Estos nombres son sugeridos. Reutilizar interfaces actuales cuando ya cubran la función y mantener las reglas fuera de controladores y Razor.

### 6.2 Enviar pedido

```text
SubmitOrder(orderId, expectedVersion, clientOperationId)
  autorizar rol/permiso y alcance de sucursal
  cargar pedido y líneas bajo transacción
  comprobar Draft, versión, canal, servicio y datos obligatorios
  validar mesa o dirección/zona según servicio
  cargar menú vigente y validar producto, variante, opciones y combo
  calcular precio y demanda de inventario en servidor
  comprobar existencia física menos reservas de la bodega
  crear reservas, snapshots, número y comandas por estación
  cambiar a Submitted y guardar evento/auditoría
  confirmar todo o revertir todo
```

El cliente no decide precio, costo, bodega ni disponibilidad. Si cambió una regla mientras el borrador estaba abierto, mostrar diferencias y pedir revisión antes de enviar; no aceptar en silencio otro total.

### 6.3 Iniciar preparación y cancelar

```text
StartLine(lineId, expectedVersion, operationId)
  confirmar estado Queued y permiso cocina
  consumir reservas de componentes o SKU según modo de receta
  escribir lotes/Kardex y snapshot de consumo/costo
  cambiar línea a Preparing y registrar tiempos/auditoría
  confirmar en una transacción

CancelLine(lineId, quantity, reason, operationId)
  separar cantidad pendiente y cantidad físicamente preparada
  liberar reserva de la pendiente
  conservar consumo de la preparada y registrar merma relacionada
  registrar evento y actualizar pedido/KDS en una transacción
```

No aceptar cantidad negativa, transición repetida ni cancelación de más unidades de las que siguen activas. Ante reintento idéntico, devolver resultado previo; con el mismo ID y contenido distinto, rechazar.

## 7. Interfaz y permisos

### Pantallas

- **Configuración:** canales/plataformas, menú por sucursal, grupos de opciones, combos/promociones, estaciones, áreas/mesas, zonas y motoristas.
- **Toma de pedido:** canal, servicio, mesa o dirección, menú visual, variantes/opciones/exclusiones, resumen y envío. Mostrar precio y disponibilidad de sucursal antes de confirmar.
- **Pedidos:** lista con búsqueda, filtros por estado/canal/servicio/sucursal, detalle, línea de tiempo, revisión y acciones permitidas.
- **KDS:** columnas por estación/estado, reloj de preparación, alertas de revisión, acciones grandes para tableta, recuperación tras reconexión.
- **Mesas:** mapa/lista por área, disponibilidad y turno actual. **Delivery:** pendientes, asignados, en ruta, entregados e incidencias.
- Ajustar pantallas al patrón MVC/Razor existente, con acciones críticas confirmadas y errores que conserven los datos capturados.

### Autorización backend

- Reutilizar `restaurant.orders.view/create/update`, `restaurant.tables.view/manage` y `kitchen.orders.view/status` ya declarados.
- Añadir permisos separados para cancelar pedido, gestionar menú/opciones/promociones, configurar estaciones, gestionar zonas/motoristas y asignar/confirmar delivery. La mera visibilidad de la página no concede permiso de mutación.
- Toda acción consulta alcance de sucursal y bodega; no confiar en IDs enviados por la UI. Mesero no cambia estados de cocina y cocina no edita precios/promociones sin permisos específicos.
- Auditar cancelación, merma, cambio de mesa, modificación tras envío, asignación de motorista y cambios de precio/configuración.

## 8. Migraciones y compatibilidad

1. Crear tablas nuevas de pedidos de cliente con nombres inequívocos; no migrar `PurchaseOrders` ni `ProductionOrders`.
2. Crear catálogos y restricciones únicas; insertar configuraciones iniciales mínimas de canales/servicios/estaciones sin asignar datos históricos a una sucursal inventada.
3. Incorporar reservas y nuevas referencias de Kardex/merma; mantener el ledger actual como fuente de verdad de movimientos físicos.
4. Hacer cambios de Fase 7 compatibles: agregar a venta referencias opcionales de pedido/línea y marcador de consumo previo, sin cambiar el comportamiento de ventas directas antiguas.
5. Verificar migraciones con históricos y comprobar que números, existencias, lotes, costos y ventas previas no cambian.

## 9. Contrato obligatorio con Fase 7

La Fase 6 entrega un `OrderCheckoutSnapshot` con ID/versión del pedido, líneas entregables, precio/impuesto/descuento, estado de inventario por línea y detalle histórico de componentes consumidos. La Fase 7 valida ese snapshot y registra venta/pago/caja sin volver a consumir las líneas ya consumidas. Para líneas todavía reservadas, la confirmación de venta realiza el consumo pendiente exactamente una vez en la misma transacción. La venta conserva vínculo a pedido y líneas para devolución/cancelación posterior.

El documento anterior de Fase 7 debe ajustarse a este contrato antes de implementarse: su algoritmo actual presume consumo al cobrar. La preparación y el cobro pueden ocurrir en momentos diferentes; ambos deben compartir un único registro de consumo por línea.

## 10. Reglas complementarias incorporadas al alcance

Estas reglas ya están integradas en el plan, el diseño y los criterios de aceptación. El agente debe implementarlas junto con `ORD-01` a `ORD-10`; esta lista sirve como comprobación de cobertura.

1. **Separar preparación de cobro.** Evita que una pizza ya elaborada falle al cobrar por stock que fue usado antes o que se descuente dos veces.
2. **Revisiones explícitas de comandas.** Una corrección debe aparecer en KDS como cambio nuevo; sobrescribir la línea original produce errores de cocina.
3. **Reserva con bodega y lote.** La disponibilidad del menú debe considerar otros pedidos pendientes, no solo ventas ya cobradas. En perecederos, conservar FEFO y evitar lotes vencidos.
4. **Estados por línea.** Bebida y pizza pueden terminar en tiempos diferentes; el pedido agregado no debe ocultarlo.
5. **Separar canal y servicio.** Plataforma, salón y delivery no son la misma dimensión, y su mezcla dificultaría tarifas e informes.
6. **Snapshot de tarifa y promociones.** Cambiar una tarifa o promoción mañana no debe alterar pedidos de hoy.
7. **Sincronización KDS idempotente.** Una recarga, doble clic o pérdida de red no crea dos comandas ni dos salidas de inventario.
8. **Incidencias de entrega.** Un motorista puede no encontrar al cliente; registrar el intento sin fingir entrega exitosa.
9. **Observabilidad operativa.** Registrar tiempos de cola, preparación y entrega por estación/canal, con filtros por sucursal. No introducir un motor complejo de analítica para lograrlo.
10. **Límites claros de Fase 6.** Capturar pedidos de plataformas y su referencia; cualquier integración automática con PedidosYa u otra API requiere contrato y credenciales específicos.

## 11. Criterios de aceptación para el agente

| Clave | Prueba funcional mínima |
|---|---|
| `ORD-01` | Pedido de plataforma capturado manualmente para delivery conserva canal, servicio, sucursal y referencia; reenvío con la misma referencia no duplica. |
| `ORD-02` | Pedido pasa por estados permitidos; comanda y líneas muestran revisiones explícitas y no se duplican con reintentos o reconexión. |
| `ORD-03` | Producto oculto/inactivo en sucursal A no se pide allí; precio local de B se usa solo en B. |
| `ORD-04` | Extra queso suma precio y 40 g de consumo en la bodega correcta y conserva versión histórica. |
| `ORD-05` | “Sin cebolla” resta su cantidad de consumo solo si la receta permite excluirla. |
| `ORD-06` | Combo pizza + bebida conserva total, reparto, consumo de ambos y vigencia de promoción. |
| `ORD-07` | Dos operadores no abren turnos simultáneos de la misma mesa; el traslado queda auditado. |
| `ORD-08` | Pizza aparece en cocina y bebida en despacho; tiempos de cola/preparación/entrega y revisiones se consultan por estación, canal y sucursal. |
| `ORD-09` | Zona calcula tarifa visible e histórica; asignación, salida, incidencia y entrega conservan actor/tiempo sin marcar éxito ante una entrega fallida. |
| `ORD-10` | Cancelar antes de preparar libera reserva; cancelar pizza preparada registra merma sin reintegrar ingredientes ni descontarlos otra vez. |

Además verificar rollback ante fallo, concurrencia por stock/mesa/línea, permisos por rol y sucursal, idempotencia de comandos, lotes/caducidad y FEFO de reservas, snapshots de promociones y tarifas, migración con históricos y enlace posterior a venta sin doble consumo. No declarar la fase completa hasta que los diez escenarios y las diez reglas complementarias estén cubiertos.

## 12. Instrucción lista para el agente de IA

> Implementa la Fase 6 `ORD-01` a `ORD-10` en el repositorio actual siguiendo este documento y la imagen aprobada. Las diez reglas complementarias de la sección 10 también son alcance de implementación. Primero actualiza los documentos normativos que aún excluyen pedidos o promociones. Reutiliza catálogo, recetas versionadas, bodega/lotes, autorización y auditoría existentes. Separa pedido, cocina, entrega y cobro; reserva al enviar, consume una sola vez cuando ocurre el evento físico y registra merma real al cancelar después de preparar. Conserva revisiones de comanda, snapshots de precio/tarifa, incidencias delivery y métricas operativas. Crea tablas, migraciones, servicios, controladores y vistas MVC/Razor en lotes pequeños. Mantén transacciones, idempotencia, concurrencia y permisos en backend. Entrega evidencia por cada criterio `ORD-01` a `ORD-10` y deja el contrato de checkout listo para Fase 7.
