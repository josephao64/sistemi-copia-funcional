# Fase 7 — POS, ventas, pagos y caja: documento de implementación

**Estado:** documento de implementación solicitado para la fase 7.  
**Secuencia corregida:** la Fase 6 se implementa primero. Antes de ejecutar este documento, ajustar el cobro de pedidos al contrato de consumo único definido en `FASE_6_PEDIDOS_COCINA_DELIVERY_IMPLEMENTACION.md`, sección 9.  
**Fuente de alcance aprobada:** imagen entregada por el usuario (`POS-01` a `POS-12`). Cuando la documentación anterior contradiga esta fase, actualizarla durante la implementación.  
**Base revisada:** código y documentos de la rama local `dev`, commit `74975a3`. Las referencias remotas son las que estaban almacenadas localmente; esta revisión no confirma cambios nuevos de GitHub.

## 1. Plan primero

1. **Alinear reglas y dependencias.** Tomar `POS-01` a `POS-12` como alcance de esta entrega, registrar las reglas de la sección 3 en `REQUIREMENTS.md`, `BUSINESS_RULES.md` y `DEVELOPMENT_PLAN.md`, y retirar las contradicciones anteriores.
2. **Cerrar el contexto operativo (`POS-01`, `POS-08`).** Definir terminal, sucursal, bodega de consumo y sesión de caja; exigir que todas las referencias pertenezcan al mismo alcance autorizado. Toda venta, movimiento y cierre debe conservar ese contexto.
3. **Modelar el pedido de restaurante (`POS-02`, `POS-06`, `POS-10`).** Crear su ciclo de vida, líneas, modificadores y vínculo a ventas. Dividir la cuenta por productos, personas o importes sin doble cobro.
4. **Endurecer la confirmación de ventas (`POS-03`, `POS-04`, `POS-05`, `POS-07`).** Reutilizar precios y recetas existentes, recalcular en servidor, validar stock por bodega, bloquear duplicados y registrar en una transacción venta, pago, consumo, Kardex, caja y auditoría.
5. **Completar caja por sucursal y cierre (`POS-08`, `POS-09`).** Mantener propiedad de la caja por usuario, segregación por sucursal y cierres por dispositivo POS. Capturar el arqueo antes de revelar el esperado y revisar diferencias mediante permiso y trazabilidad.
6. **Ajustar devoluciones y reembolsos (`POS-10`).** Relacionar cada devolución con venta y línea originales, reponer únicamente lo que vuelve físicamente, limitar cantidades e importes, exigir autorización de reembolso y reflejarlo en la caja correspondiente.
7. **Completar offline por ubicación (`POS-11`).** Separar caché, pendientes y stock por usuario, dispositivo, sucursal, caja y bodega; sincronizar idempotentemente, resolver conflictos y preservar operaciones pendientes.
8. **Cerrar interfaz operativa (`POS-12`).** Optimizar búsqueda, escáner, botones táctiles, vista de cobro, estados de red y uso en PC, tableta y teléfono. La UI no reemplaza validaciones del servidor.
9. **Migrar y validar.** Hacer migraciones compatibles con históricos, revisar restricciones e índices, y verificar los escenarios de aceptación de cada clave. Actualizar documentación de arquitectura y operación.

**Orden recomendado:** actualizar reglas → contexto de sucursal/bodega → pedidos y división → venta/pago/inventario → caja → devoluciones → offline → interfaz → aceptación.

## 2. Diagnóstico de la base existente

