# SISTEMI POS — Reglas consolidadas de interfaz, botones, columnas, filtros y comportamiento de pantallas

> Actualización aprobada del 24/10/2026: el selector de terminal existente del POS determina el turno físico/lógico que se consulta o abre. La caja conserva un cajero responsable y no se opera un turno ajeno. La barra contextual, la apertura integrada y el bloqueo de cobros nuevos sin conexión se describen en [Caja en el punto de venta](CAJA_EN_PUNTO_DE_VENTA.md). Esta actualización prevalece para `/pos` sobre las reglas anteriores que contradigan esa especificación; no habilita edición de cajas cerradas.

> Documento de referencia para Codex Agent.
>
> Este archivo consolida decisiones tomadas durante la construcción de SISTEMI POS sobre interfaz, navegación, botones, columnas, filtros, formularios, modales y comportamiento visual.
>
> **No agregar requisitos nuevos.**
>
> Si una definición antigua contradice una definición posterior, debe prevalecer la **definición más reciente**.

---

# 1. Reglas generales de interfaz

## Módulo de recetas

Acceso propio `Recetas` dentro de la sección Inventario, sujeto a `recipes.view`. El listado permite buscar por producto/código y filtrar por estado. Crear y editar reutilizan el modal CRUD; el detalle muestra rendimiento, cantidades por elaboración, equivalentes en unidad base y enlaces a subrecetas. La captura usa selectores de productos inventariables del catálogo existente y mantiene los componentes al mostrar errores. El permiso `recipes.manage` controla las acciones de administración.

## 1.1 Diseño compacto

Todas las vistas administrativas deben aprovechar mejor el espacio disponible.

Aplicar de forma consistente en:

- Compras.
- Inventario.
- Productos.
- Proveedores.
- Clientes.
- Caja.
- Caja chica.
- Cuentas por cobrar.
- Cuentas por pagar.
- Administración.
- Demás módulos administrativos.

Reducir espacios verticales innecesarios entre:

- breadcrumb;
- título;
- acciones;
- descripción;
- filtros;
- buscador;
- contenido;
- tablas;
- botones finales.

No dejar grandes espacios vacíos.

La información principal debe aparecer lo más arriba posible sin perder claridad visual.

---

## 1.2 Un solo título principal

Cada pantalla debe tener un único título principal.

No repetir dentro de tarjetas:

- el mismo título;
- subtítulos innecesarios;
- descripciones que ya son obvias por el contexto de la pantalla.

---

## 1.3 Breadcrumb

El breadcrumb es únicamente informativo.

Reglas:

- No debe ser clickeable.
- No debe utilizarse para regresar.
- No debe redirigir al Dashboard.
- Evitar breadcrumbs genéricos como:

`SISTEMI POS > ...`

Usar contexto real del módulo.

Ejemplos:

- `Compras > Órdenes de compra`
- `Inventario > Toma física`
- `Productos > Editar producto`
- `Proveedores > Detalle`

---

# 2. Botón estándar para regresar

En todo el sistema utilizar una única forma de regresar:

`← Volver a [pantalla anterior]`

Ejemplos:

- `← Volver al historial`
- `← Volver a compras`
- `← Volver a productos`
- `← Volver a proveedores`

Reglas:

- Ubicar del lado izquierdo cerca del título principal.
- Mantener el mismo estilo.
- Mantener el mismo tamaño.
- Mantener el mismo contraste.
- Mantener la misma ubicación relativa.
- Debe verse como una acción de navegación.
- No debe verse como texto secundario.

Eliminar variantes inconsistentes como:

- `Regresar`
- `Volver`
- botones diferentes según módulo;
- navegación de regreso dentro del breadcrumb.

---

# 3. Creación y edición mediante modal

Como regla general, cuando el usuario esté dentro de un proceso y necesite crear o editar un registro relacionado, no abrir una nueva pestaña del navegador.

Usar modal o ventana flotante para:

- Producto.
- Proveedor.
- Cliente.
- Usuario.
- Categoría.
- Marca.
- Atributo.
- Valor.
- Catálogos relacionados.

Comportamiento esperado:

