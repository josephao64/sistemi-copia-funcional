# DEVELOPMENT_PLAN.md

## Entrega de mensajería PDF - 27/10/2026

Se incorpora preparación PDF bajo demanda con Outbox PostgreSQL, RabbitMQ, consumidor idempotente, reintentos, cola de fallos y medición del flujo HTTP. Se conserva la confirmación síncrona de pagos/venta/caja/stock y el acceso inmediato al ticket HTML. Despliegue y verificación en `COLA_PDF_RABBITMQ.md`; no representa una integración con proveedor de pagos ni certificación de latencia en el navegador.

# Secuencia de implementación

## Fase 6 del plan de restaurante — Pedidos, cocina y delivery

El alcance actual es `ORD-01` a `ORD-10`, descrito en [Fase 6](FASE_6_PEDIDOS_COCINA_DELIVERY_IMPLEMENTACION.md). Incluye menú y configuración por sucursal, opciones/exclusiones, combos/promociones, pedidos y reservas, KDS, mesas, delivery y merma. Las secciones numeradas antiguas de este documento son la secuencia original del MVP y no sustituyen las fases nuevas. Fase 7 incorporará el cobro usando el contrato de checkout y consumo previo de restaurante.

Código y migración incorporados; activación y aceptación funcional pendientes. Ver [implementación y guía de uso](FASE_6_IMPLEMENTACION_Y_USO.md).

## 1. Base
- Solución y proyectos.
- Configuración.
- Persistencia.
- Migraciones.
- Manejo de errores.
- Auditoría base.

## 2. Seguridad
- `RF-SEG-*`

## 3. Catálogo
- `RF-PROD-*`
- `RF-CAT-*`
- `RF-BAR-*`

## 4. Proveedores y compras
- `RF-PRV-*`
- `RF-COM-*`
- `RF-COST-*`

## 5. Inventario
- `RF-INV-*`

## 6. Clientes
- `RF-CLI-*`

## 7. Caja
- `RF-CAJ-*`

## 8. Punto de venta y pagos
- `RF-POS-*`
- `RF-PAY-*`
- `RF-CARD-*`
- `RF-TRF-*`
- `RF-DES-*`
- `RF-TAX-*`
- `RF-NUM-*`
- `RF-CMP-*`

## 9. Operación conectada y offline exclusiva del POS
- `RF-CON-*`

El Punto de Venta registra sus operaciones directamente en el servidor cuando hay conexión. Por autorización expresa para la fase 7, admite ventas offline exclusivamente en POS según RF-OFF-001 a RF-OFF-007 y BR-OFF existentes: cola local, comprobante provisional, idempotencia y revalidación al sincronizar. Los demás módulos requieren conexión.

La fase 7 incluye las dependencias de pedidos de restaurante, preparación y despacho. Sus reglas de estados, consumo, restitución y relación con ventas fueron aprobadas expresamente y se conservan en `FASE_7_REGLAS_PROPUESTAS.md`. El avance y las limitaciones verificadas se documentan en `FASE_7_POS_IMPLEMENTACION.md`.


## 10. Anulaciones, devoluciones y reembolsos
- `RF-ANU-*`
- `RF-DEV-*`
- `RF-REF-*`

## 11. Reportes y dashboard
- `RF-REP-*`
- `RF-DASH-*`

## 12. Configuración
- `RF-CONF-*`
- Validar configuración completa de moneda.

## 13. Validación
- Integridad.
- Autorización.
- Transacciones.
- Concurrencia.
- Rendimiento.
- Pruebas de reglas críticas.

## Entrega de creación y gestión de recetas

Entrega técnica documentada en [Recetas — implementación y guía de uso](RECETAS_IMPLEMENTACION_Y_USO.md), con revisión funcional y visual pendiente del usuario.

- Definición de recetas de Preparados y Subrecetas con cantidades y rendimiento en unidad base.
- Conversión de unidades mediante Catálogo, prevención de duplicados y ciclos, permisos y protección de referencias.
- Producción, consumo automático de ingredientes y costeo no forman parte de esta entrega.

## Pruebas del repositorio

La estructura y permanencia de los proyectos de pruebas se define en `TECH_STACK.md`.

Los proyectos de pruebas deben conservarse en Git durante todo el ciclo de desarrollo y no deben agregarse al `.gitignore`.


## Entrega de fase 8: control, reportes y respaldos

Se incorporó el centro de control por ubicaciones autorizadas, auditoría contextual, costos históricos adicionales, exportación paginada, expedientes fiscales y respaldo PostgreSQL programable. Véase [FASE_8_CONTROL_REPORTES_Y_RESPALDOS.md](FASE_8_CONTROL_REPORTES_Y_RESPALDOS.md) para cobertura CTL-01 a CTL-10, manual, permisos, migración y validación.

El cierre operativo mantiene pendientes explícitos: contrato/adaptadores de certificación FEL y consulta NIT, configuración del servidor de respaldos, simulacro de recuperación y aceptación funcional. La emisión fiscal productiva no está habilitada sin contrato. No se afirma cierre completo por disponer del código de preparación.