| Clave de imagen | Estado observado en el repositorio | Trabajo de fase 7 |
|---|---|---|
| POS-01 Terminal y bodega de consumo | `CashSession` conserva `BranchId`; `SaleService` usa bodega predeterminada de la sucursal o contexto actual. La sesión se abre por usuario y usa `RegisterCode` derivado del nombre. | Definir terminal física/configurada, vínculo obligatorio con sucursal y bodega activa, y evitar el fallback ambiguo. |
| POS-02 Venta desde pedido | No se encontró modelo de pedido de restaurante; `PurchaseOrder` es de compras. La venta se crea desde productos del formulario. | Crear enlace con pedido preparado, versión/snapshot de líneas y modificadores; bloquear cobro repetido. |
| POS-03 Consumo con receta | `SaleService` usa `IRecipeOperationsService.PlanSaleAsync`, conserva versión y componentes y genera movimientos. | Revisar empaque/preparación, múltiples líneas, lotes y consistencia con la bodega elegida. |
| POS-04 Concurrencia de inventario | Hay control de existencias y restricciones por producto/bodega; la venta termina con `SaveChangesAsync`. | Verificar concurrencia real en confirmaciones simultáneas, idempotencia y reversión completa ante conflicto. |
| POS-05 Pago único | `Sale` posee un `SalePayment`; el pago debe igualar el total. | Mantener la regla vigente para cada venta confirmada y completar validaciones por medio. |
| POS-06 División de cuenta | No se encontró entidad ni servicio para distribuir líneas/personas/importes. | Crear cuenta padre, asignaciones y ventas hijas con conciliación exacta de importes. |
| POS-07 Descuentos controlados | Ya hay solicitud y autorización por otro usuario, validación contra el carrito y auditoría por línea. | Revisar el flujo extremo a extremo al cobrar desde pedido y desde cuenta dividida. |
| POS-08 Caja por sucursal | `CashSession` tiene `BranchId` y cierre por dispositivos POS; `CashService` abre caja para usuario autenticado. | Hacer obligatorio el alcance aprobado y comprobar autorización de sucursal en backend. |
| POS-09 Cierre ciego y control | Existe arqueo único por denominaciones, cierre y conciliación posterior; el servicio conoce el esperado durante el conteo. | Definir y aplicar ocultamiento del esperado antes del arqueo, y revisión de diferencias sin alterar el cierre. |
| POS-10 Devoluciones y reembolsos | Existen `SaleReturn`, `SaleRefund`, servicios y vistas. | Revisar trazabilidad al pedido y a la bodega/lote originales, límite de devolución y autorización. |
| POS-11 Offline por ubicación | Existen PWA, IndexedDB y `/api/offline-pos`; el bootstrap de producto contiene stock sin clave de bodega y la sesión local no incluye `BranchId`. | Particionar datos por ubicación y resolver caja/bodega en sincronización. |
| POS-12 Interfaz táctil final | Hay formulario MVC del POS y página de sincronización offline. | Evaluar en los tres tamaños de pantalla, escáner, foco, accesibilidad y estados de error. |

### Componentes existentes para reutilizar

- `src/SistemiPos.Application/Sales/SaleService.cs`, `CreateSaleRequest.cs`, `SalePaymentRequest.cs`.
- `src/SistemiPos.Application/Cash/CashService.cs` y entidades de `src/SistemiPos.Domain/Cash/`.
- `src/SistemiPos.Web/Controllers/SalesController.cs`, `CashController.cs`, `Controllers/Api/OfflinePosController.cs`.
- `src/SistemiPos.Web/Views/Sales/Form.cshtml`, `Views/Pos/Offline.cshtml` y `wwwroot/js/pos-sync.js`.
- `src/SistemiPos.Application/Recipes/` e inventario por bodega/lote existentes.

## 3. Reglas de diseño de esta implementación

La imagen es el alcance de la fase. Las siguientes reglas completan la lógica necesaria para implementarla y deben registrarse en los documentos normativos al modificar el código:

1. **Pedido de restaurante.** Es un documento nuevo y distinto de `PurchaseOrder` y `ProductionOrder`. Estados: `Draft`, `Submitted`, `InPreparation`, `Ready`, `PartiallyPaid`, `Paid`, `Cancelled`. Solo `Ready` y `PartiallyPaid` permiten crear ventas. Un pedido pagado no vuelve a edición; los cambios de un pedido parcialmente pagado solo afectan porciones sin cobrar y quedan auditados. Los modificadores se guardan por línea con precio y efecto de receta cuando corresponda.
2. **División de cuenta.** Un pedido tiene una cuenta padre. Cada porción produce una venta hija confirmada con un solo medio de pago, preservando la regla de pago único por venta. Se admiten reparto por producto/cantidad, por persona y por importe. Las porciones usan centavos enteros para asignar total; la última recibe el residuo de redondeo. Los impuestos y descuentos de cada línea se prorratean proporcionalmente y el residuo se asigna a la última porción de esa línea. No se agrega propina porque no aparece en el alcance entregado.
3. **Offline.** El POS funciona offline por ubicación según `POS-11`; actualizar la sección contradictoria de `DEVELOPMENT_PLAN.md`. Los pedidos preparados y cuentas divididas requieren conexión al servidor en esta fase porque necesitan bloquear porciones compartidas entre cajas. El POS offline mantiene la venta directa existente, por bodega, con comprobante provisional y sincronización posterior. La UI indica claramente esta limitación operacional.
4. **Terminal y caja.** La caja sigue perteneciendo al usuario autenticado. La terminal configurada fija sucursal y bodega de consumo al abrir la sesión. No se cambia terminal o bodega de una sesión activa; para cambiar de ubicación se cierra y se abre otra sesión. El backend comprueba esa relación en cada venta.
5. **Cierre ciego.** El cajero ve denominaciones y captura conteo sin mostrar esperado, ventas acumuladas ni diferencia. El servidor guarda el arqueo y después revela el resultado. Se puede cerrar con diferencia; un usuario con permiso administrativo revisa y concilia posteriormente sin alterar importes originales.
6. **Preparados devueltos.** La devolución económica de un producto elaborado no repone automáticamente ingredientes ya consumidos. Solo se registra entrada del artículo cuando vuelve físicamente a inventario vendible y hay trazabilidad válida. Un reembolso no implica por sí mismo devolución física. El flujo debe permitir reembolso sin entrada de inventario.
7. **Prioridad de reglas.** Si una regla previa contradice estas definiciones, el agente actualiza la documentación y conserva migraciones y datos históricos. Cualquier detalle adicional que no se desprenda de estas reglas se documenta antes de codificarlo.

## 4. Especificación de lógica por clave

### POS-01 — Terminal y bodega de consumo

- Al iniciar operación, resolver `usuario → sucursales permitidas → terminal habilitada → caja activa del usuario → bodega de consumo activa`.
- Rechazar venta si terminal, caja, sucursal y bodega son incompatibles. Validar en backend, aunque el selector UI ya filtre opciones.
- Guardar `BranchId`, `WarehouseId`, terminal y caja como contexto histórico de la operación; los cambios posteriores de configuración no deben reinterpretar ventas antiguas.
- Para recetas, todos los insumos se consultan y descuentan en la bodega resuelta; una bodega incompatible no puede reemplazarse silenciosamente por otra.
- Modelar terminal configurable y vincularla a `CashSession`/`Sale`, preservando la caja como propiedad del usuario.

### POS-02 — Venta desde pedido preparado

- Crear entidad de pedido de restaurante y líneas con identificadores estables, cantidades, modificadores, precio y estado histórico.
- Al abrir cobro, tomar una versión del pedido; verificar estado apto, sucursal, caja y que ninguna línea/cantidad haya sido cobrada.
- El servidor vuelve a calcular precio, impuestos, descuentos y consumo; la UI solo envía selección de líneas, cantidades y versión del pedido.
- Confirmar venta y marcar asignación cobrada en una misma transacción. Reintento con la misma clave de operación devuelve el resultado existente.
- Si cambia el pedido después de abrir cobro, exigir recarga y nueva aprobación de descuentos. No cobrar dos veces una línea.

### POS-03 — Consumo con receta

- Reutilizar plan de receta/versiones existentes. Cada línea preparada fija versión y componentes usados para que una edición futura no altere históricos.
- Agregar explícitamente empaque u otros componentes definidos en la receta; no deducirlos por nombre del producto.
- Agrupar la demanda física por producto y bodega antes de descontar, incluyendo cantidades provenientes de varias líneas.
- Validar disponibilidad por lote vigente y unidad base; aplicar FEFO/FIFO conforme a reglas actuales y crear asignaciones y Kardex vinculados a venta/línea.
- Un fallo en un componente cancela toda la confirmación y no crea pago ni movimiento de caja.

### POS-04 — Concurrencia de inventario