1. Abrir modal.
2. Crear o editar.
3. Guardar.
4. Cerrar modal.
5. Refrescar automáticamente el listado correspondiente.
6. Si se creó un registro nuevo dentro de otro proceso, seleccionarlo automáticamente cuando corresponda.

Esta regla aplica tanto a **Crear** como a **Editar**.

---

# 4. Mensajes y notificaciones del sistema


Todos los mensajes del sistema deberán utilizar componentes visuales estandarizados y no mensajes simples del navegador.

Utilizar el tipo de mensaje según su finalidad:

* **Toast:** para confirmaciones, información, advertencias y errores generales derivados de una acción.
* **Validación Inline:** para errores asociados directamente con campos de formularios.
* **Modal/Dialog de confirmación:** para operaciones importantes, sensibles, irreversibles o que requieran decisión explícita del usuario.
* **Banner:** para advertencias o problemas generales que afecten una pantalla, módulo o al sistema completo.

Los mensajes deberán manejar como mínimo los siguientes estados:

* Éxito.
* Información.
* Advertencia.
* Error.

Evitar utilizar `alert()` del navegador o mensajes técnicos directamente visibles al usuario.

Los mensajes deberán mostrarse siempre en español y con texto claro orientado al usuario.


# 5. Campos numéricos y monetarios

## 5.1 Campos enteros

Para cantidades y cualquier dato que deba ser entero:

- Permitir únicamente números enteros.
- No permitir decimales.
- Quitar las flechas de incremento/decremento del control numérico.

---

## 5.2 Campos monetarios

Para:

- Precio.
- Costo.
- Monto.
- Total.
- Subtotal.
- Otros valores monetarios.

Reglas:

- Quitar flechas de incremento/decremento.
- Utilizar la moneda configurada en el sistema.
- Respetar símbolo.
- Respetar formato.
- Respetar cantidad de decimales configurada.
- No asumir siempre dos decimales.

---

# 6. Tablas

## 6.1 Criterio general

Las tablas principales deben mostrar solamente información útil para operación y consulta rápida.

La información detallada de:

- auditoría;
- usuarios;
- fechas y horas de cada acción;
- observaciones extensas;
- trazabilidad completa;

debe mostrarse preferiblemente en **Ver detalle** cuando no sea necesaria en la tabla principal.

Evitar tablas excesivamente anchas.

---

## 6.2 Acciones

Cuando exista detalle de un registro, mantener una acción clara como:

`Ver`

Para edición:

`Editar`

Para eliminar elementos temporales o configuraciones que todavía pueden modificarse:

`Eliminar`

No agregar botones internos para navegar a pantallas que ya tienen acceso directo desde el menú lateral.

---

# 7. Filtros

## 7.1 Aplicación automática

Cuando una pantalla utilice filtros de consulta:

- aplicar filtros automáticamente al cambiar los valores cuando el patrón actual del módulo lo permita;
- evitar botones redundantes como `Filtrar` o `Aplicar filtros` cuando no sean necesarios;
- mantener únicamente `Limpiar` para restablecerlos.

---

## 7.2 Rango de fechas por defecto

En historiales donde se haya definido rango por fechas:

- **Desde:** primer día del mes actual.
- **Hasta:** fecha actual.

El usuario puede modificar ambas fechas.

---

## 7.3 Buscadores autocompletables

Cuando una entidad tenga muchos registros, evitar selects largos.

Usar búsqueda/autocompletado cuando esté definido para:

- Proveedor.
- Producto.
- Categoría.
- Marca.
- Usuario.
- Cliente/Nombre cuando corresponda.

---

# 8. Productos

# 8.1 Tabla principal de productos

No mostrar como columnas visibles de la tabla principal:

- Código.
- Código de barras.
- Estado.

Estos datos pueden mantenerse internamente y consultarse donde corresponda, especialmente en edición/detalle.

La tabla principal debe mantenerse limpia y enfocada en la información operativa.

---

# 8.2 Código interno

No solicitar código interno al crear o editar un producto.

Reglas:

- Debe generarse automáticamente.
- No mostrarlo como campo editable del formulario.
- No obligar al usuario a ingresarlo.

---

