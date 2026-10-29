# Rediseño de la pantalla de ventas

Fecha: 23 de octubre de 2026.

## Alcance implementado

Rediseño de `/pos`, usando como referencia visual la imagen aportada por la usuaria y la identidad actual de SISTEMI POS. Presentación y operación del catálogo y del pedido; no requiere migraciones ni paquetes nuevos.

- Cabecera azul marino con sucursal de operación y conteos reales de catálogo, categorías y líneas del pedido.
- Catálogo de tarjetas con fotografía registrada, categoría, nombre, precio, existencia y detalle. Un toque agrega una unidad; los productos agotados permanecen bloqueados.
- Marcador visual para productos incluidos en el pedido. Las imágenes ausentes o fallidas muestran un reemplazo local, sin descargar fotografías de ejemplo.
- Filtros por las categorías reales del catálogo; búsqueda por nombre, categoría, código interno o código de barras; presentación de tarjetas o lista.
- F2 enfoca el buscador. Enter agrega la coincidencia exacta y única de código interno o barras, incluso fuera de la categoría activa. Si no existe código exacto, agrega únicamente cuando hay un solo resultado visible. Los casos ambiguos no agregan productos automáticamente.
- Pedido lateral con cantidades, descuentos existentes, subtotales y total destacado. Estado vacío explícito.
- Cobro principal y accesos a efectivo, tarjeta/POS y transferencia según las formas habilitadas. Estos accesos abren el flujo existente; no registran automáticamente una venta.
- Se conservan el diálogo de clientes, autorizaciones de descuento, proforma y acceso existente a cobro mixto/división de cuenta.
- Distribución en dos columnas en escritorio y una columna cuando el área disponible es estrecha. Controles táctiles ampliados y foco de teclado visible.

## Criterios de diseño

Se conserva el azul `#12263A`. Las acciones principales de esta pantalla usan `#347A0A` con texto blanco. Los estilos están delimitados por `.pos-sales-workspace` para no rediseñar otros módulos.

La tarjeta visual y la agrupación por categorías también se encuentran en el patrón de catálogo descrito por [Square en su documentación oficial](https://squareup.com/help/us/en/article/8334-set-up-item-grid). Se adaptó la presentación al catálogo y a las operaciones existentes de SISTEMI POS.

La referencia contiene mesas, indicadores diarios y porcentajes. Esos elementos no se presentan con datos ficticios ni se convierten en nuevas funciones dentro de la venta directa. La selección de productos, la preparación en cocina, los permisos y las reglas de cobro mantienen sus flujos actuales.

## Archivos principales

- `src/SistemiPos.Web/Views/Sales/Form.cshtml`
- `src/SistemiPos.Web/wwwroot/css/site.css`
- `src/SistemiPos.Web/wwwroot/js/site.js`

## Revisión manual pendiente

1. Ingresar como usuario autorizado de Punto de Venta, con caja abierta, y abrir `/pos`.
2. Revisar fotografías cargadas y faltantes, categorías, búsqueda y alternancia de tarjetas/lista.
3. Agregar productos por toque y lector de barras; comprobar cantidades, descuentos, eliminación y total.
4. Revisar efectivo, tarjeta/POS, transferencia, cobro mixto, división de cuenta y proforma con los datos y permisos del entorno.
5. Comprobar el bloqueo de productos agotados y la actualización de existencia después de vender.
6. Revisar escritorio, tableta y teléfono; verificar que las acciones y los importes sigan legibles.

## Validación técnica

- Compilación Release del proyecto web, incluidas las vistas Razor: correcta, 0 errores y 0 advertencias. Se completó fuera del entorno restringido después de que los intentos dentro de ese entorno fallaran o quedaran bloqueados.
- Revisión de sintaxis de `site.js` con `node --check`: correcta.
- Revisión de espacios y conflictos con `git diff --check`: correcta.

No se realizaron pruebas funcionales ni navegación visual, conforme a la política del proyecto. La presentación de fotografías depende de las imágenes configuradas en cada producto.
