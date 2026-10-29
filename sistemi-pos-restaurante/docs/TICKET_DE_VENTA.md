# Ticket de venta

El botón «PDF» ahora solicita un trabajo en segundo plano y permite consultar/descargar el resultado. Los tickets HTML y el comprobante A4 imprimible conservan su acceso inmediato. El alcance, reintentos y despliegue se documentan en `COLA_PDF_RABBITMQ.md`.

Actualización de presentación: 26 de octubre de 2026.

`GET /Sales/Ticket/{id}` muestra un comprobante HTML independiente, preparado para impresoras térmicas. Conserva la autorización y el alcance de venta existentes. `?format=text` mantiene el acceso a la salida de texto original.

## Presentación

- Datos del negocio y logo únicamente cuando esté configurado, número de venta, fecha, cajero y terminal si se registró.
- Cliente y NIT del histórico de la venta.
- Nombre del producto en su propia línea, cantidades sin decimales innecesarios, precio unitario e importe alineado. Los descuentos aplicados se identifican por importe.
- Total destacado; base imponible e impuesto incluido cuando la configuración permite mostrarlos. Se conserva la moneda, precisión y separadores históricos de la venta.
- Medios de pago en español, pagos mixtos desglosados y recibido/cambio una vez por pago en efectivo. Se muestra la referencia disponible y solo los últimos cuatro dígitos de tarjeta.
- Estado explícito para ventas anuladas o con devoluciones. Los importes siguen siendo los del comprobante original y no se reinterpretan como un reembolso.
- Pie configurado, agradecimiento y la aclaración de que el comprobante no sustituye una factura FEL. Se conserva el aviso de demostración si está configurado.

## Impresión

La vista comienza en 80 mm y permite seleccionar 58 mm. `?width=58` conserva el ancho en la URL; el botón imprime mediante el diálogo del navegador. La barra de acciones y las instrucciones quedan fuera de impresión. El tamaño de papel de la impresora debe coincidir con la selección y los encabezados/pies automáticos del navegador deben estar desactivados. La aplicación no modifica la configuración del dispositivo.

Archivos: `Views/Sales/Ticket.cshtml`, `wwwroot/css/sale-ticket.css`, `wwwroot/js/sale-ticket.js` y acción `SalesController.Ticket`. No se agregan dependencias, reglas financieras ni migraciones.

Revisión manual pendiente: ticket de 80 y 58 mm, nombres de productos y clientes largos, moneda histórica, descuentos, efectivo con cambio, pagos mixtos, ventas anuladas, logo ausente y logo configurado. Verificar impresión con el dispositivo real. La política del proyecto reserva esta revisión visual y de navegación al usuario.

## Comprobante A4

El botón «Imprimir comprobante» mantiene la ruta `/Sales/Print/{id}`. La vista separa la barra de acciones del documento y organiza emisor, número, estado en español, fecha, cajero, cliente, productos y liquidación. Los pagos se agrupan por medio; el impuesto se identifica como incluido y los importes/moneda proceden del histórico existente. Conserva la impresión automática solicitada por el flujo de venta y los botones Imprimir/Cerrar. Si el navegador impide cerrar la pestaña, vuelve al detalle de esa venta.

El logo solo se solicita cuando está configurado. Las rutas web se resuelven como URLs y las rutas de archivo locales usan el endpoint existente. Una imagen ausente o fallida se sustituye por la inicial del emisor, sin mostrar un icono de imagen rota ni utilizar la marca de otro negocio.

Los estilos aislados en `sale-receipt.css` preparan A4 con márgenes de 14 mm, cabecera de tabla repetible y filas protegidas de cortes. En pantalla pequeña el documento se apila y la tabla puede desplazarse horizontalmente. `sale-receipt.js` controla impresión, cierre y sustitución del logo. No se modifican operaciones ni cálculos financieros. Revisar manualmente impresión A4 de una venta y una venta larga, nombres extensos, logo válido/ausente, pagos mixtos, estado anulado, botón Cerrar y la impresión automática después de cobrar.

La segunda revisión visual elimina la altura A4 forzada en pantalla y las tarjetas internas de cliente y pagos; usa una barra de acciones verde oscuro, jerarquía tipográfica mayor, separadores finos y un bloque destacado del importe y estado. La impresión conserva A4 y transforma ese bloque a fondo blanco para no depender de la impresión de colores. Ambos importes destacados son el mismo total histórico y no representan cargos adicionales. La aceptación visual sigue pendiente del usuario.