# 8.3 Código de barras

No mostrar el código de barras dentro de la sección de información general.

Debe manejarse en **Atributos y Variantes**.

### Producto sin variantes

Mostrar un único campo:

`Código de barras`

### Producto con variantes

El producto base no necesita un código de barras general.

Cada variante debe tener su propio:

- código;
- código de barras;
- stock;
- precio.

No permitir códigos de barras duplicados.

---

# 8.4 Crear y Editar producto

**Nuevo producto** y **Editar producto** deben utilizar exactamente:

- misma estructura;
- mismos campos;
- mismas secciones;
- mismo orden;
- mismos márgenes;
- mismo espaciado;
- mismos componentes;
- mismas validaciones;
- mismos autocompletados;
- misma lógica de atributos;
- misma lógica de variantes;
- misma lógica de multiprecios.

No mantener dos diseños diferentes.

El formulario debe ser compacto.

---

# 8.5 Categoría y Marca

Cambiar ambos campos de select tradicional a buscador con autocompletado.

Al escribir:

- filtrar coincidencias existentes;
- mostrar sugerencias;
- permitir seleccionar una opción.

Mantener:

- `+ Crear categoría`
- `+ Crear marca`

Si no existe el valor, debe poder crearse desde el proceso correspondiente mediante modal.

Categoría y Marca son opcionales.

Si no se seleccionan:

- Categoría = `Sin categoría`
- Marca = `Sin marca`

Ambos valores deben existir dentro de sus catálogos y aparecer en búsqueda.

---

# 8.6 Atributos, Valores y Variantes

Mostrar una explicación corta:

**Atributo:** característica del producto, por ejemplo Talla, Color o Material.

**Valor:** opción correspondiente al atributo, por ejemplo 32, Azul o Algodón.

Al agregar:

- atributos;
- valores;
- variantes;

los cambios deben mostrarse inmediatamente.

No exigir:

- guardar primero el producto;
- recargar la página.

Permitir editar o eliminar cuando corresponda.

No crear una segunda lógica de variantes para Compras.

Compras debe reutilizar exactamente la misma estructura de variantes existente en Productos.

---

# 8.7 Multiprecios

Mantener opción:

`Habilitar multiprecios`

Tipos:

- Cantidad mínima.
- Cantidad exacta.

Mostrar ambas opciones de forma compacta, preferiblemente en una sola línea.

Tabla de multiprecios:

`Cantidad | Precio | Estado/Activo | Acciones`

Al guardar un multiprecio:

1. cerrar modal;
2. agregarlo inmediatamente a la tabla;
3. no exigir recargar;
4. no exigir guardar primero el producto.

Permitir antes de guardar el producto:

- Editar.
- Eliminar.

Si existen varios multiprecios, cada uno debe aparecer en su propia fila.

---

# 8.8 Impuesto y producto sin movimiento

Mantener:

`Aplica impuesto`

Cambiar el texto:

`Usar configuración general para producto sin movimiento`

por:

`Usar días generales sin movimiento`

Si está marcado:

- usar configuración general;
- ocultar o deshabilitar `Días específicos sin movimiento`.

Si está desmarcado:

- mostrar `Días específicos sin movimiento`;
- permitir únicamente enteros positivos;
- no permitir decimales;
- quitar flechas del campo.

---

# 8.9 Botones de Producto

Usar:

`Guardar producto`

en lugar de:

`Guardar`

Mantener navegación de regreso con el patrón global:

`← Volver a productos`

o el destino real correspondiente.

---

# 9. Punto de Venta / Ventas

## 9.1 Crear venta

Las ventas nuevas deben iniciarse desde la pantalla principal del usuario **Punto de Venta**.

Quitar el botón:

`+ Nueva venta`

del historial de ventas.

Esta regla aplica a todos los perfiles.

No utilizar el historial de ventas como entrada para iniciar una venta.

---

## 9.2 Acceso por perfil

El usuario **Punto de Venta** es quien debe utilizar la pantalla para crear ventas.

Los perfiles administrativos que no tengan función de venta directa no deben recibir el botón de Nueva venta únicamente por tener permisos administrativos.

