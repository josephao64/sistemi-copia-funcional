# Revisión del plan ERP y replanteamiento de la fase 6

**Fecha:** 19 de octubre de 2026  
**Fuente:** `Plan_Implementacion_SISTEMI_POS_ERP.docx` proporcionado por el usuario, contrastado con los informes y el código local.  
**Alcance:** evaluación del plan y propuesta de secuencia. Este documento no afirma que se haya desplegado o probado la fase 6.

## Dictamen

El orden general del plan es correcto: organización y permisos → catálogo y unidades → inventario por bodega → compras → recetas y producción → pedidos, cocina y delivery → venta, pagos y caja → facturación y reportes. Las dependencias de `ORD-01` a `ORD-10` son razonables y protegen la separación por sucursal y el consumo único de inventario.

La fase 6 **está incorporada al código, pero no debe marcarse como terminada** según la definición de terminado del propio plan. La compilación y la migración EF se prepararon; faltan aplicación sobre una base de prueba con datos representativos, pruebas automatizadas y funcionales, concurrencia real en PostgreSQL, conciliación y piloto. La guía local de fase 6 declara expresamente esos pendientes.

## Relación con las fases anteriores

| Base requerida | Situación observada | Comprobación antes de cerrar fase 6 |
|---|---|---|
| Fases 1–2: sucursales, alcance, catálogo y unidades | Hay informes de implementación y funciones que fase 6 reutiliza. | Verificar un usuario con dos sucursales, un producto con precio/estado local y bloqueo de acceso cruzado por URL. |
| Fase 3: stock, Kardex y lotes por bodega | La fase 6 agregó reservas a la existencia y guardas en el ledger. | Conciliar `existencia física = disponible + reservado` por bodega/lote; probar venta o traslado paralelo a pedidos. |
| Fase 4: compras y recepciones | Existen migraciones de recepciones parciales; son fuente de stock y lotes. | Recibir un lote, reservarlo y comprobar que la cancelación no altera una recepción histórica. |
| Fase 5: recetas, subrecetas, producción y costos | Hay informe de fase 5 y lógica de receta versionada. | Producir masa, reservar pizza, cocinarla y comprobar que sale masa terminada una vez, sin volver a descontar harina. |

Los informes anteriores son evidencia de implementación, pero la aceptación de fase 6 exige comprobar el recorrido completo con la versión y la base de datos que realmente se vayan a usar.

## Flujo operativo recomendado

1. **Configurar** sucursal, bodega de consumo, menú local, estaciones, canales, mesas, zonas de reparto, modificadores, promociones y recetas activas.
2. **Capturar borrador** con sucursal, canal, mesa o entrega, líneas y precio calculado en servidor. El borrador no afecta inventario.
3. **Enviar comanda** y reservar componentes o artículos terminados por bodega y lote en una transacción. Guardar la versión de precio, receta, modificadores, impuestos y tarifa usada.
4. **Preparar o despachar** desde KDS. Al iniciar trabajo físico, transformar la reserva en consumo exactamente una vez. Una línea parcialmente preparada conserva la reserva de la parte pendiente.
5. **Marcar listo y entregar** con tiempos y responsables; delivery requiere asignación, salida, incidencia y confirmación de entrega.
6. **Resolver cambios y cancelaciones**: pendiente libera reserva; preparada registra merma; artículo terminado intacto solo reingresa mediante devolución física autorizada y trazable.
7. **Convertir el pedido en venta en fase 7** mediante un servicio transaccional e idempotente que vuelva a leer revisión y cantidades, genere venta y cobro, reconozca consumos previos y consuma una sola vez lo todavía reservado.
8. **Cerrar financieramente** mesa/caja después de las reglas de venta y pago; la liberación operativa de mesa de fase 6 no equivale a cuenta pagada.

### Ajuste al caso integral del plan

La frase del plan «pedido con o sin extra → venta y pago → consumo correcto» debe especificarse así: **pedido → reserva → preparación/despacho y consumo físico → entrega → venta/cobro y conciliación**, admitiendo anticipo o pago antes de preparar cuando la regla comercial lo permita. En todos los casos, venta y cocina comparten el mismo registro de consumo para impedir el doble descuento. El evento exacto de consumo depende del tipo de artículo y del comienzo de su preparación o despacho.

## Replanteamiento de fase 6 por bloques

