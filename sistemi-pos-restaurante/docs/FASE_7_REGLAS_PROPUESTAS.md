# Fase 7: reglas propuestas para pedidos, preparación y despacho

Estado: reglas aprobadas expresamente por el usuario el 20 de octubre de 2026 («Sí, vamos, trabajemos en eso»). La aprobación define el comportamiento esperado; no certifica su implementación completa.

## Decisiones ya autorizadas

- Ventas offline exclusivamente en POS, conservando los límites existentes de RF-OFF y BR-OFF: siete días, 500 pendientes por dispositivo, sin descuentos ni crédito offline y revalidación en servidor.
- Incluir dependencias de pedidos de restaurante, preparación y despacho; definir primero sus reglas.
- Conservar los controles existentes de permisos, sucursal, dinero decimal, auditoría e históricos.

## Reglas aprobadas

1. El pedido empieza en borrador. Confirmarlo fija por línea producto, cantidad, precio, impuestos, descuentos aprobados, modificadores y versión expandida de receta. Cambios de catálogo o receta posteriores no recalculan ese histórico. Un cambio posterior de cantidades o modificadores crea una revisión trazable; una línea cuya preparación comenzó no se reescribe ni se borra.

2. Las líneas elaboradas transitan por pendiente, en preparación, preparada y despachada; las de reventa pasan de pendiente a despachada. Los estados agregados del pedido se derivan de sus líneas y cantidades, permitiendo avance parcial. Cada transición valida permiso y ubicación en backend. No se exige que todas las líneas del pedido avancen juntas.

3. Iniciar preparación consume ingredientes según la receta y modificadores históricos, proporcionalmente a la cantidad que empieza a prepararse. Si falta stock, se rechaza toda esa transición. Las subrecetas se expanden a sus componentes; esta entrega no incorpora producción por lotes ni stock de subrecetas elaboradas. Debe confirmarse expresamente si el restaurante necesita ese otro modelo antes de implementarlo.

4. Reventa consume unidades al despachar. Una venta directa de reventa consume al confirmarse. Un preparado vendido directamente requiere un pedido asociado y preparación registrada. El cobro no repite consumos ya realizados. Cada consumo conserva línea, evento, ubicación y cantidades; reintentar no duplica movimientos.

5. Los empaques incluidos en una receta se consumen con esa receta. Los empaques adicionales del despacho se consumen al despachar y se identifican como adicionales, evitando volver a descontar los ya incluidos. Se registra su cantidad por línea y ubicación.

6. Cancelar antes de iniciar preparación no genera devolución de ingredientes. Una vez iniciada, cancelar o reembolsar no repone automáticamente ingredientes ni empaques consumidos. Las cantidades consumidas se conservan y una merma se documenta sin descontarlas de nuevo. Una devolución física de reventa solo repone unidades declaradas aptas y recibidas, con autorización y movimiento relacionado. Reembolso y recepción física son efectos independientes.

7. Venta desde pedido requiere que las cantidades a vender estén preparadas o despachadas; las de reventa pueden despacharse atómicamente con la venta. Se permite vender una parte del pedido mediante asignaciones persistentes de cantidades; nunca volver a vender la misma cantidad. Esto permite dividir por productos sin crear ventas duplicadas del pedido completo. Esta propuesta no incluye anticipos ni cobro previo a preparación.

8. División por productos asigna cantidades disponibles de líneas; por personas reparte el saldo entre el número indicado; por importes usa valores positivos cuya suma coincide exactamente con el saldo. El residuo de redondeo se asigna a la última partición. Las divisiones por personas o importes no multiplican líneas ni consumos. Cada pago pertenece a una partición; no se permite editar una partición ya cobrada. Unicidad e idempotencia se validan en servidor.

9. Pagos mixtos conservan cada método e importe aplicado. El cambio solo procede de efectivo recibido y no se contabiliza como ingreso; los importes aplicados suman el total exacto. Los reembolsos parciales o totales conservan relación con los pagos originales y no superan lo pagado menos reembolsos previos. El efectivo afecta la caja activa que lo entrega, con trazabilidad a la venta original.

10. Offline conserva por dispositivo usuario, sucursal, terminal, caja y bodega del contexto sincronizado. Cambiar de ubicación no reasigna operaciones pendientes. La sincronización revalida permisos, caja, stock e históricos; un conflicto permanece pendiente y visible, sin ajuste automático ni borrado. El stock local es estimado y no garantiza disponibilidad entre dispositivos desconectados. Se propone mantener preparación/despacho y conversiones de pedidos existentes como operaciones conectadas; offline admite ventas directas de reventa. Ampliar offline a preparados exige aprobar además captura local de pedidos, recetas y eventos de consumo, hoy fuera de RF-OFF-002.

## Secuencia de implementación

1. Terminal operativa y sus vínculos; pedidos y revisiones históricas.
2. Preparación/despacho, consumos por tipo y devoluciones físicas.
3. Conversión parcial a ventas, pagos mixtos y divisiones.
4. Caja por terminal, cortes y conciliación; integración de autorizaciones.
5. Aislamiento offline, sincronización y conflictos.
6. Integración Razor y pruebas automatizadas relevantes, incluyendo PostgreSQL concurrente. La revisión visual/manual corresponde al usuario.

Los cambios de persistencia, migraciones y endpoints se desarrollan contra estas reglas, conservando ventas y pagos históricos. El estado técnico y las verificaciones de cada bloque se registran en `FASE_7_POS_IMPLEMENTACION.md`.