Las restricciones deben respetarse también en backend y no solamente ocultando botones.

---

## 9.3 Línea de venta

La información principal por línea debe mantener el criterio operativo:

- Producto.
- Cantidad.
- Precio unitario.
- Descuento cuando aplique.
- Subtotal.

Las cantidades pueden modificarse antes de confirmar.

---

# 10. Compras — nomenclatura actual

La denominación vigente es:

**Órdenes de compra**

No utilizar nuevamente **Pedidos de compra** como nombre visible del módulo.

Actualizar textos visibles como:

- `Nuevo pedido` → `Nueva orden`
- `Detalle de pedido` → `Detalle de orden`
- `Pedido de compra` → `Orden de compra`

La numeración existente puede mantenerse aunque conserve prefijo histórico.

---

# 10.1 Menú Compras

Mantener **Compras** como opción principal desplegable.

Orden:

1. Borradores.
2. Solicitadas.
3. Recepciones.
4. Cuentas por pagar.
5. Historial de compras.

No agregar botones internos para navegar entre estas pantallas cuando ya existe acceso desde el menú.

---

# 10.2 Estados operativos de Órdenes de compra

En listas activas mostrar solamente:

- Borrador.
- Solicitada.

No mezclar como activas:

- Recibidas.
- Canceladas.

Flujo:

`Borrador → Solicitada → Recibida parcialmente → Recibida`

Cada recepción confirmada genera una compra y muestra el pendiente restante. La acción `Registrar recepción` permanece disponible mientras exista cantidad pendiente.

El detalle de una compra confirmada ofrece `Anular compra` a usuarios con `purchases.cancel`, con motivo obligatorio y confirmación. Tras la anulación muestra motivo, usuario y fecha/hora; no ofrece editar, eliminar ni reconfirmar. El detalle de la orden conserva todas las recepciones y distingue `Vigente` y `Anulada`; sus cantidades y total operativo excluyen las anuladas. Anular una recepción reabre el pendiente para volver a recibir. La solicitud original permanece de solo lectura cuando existe historial de recepción, aunque el pedido vuelva a `Solicitada`.

Una orden cancelada no debe convertirse en compra.

---

# 10.3 Nueva Orden de compra

## Proveedor

Debe ser buscador autocompletable.

No usar un select largo.

Si el proveedor no existe:

- permitir crearlo desde el mismo proceso mediante modal;
- refrescar búsqueda;
- seleccionar el proveedor creado.

## Fecha

Tomar la fecha del sistema automáticamente.

No permitir modificación manual cuando la fecha corresponda a la creación de la orden.

## Productos

El buscador de Producto debe funcionar como el buscador de Proveedor:

- escribir;
- buscar;
- mostrar sugerencias;
- seleccionar.

Si el producto no existe:

- permitir crearlo mediante modal;
- regresar al proceso;
- seleccionarlo.

Permitir agregar múltiples productos.

No permitir agregar dos veces exactamente el mismo producto dentro de la misma orden.

---

# 10.4 Detalle de productos en Orden/Compra

Cuando exista información del proveedor, mostrar claramente:

- Nombre del producto según proveedor cuando aplique.
- Nombre del producto del sistema.
- Descripción del sistema.
- Descripción del proveedor.

No perder ninguna de las dos descripciones.

En una tabla compacta de captura puede utilizarse como base:

`Cantidad | Producto | Acción`

La celda **Producto** debe contener suficiente identificación para distinguir el producto del sistema y la referencia/nombre del proveedor.

---

# 10.5 Compra a crédito

La fecha de vencimiento solamente debe aparecer cuando la condición sea:

`Crédito`

Usar inicialmente los días de crédito configurados en el proveedor.

Permitir modificarlos manualmente cuando el proceso lo permita.

Si es contado, no mostrar controles de vencimiento innecesarios.

---

# 11. Historial de compras

Descripción:

`Consulta de todas las compras realizadas, directas o generadas desde órdenes de compra.`

Debe contener únicamente compras realmente realizadas:

- compras directas;
- compras generadas desde una orden recibida.

No mostrar compras en Borrador.

---

# 11.1 Filtros del Historial de compras

Usar:

- Proveedor — buscador autocompletable.
- Desde.
- Hasta.
- Condición — Todos / Contado / Crédito.
- Estado.
- Limpiar.

Si no se seleccionan fechas:

- Desde = primer día del mes actual.
- Hasta = fecha actual.

---

# 11.2 Columnas del Historial de compras

Mostrar únicamente:

`Fecha | Número | Proveedor | Factura | Condición | Origen | Total | Estado | Ver`

Quitar de la tabla:

- Usuario.
- Hora.
- Días de crédito.
- Fecha de vencimiento.
- Productos.
- Costos unitarios.
- Observaciones.

Esa información debe quedar en **Ver detalle**.

---

# 11.3 Origen de la compra

La columna debe llamarse:

`Origen`

Si proviene de una orden:

- mostrar la referencia correspondiente, por ejemplo `OC-000004` o el correlativo vigente.

Si no existió orden previa:

`Compra directa`

---

# 11.4 Botones del Historial de compras

Quitar:

`Ver pedidos`

No sustituirlo por otro botón similar.

La navegación entre las secciones de Compras se realiza desde:

- menú lateral;
- botón estándar `← Volver a...`.

---

# 11.5 Detalle de compra

Fecha/hora con formato similar a:

`01/09/2026 21:01`

Estados visibles siempre en español.

Mostrar moneda configurada en:

- Total.
- Costo unitario.
- Subtotal.

Separar correctamente:

- Número interno de compra.
- Factura/documento del proveedor.

Si no existe factura/documento:

`Sin documento`

Nunca usar automáticamente el número interno de compra como factura del proveedor.

Encabezado compacto, aprovechando el ancho disponible.

---

# 11.6 Trazabilidad de compra

Si proviene de Orden de compra, mostrar etapas reales:

- Borrador creado por + fecha/hora.
- Orden solicitada por + fecha/hora.
- Recepción realizada por + fecha/hora.
- Compra registrada/confirmada por + fecha/hora.

Si fue compra directa, mostrar únicamente las etapas que realmente existieron.

No usar un único campo genérico `Usuario` para representar toda la trazabilidad.

---

# 12. Inventario

# 12.1 Exportación

Las vistas de Inventario que ya tienen exportación definida deben permitir exportar a Excel según la vista actual.

Ejemplos:

- Existencias → exportar existencias.
- Kardex → exportar Kardex.

La exportación debe respetar el contexto/filtros de la vista cuando corresponda.

---

# 12.2 Toma física de inventario

Al iniciar una toma física:

- registrar nombre;
- registrar fecha;
- permitir inventario completo o por categorías.

La fecha debe provenir del sistema cuando corresponda.

---

# 12.3 Estados de conteo

Estados definidos:

- Pendiente.
- Contado.
- Reconteo requerido.

Mostrar la diferencia.

No utilizar campo `Motivo`.

Mantener comentario opcional.

---

# 12.4 Guardado temporal

Permitir guardar individualmente los conteos durante el proceso.

Cuando una línea quede guardada temporalmente, el botón o indicador correspondiente debe cambiar visualmente para que el usuario identifique que ya fue guardada.

---

# 12.5 Reconteo y finalización

No congelar todo el inventario durante la toma.

Si existe diferencia:

- solicitar reconteo cuando corresponda.

Al finalizar:

- realizar finalización transaccional;
- conservar trazabilidad;
- no permitir modificaciones destructivas posteriores.

---

# 13. Caja

## 13.1 Caja por usuario

La caja está asociada al usuario autenticado.

No mostrar selector de terminal al usuario para escoger una caja.

Reglas:

- un usuario no debe tener más de una caja/sesión activa simultáneamente;
- una caja cerrada no debe modificarse directamente;
- apertura y cierre deben conservar historial.

---

# 13.2 Apertura y cierre

La apertura y cierre pueden solicitar la misma contraseña utilizada para iniciar sesión cuando la operación lo requiera.

El cierre debe conservar:

- monto inicial;
- ventas;
- totales por forma de pago;
- otros ingresos;
- egresos;
- efectivo esperado;
- efectivo contado;
- diferencia.

---

# 13.3 Arqueo