- Usar una unidad de trabajo/transacción para venta, pago, consumos, movimientos, caja, vínculo al pedido y auditoría.
- Revalidar stock dentro de la operación de escritura; capturar conflicto de concurrencia en producto/bodega/lote y responder con mensaje para actualizar el carrito.
- Mantener restricciones únicas para correlativo y `ClientOperationId`; un reintento idempotente no repite efectos.
- No permitir saldo negativo, ni siquiera con dos cajas confirmando simultáneamente la última unidad.
- No volver a calcular usando stock agregado de otra bodega.

### POS-05 — Pago único

- Mantener un pago y un medio por venta confirmada; una cuenta dividida produce varias ventas hijas. `TotalAmount` debe igualar el total de cada venta en decimal, con redondeo monetario vigente.
- Efectivo: recibido mayor o igual al total, cambio calculado en servidor; el ingreso de caja es el importe de la venta, no el efectivo entregado.
- Transferencia: cuenta destino activa, referencia y datos exigidos; solo `Received` o `Verified` permiten confirmar según las reglas actuales.
- Tarjeta/POS: dispositivo asignado a la caja, estado `Approved`, autorización y referencia; no guardar datos completos de tarjeta.
- Generar comprobante solo tras confirmación; la respuesta conserva número y referencia de pago.

### POS-06 — División de cuenta

- Crear cuenta padre con asignaciones por línea/cantidad, persona o importe y ventas hijas confirmadas por porción.
- Invariante: suma de porciones confirmadas y pendientes = total cobrable del pedido; ninguna unidad o centavo se asigna dos veces.
- Si se divide por productos, una línea divisible conserva cantidad de origen y cantidad asignada. Si se divide por importes, prorratear impuestos y descuentos en centavos y asignar el residuo a la última porción.
- Cada venta hija conserva una forma de pago, caja, bodega y referencias al pedido/cuenta padre; cada pago se confirma por separado.
- La cuenta no queda liquidada hasta cobrar todas las porciones. Fallar una porción no revierte pagos previos; debe permitir reintento seguro de lo pendiente.

### POS-07 — Descuentos controlados

- Reutilizar el servicio existente de aprobación por otro usuario. La solicitud lleva snapshot de líneas, cantidades, precio, tipo/valor de descuento y comentario.
- El descuento no excede el bruto de la línea y respeta permiso, habilitación y límite configurado. El backend bloquea cobro pendiente o rechazado.
- Cualquier cambio de carrito, pedido, porción, cantidad o precio invalida la aprobación; el cobro requiere nueva solicitud.
- Guardar usuario que vende, autorizador, fecha/hora, razón y descuento por línea. No ampliar el límite permanente del vendedor.

### POS-08 — Caja por sucursal

- Al abrir caja, fijar sucursal autorizada y dispositivos POS activos/asignados. Una sesión activa corresponde al usuario autenticado según `RF-CAJ-011`.
- Consultas de venta, movimientos, arqueo y cierre filtran por sesión y sucursal; el backend no acepta IDs de otra sucursal aunque se envíen manualmente.
- El cierre consolida efectivo, transferencia, POS por dispositivo, otros ingresos/egresos y reembolsos; conserva cada movimiento original y su documento.
- No cerrar caja con dispositivos POS usados sin su cierre físico requerido. Una caja cerrada no se reabre.

### POS-09 — Cierre ciego y control

- La primera pantalla solo solicita cantidades por denominación y datos físicos POS; no revela efectivo esperado ni diferencia antes de guardar el arqueo.
- Una vez guardado, calcular esperado y diferencia en servidor. El cierre conserva conteo, diferencia, usuario, fecha y cierres POS sin mutar cifras históricas.
- Diferencia no bloquea el cierre según reglas vigentes; revisión administrativa posterior agrega resultado, observación, usuario y fecha.
- Reintentos de cierre y de arqueo no crean duplicados. El conteo único por sesión se protege también en persistencia.

### POS-10 — Devoluciones y reembolsos

- Elegir venta/línea original, cantidad no devuelta y motivo. La suma de devoluciones no excede lo vendido, ni el reembolso el saldo elegible.
- Solo el retorno físico aprobado genera entrada; para lotes, conservar referencia a asignación de salida original y no inventar lotes faltantes.
- Autorizar el reembolso con permiso alto en backend, aunque el medio de devolución sea distinto al medio cobrado.
- Registrar movimiento de caja del medio correspondiente, siempre ligado a sesión activa apropiada y a devolución/venta. El efectivo reembolsado reduce esperado.
- Mantener inmutables venta, pago y cierre originales; consulta histórica muestra todas las relaciones.

