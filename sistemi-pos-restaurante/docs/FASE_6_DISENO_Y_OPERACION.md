# Fase 6: estructura visual y operación

Actualización del 19 de octubre de 2026. Se rediseñaron las siete pantallas del restaurante utilizando los colores, tipografía y componentes del sistema.

## Cambios

- Ajuste visual de Configuración: se conservan sus tres columnas, pestañas y acciones. Se reutilizan los colores globales azul oscuro, verde y grises, con bordes y sombras discretos. Las imágenes disponibles se conservan y los productos sin foto muestran iniciales; se retiraron emojis decorativos y se unificaron favoritos, cierres, filtros y controles de visibilidad. Revisar manualmente selección de sección/producto, todas las pestañas, selector, copia de menú y simulador.
- Pedidos: indicadores de abiertos, cocina, listos y en ruta; filtros automáticos, limpiar filtros, contador y estado vacío.
- Captura: servicio y cliente arriba; menú con fotos o iniciales, secciones de carta visibles y filtrables, y favoritos; selección de productos en panel lateral; mesa y dirección según el servicio. El total definitivo se revisa en el borrador del servidor.
- Detalle: datos de atención, bodega, total, estados de líneas, acciones, despacho, mesa e historial agrupados. Un borrador muestra sus líneas como sin enviar.
- Cocina: columnas En cola, Preparando y Listo, cantidad destacada, instrucciones, tiempos y filtros. La actualización automática se suspende durante un diálogo o una operación.
- Mesas: agrupación por salón, disponibilidad, ocupación y pedidos por turno. Se indica que liberar mesa no confirma pago.
- Delivery: entregas activas por defecto, filtros, datos del cliente, motorista, ruta, tiempos y última incidencia. Confirmar entrega se ofrece para delivery en ruta y para los otros servicios cuando están listos.
- Configuración: guía en tres pasos, tarjetas con ejemplos, secciones de carta propias de la sucursal, selector buscable de productos vendibles y editor de opciones por plato. Los insumos y empaques quedan fuera del selector; los preparados requieren receta activa y solo ofrecen exclusiones de componentes directos cuando se elaboran al pedir.
- Acciones: diálogos accesibles con confirmación, cantidad y motivo; errores dentro de la pantalla; moneda, posición y separadores configurados.

## Archivos principales

Views/Restaurant/*.cshtml, wwwroot/css/restaurant.css, wwwroot/js/restaurant.js, wwwroot/js/restaurant-config.js, wwwroot/js/restaurant-ui.js, Models/Restaurant/RestaurantLabels.cs y el contexto de navegación de Views/Shared/_Layout.cshtml.

## Revisión manual

1. Comprobar las siete pantallas en computadora, tableta y teléfono, incluyendo nombres e instrucciones largos.
2. Crear y editar borradores de salón, llevar y delivery; comprobar que cambian los campos obligatorios y se conservan productos, extras y combos.
3. Filtrar pedidos, menú, cocina y entregas; comprobar los mensajes sin resultados y la limpieza de filtros.
4. Abrir un diálogo de cocina, esperar un intervalo de actualización, cancelar y volver a abrirlo; después confirmar cantidad, motivo y operación.
5. Preparar parcialmente, marcar listo, cancelar y revisar el historial; comprobar los permisos de cada perfil.
6. Asignar motorista, salir a reparto, registrar incidencia y entregar. Verificar que no se ofrece entregar un delivery antes de salir.
7. Crear secciones Pizzas, Acompañamientos y Bebidas; agregar productos desde el selector y asignar estación y orden. Verificar que insumos y empaques no aparezcan y que una pizza con receta elaborada al pedir ofrezca solo sus componentes directos como exclusiones.
8. Comprobar importes con la moneda y separadores configurados.

## Límites del cierre

Validación técnica de esta actualización: compilación de Web y dependencias con configuración Phase6Verification, sin restaurar paquetes, terminada con 0 errores y 0 advertencias; node --check de restaurant.js y restaurant-config.js, y git diff --check sin errores. Los recorridos visuales y de navegación corresponden a la revisión manual del proyecto.

Este rediseño no constituye aceptación del flujo de inventario contra PostgreSQL ni despliegue de la migración. La integración pedido–venta, los pagos y el cierre financiero corresponden a fase 7. El cierre definitivo de entrega fallida requiere la decisión operativa descrita en la revisión del plan. Las consultas actuales todavía cargan el historial de pedidos de la sucursal; los filtros visuales no sustituyen paginación de servidor.