Organizar visualmente:

- Billetes del lado izquierdo.
- Monedas del lado derecho.

Evitar una tabla excesivamente larga cuando pueda utilizarse esta distribución.

---

# 13.4 Historial de cajas

Filtros:

- Usuario — buscador/autocompletable.
- Desde.
- Hasta.
- Revisión/Estado.
- Limpiar.

Comportamiento:

- aplicar filtros automáticamente;
- no agregar un botón adicional `Filtrar`;
- ordenar de la sesión más reciente a la más antigua.

---

# 13.5 Auditoría de caja

Filtros:

- Usuario — buscador/autocompletable.
- Desde.
- Hasta.
- Evento/Movimiento.
- Limpiar.

Comportamiento:

- aplicar automáticamente;
- ordenar del evento más reciente al más antiguo;
- fecha/hora debe conservar suficiente precisión, incluyendo segundos cuando sea importante para auditoría.

No mostrar columnas que no aporten valor operativo.

Se había definido quitar de Auditoría columnas como:

- Sesión.
- Autorizó.

cuando la información ya se encuentra disponible mediante el contexto/detalle correspondiente.

---

# 13.6 Movimientos dentro de una sesión

Mostrar movimientos realmente útiles para comprender qué ocurrió mientras la caja estuvo abierta.

Mantener acceso al historial de ventas realizadas durante la sesión.

Quitar columnas que no aporten información útil cuando se encuentren vacías o sean redundantes, incluyendo las que se definieron previamente como:

- Venta.
- Estado.

cuando no generan valor en esa tabla específica.

---

# 14. Caja chica

## 14.1 Responsable

Cada Caja chica tiene un responsable único.

Por esa razón, no mostrar columna `Usuario` dentro del historial de Caja chica.

---

# 14.2 Ciclos

Cada apertura genera un ciclo independiente.

El historial del ciclo debe representar:

`Apertura → movimientos → cierre`

Una nueva apertura inicia un nuevo ciclo.

No mezclar movimientos de ciclos anteriores con el ciclo actual.

---

# 14.3 Caja chica activa — filtros

La pantalla de Caja chica activa debe tener filtros para sus movimientos.

Filtros:

- Tipo de movimiento.
- Categoría.
- Desde.
- Hasta.
- Buscador por concepto, documento o proveedor/beneficiario.
- Limpiar.

Mostrar únicamente movimientos correspondientes al ciclo activo.

Aplicar filtros sobre la tabla de movimientos, no sobre el formulario de captura.

---

# 14.4 Caja chica activa — columnas

Tabla:

`Fecha | Tipo | Concepto | Categoría | Monto | Documento | Estado`

Ordenar de más reciente a más antiguo.

---

# 14.5 Categorías de Caja chica

Las categorías deben ser reutilizables.

Si una categoría no existe, permitir crearla mediante modal dentro del proceso y utilizarla inmediatamente.

---

# 15. Cuentas por cobrar

La organización vigente contempla vistas como:

- Resumen.
- Vencidas.
- En mora.
- Sin abonos.
- Parcialmente abonadas.
- Historial.

Usar:

`En mora`

en lugar de:

`Morosas`

---

# 15.1 Filtros de Resumen

En **Resumen** el filtro `Estado` sí debe existir.

Orden exacto:

1. Estado.
2. Nombre.
3. Fecha desde.
4. Fecha hasta.
5. Limpiar.

El filtro Estado debe aparecer antes de Nombre.

---

# 15.2 Filtros de vistas específicas

En las demás vistas específicas NO mostrar filtro `Estado`, porque la propia vista ya representa el estado o condición que se quiere consultar.

Ejemplos:

- Vencidas.
- En mora.
- Sin abonos.
- Parcialmente abonadas.
- Historial cuando aplique el mismo patrón.

Mantener los demás filtros relevantes, principalmente:

- Nombre.
- Fecha desde.
- Fecha hasta.
- Limpiar.

---

# 15.3 Rango por defecto

Cuando aplique rango de fechas y el usuario no haya seleccionado otro:

- Fecha desde = primer día del mes actual.
- Fecha hasta = fecha actual.

---