### POS-11 — Offline por ubicación

- El bootstrap entrega sucursal, caja, bodega, terminal/dispositivo, configuración vigente, productos visibles y existencias por bodega.
- IndexedDB separa datos por identidad de usuario, dispositivo, sucursal, caja y bodega; el stock local disponible se calcula como último sincronizado menos operaciones pendientes del mismo contexto.
- La cola conserva `ClientOperationId` único, contexto, fecha local, detalle/precios históricos, pago y estado. No elimina pendientes ante error o conflicto.
- Bloquear nueva venta tras 7 días sin sincronizar, al llegar a 500 pendientes o sin caja/datos válidos, según reglas existentes.
- En sincronización, el servidor valida nuevamente identidad, caja activa, bodega, producto, receta, stock, precios y pago. Una respuesta idempotente devuelve la venta ya registrada sin repetir efectos.
- Los conflictos se muestran para resolución explícita; el comprobante provisional no consume correlativo definitivo. Crédito y descuentos continúan en línea.
- Los pedidos preparados y la división de cuenta requieren conexión, por la asignación concurrente de porciones.

### POS-12 — Interfaz táctil final

- Pantalla de venta usable con teclado, lector de barras y tacto: búsqueda rápida, cantidades, modificadores aprobados, resumen fijo, cobro visible y mensajes claros de bloqueo.
- En PC, tableta y teléfono, validar tamaños de toque, foco tras escaneo, desplazamiento, orientación y navegación sin pérdida del carrito.
- Mostrar contexto de sucursal/caja/bodega y estado online/offline antes de cobrar; las operaciones pendientes y los conflictos deben ser visibles.
- Confirmación con bloqueo contra doble clic, estado de progreso y recuperación tras error. No mostrar comprobante definitivo antes de respuesta del servidor.

## 5. Cambios técnicos propuestos

### Dominio y persistencia

- Introducir entidades de pedido de restaurante y división. No reutilizar `PurchaseOrder` ni `ProductionOrder` como pedido de cliente.
- Incorporar identificadores históricos de terminal/sucursal/bodega y relaciones de pedido/cuenta en venta.
- Índices únicos para operación idempotente, líneas cobradas por pedido, sesiones activas y arqueo único; revisar compatibilidad con datos históricos antes de imponer restricciones.
- Migraciones EF Core versionadas, con diagnóstico previo de nulos/duplicados y estrategia explícita de backfill. No asignar una bodega histórica arbitraria.

### Aplicación y API

- `SaleService` orquesta validación y escritura; extraer servicios de pedido/división si el caso de uso lo requiere. Reutilizar `IRecipeOperationsService`, servicio de precios y ledger de inventario.
- Los controladores MVC/API reciben contratos pequeños y delegan reglas al backend. Añadir versión de pedido y clave de idempotencia al confirmar.
- Los errores distinguen validación, permiso, conflicto de concurrencia, caja cerrada, stock insuficiente y operación ya confirmada.
- Todas las acciones de sucursal/caja/bodega validan alcance de usuario en servidor.

### Interfaz y operación

- Crear pantallas de pedido y división de cuenta y extender formulario de venta y caja existente, sin reemplazar MVC/Razor por otro framework.
- Mostrar auditoría y enlaces entre pedido, venta, pago, movimientos, devolución y reembolso desde el detalle.
- Instrumentar logs de fallo y conflicto sin datos sensibles de tarjeta, contraseñas ni secretos.

## 6. Diseño técnico ejecutable

### 6.1 Modelo de datos objetivo

