# Rendimiento del cobro y navegación

Fecha: 27 de octubre de 2026. Alcance: reducir consultas repetidas al pasar por catálogo, cobro, detalle y comprobante; conservar las reglas financieras y de autorización.

## Cambios

- El catálogo y sus variantes cargan las cantidades en una consulta por lote, en lugar de una consulta por producto. Se conserva la selección histórica de la primera existencia: no se suman bodegas ni se crea una nueva regla de ubicación. El POS sigue resolviendo la disponibilidad operativa mediante el servicio existente.
- La lectura del catálogo separa sus colecciones de presentaciones, precios y variantes, evitando multiplicar sus filas en una sola unión. El listado de ventas aplica la misma separación a artículos, pagos y divisiones de cuenta.
- Antes de calcular disponibilidad del catálogo POS se excluyen los tipos que la pantalla ya descartaba después. No se cambia qué productos admite la venta directa.
- El detalle de venta consulta la caja actual en vez de cargar todas las sesiones con movimientos y arqueos para terminar seleccionando solo esa caja. El comprobante deja de consultar un cliente cuyo resultado no se utilizaba.
- La validación de estado del turno, ejecutada antes del cobro, carga los movimientos necesarios y consulta la existencia de un arqueo mediante `EXISTS` en la misma consulta. No carga denominaciones, asignaciones ni cierres que ese resultado no usa. Los comandos de cierre conservan sus lecturas completas y validaciones.
- La configuración general y las sucursales autorizadas se reutilizan únicamente en la instancia del servicio de cada petición. Una nueva petición vuelve a consultar. No hay una caché global de permisos, saldos o estado del turno.
- El formulario verifica primero los campos requeridos y la conexión. Si faltan datos, informa sin esperar una petición de estado innecesaria. Con datos válidos sigue consultando el estado actual del servidor antes de enviar la venta. El botón indica «Validando caja…» y después «Registrando venta…» durante cada etapa.

No se paralelizan consultas sobre un mismo `DbContext`, se mantiene la autorización en backend y no se elimina la validación fresca de caja. No hay migraciones ni dependencias nuevas. La configuración operacional de moneda y las transacciones de ventas, stock y cierre conservan su comportamiento.

## Verificación

Las pruebas de catálogo verifican que se solicita un lote de existencias para múltiples productos y que se conservan las cantidades y los productos sin stock. La prueba PostgreSQL `NavigationQueryPerformanceTests` cuenta comandos: 40 productos usan una sola lectura para cantidades; las lecturas repetidas de configuración y sucursales no repiten SQL dentro de la instancia y una instancia nueva vuelve a consultar. Usa una base temporal aislada, no la base del negocio.

Las pruebas existentes de caja POS cubren estado, conteo ciego, alcance por sucursal, movimientos, cierre, reintentos y concurrencia. Las pruebas de ventas y catálogo cubren la compatibilidad de los cambios.

Resultado de la entrega: 64 pruebas unitarias de catálogo, ventas y caja aprobadas; 6 pruebas PostgreSQL de caja POS aprobadas y la prueba PostgreSQL de consultas aprobada. Compilación de los proyectos afectados y sintaxis de JavaScript correctas. No se ejecutó la suite completa ni se realizaron pruebas visuales o de navegación.

Estos conteos de consultas no son una medición de la latencia completa del equipo o la red. Revisión manual pendiente del usuario: pasar al cobro con datos completos/incompletos, confirmar una venta, abrir el comprobante, repetir con pagos mixtos y descuentos, cerrar/reabrir caja y comprobar el comportamiento sin conexión. Si persiste una pausa, registrar el paso exacto y su duración para medir esa petición específica.