# 16. Cuentas por pagar

Mantener acceso dentro de Compras.

No duplicar navegación con botones internos innecesarios cuando ya exista acceso desde el menú lateral.

Aplicar los mismos criterios globales:

- pantallas compactas;
- filtros cerca del encabezado;
- tablas con información operativa;
- detalle para trazabilidad completa;
- importes según moneda configurada.

---

# 17. Historiales y Auditorías

Todos los historiales y pantallas de auditoría que hayan sido definidos con filtros deben conservarlos.

No dejar pantallas de historial o auditoría sin filtros cuando el módulo ya requiere consulta por:

- usuario;
- fechas;
- estado;
- evento;
- tipo;
- entidad relacionada.

Mantener el patrón:

- filtros visibles arriba;
- resultados inmediatamente debajo;
- aplicación automática cuando corresponda;
- botón `Limpiar`;
- orden descendente por fecha.

---

# 18. Exportación a Excel

Cuando una pantalla tenga exportación definida:

- exportar los datos correspondientes a la vista;
- respetar los filtros activos;
- mantener encabezados comprensibles;
- utilizar valores visibles y formatos compatibles con Excel.

No agregar columnas técnicas que el usuario no ve ni necesita.

---

# 19. Dashboard

Mantener el patrón visual actual.

No convertir el Dashboard en una pantalla excesivamente extensa.

Priorizar indicadores realmente útiles para operación.

Las métricas nuevas deben conservar el mismo patrón visual de las existentes y no introducir bloques desproporcionados o secciones innecesarias.

---

# 20. Consistencia Crear / Editar / Detalle

Siempre que un módulo tenga Crear y Editar:

- reutilizar componentes cuando sea posible;
- evitar dos estructuras visuales diferentes;
- conservar mismos márgenes;
- mismos estilos de botones;
- mismas ayudas;
- mismos componentes de búsqueda.

Detalle debe ser de consulta y trazabilidad.

No convertir Detalle en una segunda versión del formulario de edición.

---

# 21. Regla para Codex sobre cambios visuales

Cuando se solicite modificar:

- un botón;
- una columna;
- un filtro;
- un texto;
- una posición;
- un margen;
- una tabla;
- un modal;

Codex debe modificar únicamente el alcance solicitado.

No aprovechar un cambio visual para:

- rediseñar módulos no solicitados;
- cambiar lógica de negocio;
- renombrar entidades no mencionadas;
- agregar funcionalidades;
- agregar columnas;
- agregar filtros;
- crear nuevos botones.

---

# 22. Precedencia de decisiones

Este documento contiene decisiones acumuladas de diferentes etapas del proyecto.

Cuando exista contradicción:

1. usar la decisión más reciente;
2. mantener la lógica actualmente implementada si no existe una instrucción posterior que la cambie;
3. no reintroducir nombres, botones o columnas que ya fueron eliminados.

Ejemplos:

- Usar **Órdenes de compra**, no volver a `Pedidos de compra`.
- Usar **En mora**, no volver a `Morosas`.
- No volver a agregar `+ Nueva venta` al historial.
- No volver a mostrar Código, Barras y Estado en la tabla principal de Productos.
- No volver a agregar `Usuario` a tablas donde se decidió mover esa trazabilidad al detalle.
- No convertir breadcrumbs nuevamente en enlaces.

---

# 23. Regla final

Antes de implementar una pantalla nueva o modificar una existente, Codex debe revisar este documento y respetar estas reglas como estándar de SISTEMI POS.

No interpretar este documento como autorización para modificar todo el sistema de una vez.

Las reglas deben aplicarse cuando se trabaje en la pantalla o módulo correspondiente.

Las pruebas funcionales y visuales serán realizadas manualmente por el usuario.

### Recetas y producción — fase 5

El detalle enlaza versiones, costos y disponibilidad por bodega. Producción tiene listado, captura de receta/lote/origen/destino y confirmación de rendimiento real con motivo de diferencia. Los permisos se verifican también en backend y las bodegas se limitan a sucursales autorizadas. Las tablas admiten desplazamiento horizontal y los formularios reutilizan los controles táctiles del sistema.