| Entidad o extensión | Campos y relaciones esenciales | Invariantes |
|---|---|---|
| `PosTerminal` | `Id`, código único, nombre, `BranchId`, `ConsumptionWarehouseId`, estado, versión de concurrencia. | Sucursal y bodega activas; bodega pertenece a sucursal. No asignar una terminal inactiva a una nueva caja. |
| `CashSession` (extensión) | `PosTerminalId`, `BranchId`, bodega fijada en apertura, usuario propietario, POS bancarios asignados. | El contexto de ubicación no cambia mientras la sesión está activa. Una sesión activa por usuario y punto de caja según reglas actuales. |
| `RestaurantOrder` | `Id`, número, sucursal, terminal de origen opcional, estado, versión, creador, marcas de tiempo y total histórico. | Solo una transición válida por vez. `Paid` requiere saldo cero. No eliminar pedidos con ventas. |
| `RestaurantOrderLine` | `Id`, `OrderId`, producto concreto, cantidad, precio bruto, descuento, impuesto, modificadores, estado de preparación y cantidades cobradas. | `0 <= cobrado <= cantidad`; snapshot económico después de `Ready`. |
| `RestaurantOrderModifier` | Línea, código/nombre, cantidad, importe, vínculo opcional a producto/receta y snapshot. | No consumir inventario por texto libre; solo componentes configurados. |
| `OrderBill` y `BillAllocation` | Cuenta padre, porción/persona, línea, cantidad, bruto, descuento, impuesto, neto en centavos, estado y venta resultante. | Ninguna asignación sobrepasa línea o importe pendiente; suma de porciones = saldo de cuenta. |
| `Sale` (extensión) | `OrderId`, `BillAllocationGroupId`, terminal, sucursal y bodega históricos, clave de operación. | Venta confirmada inmutable; una porción genera como máximo una venta confirmada. |
| Offline local | Clave de ámbito `{usuario, dispositivo, sucursal, terminal, caja, bodega}`, bootstrap, stock, cola y estado. | Una cola no se mezcla con otra ubicación; una operación confirmada no vuelve a consumir stock. |

Usar `decimal` para importes y cantidades de negocio. Para distribuir porciones de cuenta, convertir el importe ya redondeado a centavos enteros y volver a `decimal` al persistir ventas. Los productos y recetas siguen usando la precisión de unidad existente. Guardar UTC y presentar hora local conforme a la configuración vigente.

### 6.2 Transiciones del pedido y de la cuenta

```text
Draft -> Submitted -> InPreparation -> Ready -> PartiallyPaid -> Paid
   \         \             \         \
    -> Cancelled (solo la parte aún no cobrada, con motivo y auditoría)
```

- `Draft`: editar líneas y modificadores. `Submitted`: enviado a preparación. `InPreparation`: trabajo iniciado. `Ready`: listo para cobrar. `PartiallyPaid`: al menos una porción confirmada y saldo positivo. `Paid`: saldo cero.
- Al avanzar a `Ready`, fijar versión y snapshot de cada línea. Si un pedido aún no cobrado se corrige, incrementar versión e invalidar vistas de cobro anteriores. En `PartiallyPaid`, permitir solo nuevas asignaciones sobre saldo no cobrado y registrar la corrección como operación auditada; nunca mutar ventas previas.
- En un pedido por personas, la asignación de persona es una etiqueta y relación de reparto, no un cliente fiscal obligatorio. El cliente de cada venta hija se captura en su cobro.
- La cancelación de todo el pedido requiere cero ventas confirmadas. Si hay ventas, cancelar únicamente unidades no cobradas; una venta cobrada se corrige mediante anulación/devolución.

### 6.3 Contratos de aplicación propuestos

- `IPosContextService.ResolveAsync(user, terminalId, cashSessionId)` devuelve sucursal, bodega y permisos verificados. Cada caso de uso de venta y caja lo invoca en backend.
- `IRestaurantOrderService`: crear/editar, enviar, iniciar preparación, marcar listo, consultar y cancelar pendiente. Los comandos de edición incluyen `ExpectedVersion` para detectar modificaciones concurrentes.
- `IBillSplitService`: crear/recalcular porciones, obtener saldo y confirmar asignación. Cada porción posee ID estable y versión; confirmar requiere `ClientOperationId`.
- `ISaleService` incorpora origen de pedido/porción y contexto resuelto, manteniendo `CreateSaleAsync` para venta directa. El servicio nunca confía en precio, descuento, impuesto o bodega enviados por el navegador.
- `ICashService` abre sesión con terminal y contexto resuelto; un DTO de prearqueo omite esperado y diferencia; el DTO posterior al conteo puede incluir ambos.
- La API offline extiende bootstrap y sincronización con contexto de ubicación. El servidor compara los IDs recibidos con la sesión y rechaza cambios de ámbito.