| Bloque | Alcance | Salida verificable |
|---|---|---|
| 6.0 Prerrequisitos | Confirmar sucursales, bodegas, lotes, recetas y permisos de fases 1–5. Definir stock negativo, vencimiento, momento de consumo y política de cancelación. | Datos de prueba y reglas aprobadas; conciliación inicial. |
| 6.1 Pedido y menú (`ORD-01/02/03`) | Canal distinto de tipo de servicio, numeración, estados, precio local, edición de borrador e idempotencia externa. | Pedido histórico reproducible; transición inválida rechazada. |
| 6.2 Configuración comercial (`ORD-04/05/06`) | Extras, exclusiones, combos y promociones; snapshots de precio y componentes. | Total y consumo calculados en servidor y explicables por línea. |
| 6.3 Inventario y cancelación (`ORD-10`) | Reserva por bodega/lote, consumo al preparar, liberación y merma; concurrencia con otros escritores de stock. | Sin saldo negativo, doble consumo ni devolución ficticia. |
| 6.4 Operación (`ORD-07/08/09`) | Turnos de mesa, KDS por estación y delivery con tiempos e incidencias. | Flujo completo por cada canal, auditado y aislado por sucursal. |
| 6.5 Contrato de salida | Snapshot para fase 7, vínculo futuro pedido–venta, revisión, identificación de consumos y reglas de cuentas abiertas. | Contrato probado y aprobado; sin permitir cobro duplicado. |
| 6.6 Cierre técnico y operativo | Migración ensayada, pruebas, conciliación, rendimiento, seguridad, manual, capacitación y piloto. | Evidencia firmada de criterios `ORD-01`–`ORD-10`; decisión de pasar a fase 7. |

## Decisiones o faltantes que conviene añadir al backlog

1. **Pedido y venta deben tener vínculo único y durable.** Antes de fase 7, definir qué estados se pueden cobrar, si se permite cobrar antes de preparar y cómo se evita facturar dos veces el mismo pedido o una línea parcialmente cancelada.
2. **Mesa y pago tienen dos cierres distintos.** La liberación operativa existe en fase 6; en fase 7 debe impedirse perder o duplicar una cuenta pendiente, y debe definirse quién puede liberar una mesa con saldo abierto.
3. **Delivery necesita cierre de excepción.** Una incidencia hoy deja el pedido en ruta; definir entrega fallida, devolución a sucursal, reintento, reembolso/cobro contra entrega y su efecto en inventario y caja.
4. **Reintentos de plataforma externa.** Además de la referencia única, definir respuesta de reimportación: devolver el mismo pedido y estado, o un conflicto con enlace claro; evitar que operadores creen un duplicado manual.
5. **Visibilidad y capacidad.** Menú debe distinguir disponibilidad base de disponibilidad de extras/combos. Las listas de pedidos por sucursal deben paginarse y filtrarse antes de un historial grande.
6. **Criterios de aceptación medibles.** Añadir escenarios por canal, sucursal, último stock/lote, dos cocineros, modificación de receta/precio después de enviar, cancelación parcial y reintento de red.
7. **Piloto y reversión.** La migración de fase 6 debe aplicarse en una copia representativa, con respaldo y ventana controlada, porque el arranque de la aplicación aplica migraciones automáticamente fuera de Testing.

## Puerta de salida de fase 6

No avanzar a un POS que cobre pedidos reales hasta que se pueda demostrar en una base de prueba: (a) migración y restauración; (b) aislamiento de sucursal y permisos en backend; (c) reservas y consumos correctos bajo concurrencia; (d) cancelación pendiente/preparada/intacta; (e) pedidos de salón, llevar y delivery; (f) snapshots inmutables; (g) conciliación de stock, Kardex y costos; (h) prueba funcional de interfaz y un piloto acotado. El código de fase 7 puede diseñarse en paralelo, pero su activación operativa depende de esta puerta de salida.

## Estado de evidencia revisada

- El plan maestro exige compilación, migración aplicada en base de prueba, pruebas relevantes, seguridad backend y ausencia de efectos parciales para considerar terminada una tarea.
- `FASE_6_IMPLEMENTACION_Y_USO.md` registra compilación limpia y migración generada, pero también indica que no se aplicó a la base activa y que no se ejecutaron pruebas automatizadas ni recorrido de interfaz en esa entrega.
- El repositorio contiene `RestaurantOrder`, el servicio de pedidos, KDS, vistas, repositorio, migración EF y un endpoint de checkout que solo lee un snapshot. La venta desde pedido y el cobro corresponden todavía a fase 7.