Los nombres de interfaces son orientación para ubicar responsabilidades; conservar nombres existentes cuando ya cubran el contrato. La implementación concreta debe evitar duplicar servicios de precios, recetas, inventario y permisos.

### 6.4 Algoritmo de cobro y confirmación

```text
ConfirmarPorcion(command)
  validar identidad, rol POS y permiso ventas
  resolver terminal + caja activa + sucursal + bodega
  buscar ClientOperationId; si ya confirmado para el mismo ámbito, devolver resultado
  iniciar transacción
    cargar pedido, porción y versión con control de concurrencia
    comprobar Ready/PartiallyPaid y asignación aún no cobrada
    recalcular líneas, precios, impuestos, descuentos y total en servidor
    verificar aprobación de descuentos asociada a ese snapshot
    planificar receta y sumar demanda por componente/bodega/lote
    revalidar existencias y reservar/consumir sin saldo negativo
    validar medio de pago y estado final, crear pago
    crear venta hija y sus líneas históricas
    crear Kardex, movimiento de caja y auditoría
    marcar asignación como cobrada y actualizar saldo/estado del pedido
    guardar y confirmar transacción
  devolver número de venta y comprobante
```

El orden de persistencia puede adaptarse a EF Core, pero la visibilidad externa debe ser atómica. Si falla cualquier paso, revertir todo. Si dos cajas cobran la misma porción o el último stock, una confirma y la otra recibe conflicto recuperable con estado actualizado. Para venta directa, aplicar la misma secuencia sin pedido/porción. Para venta offline sincronizada, aplicar la misma ruta con clave idempotente y ubicación validada.

### 6.5 División exacta y pagos

- **Por productos:** asignar cantidades enteras o decimales válidas de cada línea a una porción. Si el artículo no permite fracción según su unidad, bloquear cantidades fraccionarias.
- **Por personas:** el operador distribuye líneas o partes de líneas entre etiquetas de persona. Una persona puede tener varias líneas y pagar una venta hija.
- **Por importes:** introducir importes positivos hasta completar el saldo. La asignación financiera por línea usa proporción respecto del saldo pendiente. Calcular componentes monetarios en centavos y entregar residuos a la última porción para que bruto, descuento, impuesto y neto cierren exactamente.
- **Pago:** cada porción se cobra con efectivo, transferencia o POS conforme a reglas existentes. Cambio de efectivo pertenece a esa porción. No registrar una porción como pagada hasta que su pago sea confirmable.
- **Comprobante:** cada venta hija recibe correlativo y comprobante propios; el pedido/cuenta muestra total, pagado y pendiente. El último cobro marca el pedido `Paid`.
- **Reembolsos:** operan sobre la venta hija original; la cuenta padre calcula el nuevo estado financiero desde ventas vigentes y devoluciones/reembolsos, sin borrar asignaciones históricas.

### 6.6 Concurrencia, índices y migración

1. Inventariar ventas/cajas existentes sin sucursal o bodega y reportarlas. No inventar ubicación histórica; conservar nulo legado con lectura segura y exigir ubicación en nuevas operaciones.
2. Crear tablas nuevas con claves foráneas, índices por `BranchId`, `OrderId`, `CashSessionId` y estados consultados; incorporar versión de concurrencia a pedido, cuenta y asignación.
3. Proteger `ClientOperationId` con índice único filtrado, porción cobrada con restricción única de venta válida y arqueo con unicidad por sesión. Mantener índices actuales de stock por producto/bodega.
4. Añadir columnas nuevas inicialmente compatibles con históricos. Exigir no nulo solo en entidades nuevas o después de un backfill verificable.
5. En operaciones simultáneas, traducir `DbUpdateConcurrencyException` y violación única a conflicto de negocio. No reintentar automáticamente un cobro con parámetros distintos bajo la misma clave.
6. Aplicar migraciones y comprobar conteos antes/después de ventas, pagos, movimientos, cajas y lotes. Los históricos conservan importes y referencias originales.

### 6.7 Secuencia de trabajo en el repositorio

| Lote | Archivos/áreas principales | Entrega comprobable |
|---|---|---|
| A | `docs/REQUIREMENTS.md`, `docs/BUSINESS_RULES.md`, `docs/DEVELOPMENT_PLAN.md`, `docs/ARCHITECTURE.md` | Fase 7 y reglas coherentes, incluyendo offline y pedidos. |
| B | `Domain/Organizations`, `Domain/Cash`, `Application/Cash`, `Infrastructure/Persistence`, configuración UI | Terminal, sucursal y bodega validadas en apertura y venta. |
| C | Nuevos módulos `Domain/RestaurantOrders`, `Application/RestaurantOrders`, repositorios EF y vistas MVC | Pedido preparado con versiones y modificadores. |
| D | `Application/Sales`, `Domain/Sales`, `Application/Recipes`, inventario y migraciones | Venta desde pedido, división, consumo y pago atómicos. |
| E | `Application/Cash`, `Views/Cash`, control administrativo | Cierre ciego, POS por dispositivo y conciliación trazable. |
| F | Devoluciones/reembolsos existentes, API offline, IndexedDB y vistas | Efectos separados y offline por ubicación. |
| G | `Views/Sales`, nueva UI de pedido/cuenta, estilos y documentación | Flujo táctil final y evidencia de criterios `POS-01` a `POS-12`. |

## 7. Criterios de aceptación y verificación para el agente

| Clave | Escenario mínimo |
|---|---|
| POS-01 | Terminal/caja de sucursal A no vende usando bodega de B; contexto histórico permanece visible. |
| POS-02 | Pedido preparado se cobra una vez; cambio de versión invalida el cobro abierto. |
| POS-03 | Pizza con receta y empaque descuenta componentes correctos en su bodega y conserva versión usada. |
| POS-04 | Dos cajas compiten por la última unidad: una confirma y la otra recibe conflicto; no hay saldo negativo ni efectos parciales. |
| POS-05 | Efectivo, transferencia y POS cumplen sus estados y montos; cada venta tiene un solo medio. |
| POS-06 | La suma de porciones equivale al total y cada línea/centavo se cobra una sola vez. |
| POS-07 | Un autorizador distinto aprueba; editar el carrito invalida aprobación y bloquea confirmación. |
| POS-08 | Una caja no muestra ni cierra movimientos de otra sucursal; POS asignados cierran antes de la sesión. |
| POS-09 | Conteo se captura antes de revelar esperado; diferencia se conserva y la revisión no edita el cierre. |
| POS-10 | Devolución parcial no excede venta; reposición y reembolso quedan ligados a documentos y caja correctos. |
| POS-11 | Datos offline de bodega A no aparecen en B; reenvío repetido crea una sola venta y conserva conflictos. |
| POS-12 | Venta completa por teclado/escáner y tacto en PC, tableta y teléfono, sin doble confirmación. |

El agente debe verificar también integridad transaccional, permisos backend, migraciones sobre una copia de datos con históricos, y regresión de ventas, inventario, recetas, caja, descuentos y offline existentes.

## 8. Entregables esperados

1. Documentos normativos actualizados con el alcance de la fase 7.
2. Modelo de datos, migraciones y contratos de aplicación/API.
3. Lógica completa `POS-01` a `POS-12` según la imagen y las reglas de la sección 3.
4. Vistas responsive y guía de uso de caja, cobro y sincronización.
5. Evidencia de aceptación por clave y lista de limitaciones reales.

## 9. Instrucción de ejecución para el agente de IA

> Implementa la Fase 7 conforme a la imagen y a este documento. Actualiza las reglas previas que contradigan el alcance aprobado. No conviertas un pedido de compra en pedido de restaurante. Reutiliza Domain, Application, Infrastructure y Web/MVC existentes. Mantén cálculos y autorización críticos en backend; confirma venta, inventario, pago, caja y auditoría de forma atómica. Conserva históricos e idempotencia. Implementa por lotes pequeños en el orden de la sección 1, muestra evidencia de aceptación por `POS-01` a `POS-12` y actualiza documentación cuando cambie alcance o regla.
