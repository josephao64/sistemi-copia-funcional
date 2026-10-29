# Reporte y manual de usuario de la fase 6

## SISTEMI POS Restaurante

Pedidos cocina y delivery

Versión documental 1.0 · 19 de octubre de 2026

Preparado para la administración y el personal operativo del restaurante, con un anexo para el equipo técnico. Documento elaborado a partir del código local y de la entrega de implementación de la fase 6.

### Resumen de la entrega

La fase 6 incorpora la operación del pedido desde su captura hasta la entrega: menú por sucursal, opciones y extras, combos, mesas, cocina, delivery y control del inventario comprometido. Cada pedido conserva su historial para consultar qué cambió, quién realizó la operación y cuándo ocurrió.

El código y la migración de base de datos están incorporados al proyecto. La compilación registrada terminó con cero errores y cero advertencias. Al cierre de la implementación, la migración no se había aplicado a la base activa y seguían pendientes las pruebas funcionales y visuales. Este reporte describe el comportamiento del código; no constituye una aceptación de operación en producción.

La relación de estos pedidos con ventas, pagos y caja se completará en la fase 7. Confirmar una entrega o liberar una mesa en esta fase no registra un cobro.

### Cómo utilizar este documento

| Sección | Contenido | Lectores principales |
|---|---|---|
| 1 y 2 | Alcance, estados y lógica del inventario | Administración y todo el personal |
| 3 y 4 | Configuración inicial del restaurante | Administrador |
| 5 | Captura y modificación de pedidos | Mesero y operador |
| 6 | Preparación y seguimiento en cocina | Cocina y despacho |
| 7 | Mesas y delivery | Mesero y responsable de entregas |
| 8 y 9 | Cancelaciones y ejemplos completos | Supervisor y personal operativo |
| 10 | Mensajes y solución de problemas | Todo el personal |
| 11 y 12 | Activación, evidencia técnica y aceptación | Responsable del sistema |

### Acceso al módulo

Después de activar la nueva versión, abrir **Restaurante** en la navegación. Según los permisos del usuario aparecen **Pedidos**, **Nuevo pedido**, **Cocina**, **Mesas**, **Delivery** y **Configuración**. El usuario de Punto de Venta dispone de un acceso a **Pedidos** en su navegación superior. Antes de operar, confirmar la sucursal de trabajo.

<!-- pagebreak -->

## 1 Alcance y resultado de la fase

El propósito de esta fase es coordinar la captura comercial, el trabajo de cocina y la entrega, conservando la relación de cada operación con sus existencias. El siguiente cuadro resume las diez tareas incorporadas al código.

| Clave | Función | Explicación para el usuario |
|---|---|---|
| ORD 01 | Canales y servicios | Distingue el origen del pedido de la forma de entrega. Conserva sucursal, bodega y referencia externa. |
| ORD 02 | Pedidos y comandas | Asigna un número, administra estados y guarda revisiones de los cambios. |
| ORD 03 | Menú por sucursal | Muestra productos, categoría, foto, favoritos, precio local y disponibilidad base. |
| ORD 04 | Modificadores y extras | Controla las opciones permitidas, su precio y el consumo adicional. |
| ORD 05 | Exclusiones | Permite retirar únicamente ingredientes configurados como excluibles. |
| ORD 06 | Combos y promociones | Valida selecciones y vigencias; conserva precios y componentes históricos. |
| ORD 07 | Mesas y turnos | Agrupa pedidos de una mesa en un turno y controla su traslado o liberación. |
| ORD 08 | Cocina por estación | Organiza las líneas pendientes, su preparación, tiempos y cambios recientes. |
| ORD 09 | Delivery | Registra zona, tarifa, motorista, ruta, salida, incidencias y entrega. |
| ORD 10 | Cancelación y merma | Libera reservas pendientes o registra desperdicio de lo ya preparado. |

### Aportes a la operación

El inventario reservado queda comprometido desde el envío del pedido. Esto permite que las ventas y otras salidas consideren cantidades que cocina todavía no ha utilizado. El consumo físico ocurre al iniciar la preparación o el despacho de la línea.

Los precios, las opciones y las recetas de las líneas enviadas se conservan como datos históricos. Cambiar el menú posteriormente afecta pedidos nuevos; las líneas anteriores mantienen su composición registrada.

Las estaciones trabajan por línea. Por ejemplo, una bebida puede estar lista mientras la pizza del mismo pedido sigue en preparación. El pedido completo aparece listo cuando todas sus líneas activas alcanzan ese estado.

### Límites de esta entrega

La captura de plataformas es manual. El módulo no incluye conexión automática a PedidosYa, aplicación para motoristas, geolocalización, optimización de rutas ni cobro de pedidos integrado. Las operaciones necesitan conexión al servidor. La fase 7 deberá enlazar ventas y pagos con el consumo que ya ocurrió en cocina.

<!-- pagebreak -->

## 2 Conceptos y lógica de funcionamiento

**Canal** es el origen comercial, por ejemplo Propio o PedidosYa. **Servicio** indica cómo se atiende: Salón, Para llevar o Delivery. Un pedido de PedidosYa puede ser para llevar; un pedido del canal Propio puede requerir delivery.

Una **comanda** es el trabajo que recibe una estación. Una **línea** representa un producto y sus opciones. El **turno de mesa** agrupa los pedidos de una ocupación. **KDS** es la pantalla de cocina. **Merma** es el registro de producto o componentes consumidos que ya no se entregarán al cliente.

### Estados del pedido

| Estado visible | Significado | Paso habitual |
|---|---|---|
| Borrador | Se capturó el pedido y puede editarse | Revisar y enviar a cocina |
| Enviado | Sus líneas están en cola y el inventario está reservado | Iniciar preparación |
| En preparación | Al menos una línea comenzó a trabajarse | Marcar líneas listas |
| Listo | Todas las líneas activas están listas | Entregar o asignar delivery |
| En ruta | Un delivery listo y asignado registró su salida | Registrar incidencia o entregar |
| Entregado | Se confirmó la entrega física | Consultar historial |
| Cancelado | Se canceló el borrador o toda la cantidad activa | Consultar motivo e inventario |

Una línea puede figurar **En cola**, **Preparando**, **Listo**, **Entregado**, **Cancelado** o **Dividida en preparación parcial**. La línea dividida conserva el registro original; sus líneas hijas contienen la parte preparada y la pendiente.

### Reserva consumo y entrega

La disponibilidad se calcula como **existencia física menos reservas activas**. Para productos por lote también se comprueban los saldos de cada lote y sus vencimientos. FEFO significa utilizar primero el lote elegible que vence antes.

Ejemplo ilustrativo con una bebida sin otros movimientos:

| Momento | Existencia física | Reservado | Disponible |
|---|---|---|---|
| Inicio | 10 | 0 | 10 |
| Se envía un pedido de 3 | 10 | 3 | 7 |
| Se inicia el despacho de las 3 | 7 | 0 | 7 |
| Se marcan listas y entregadas | 7 | 0 | 7 |

Si las tres unidades se cancelan antes de empezar, se libera la reserva y quedan diez disponibles. Si ya se consumieron, cancelar normalmente registra merma; el reintegro físico de un artículo intacto se realiza mediante una acción específica y autorizada.

<!-- pagebreak -->

## 3 Manual del administrador para preparar el menú

### Requisitos antes de configurar

Confirmar que la nueva versión esté activa, que el usuario tenga permiso de configuración y que esté seleccionada la sucursal correcta. Debe existir una bodega activa de esa sucursal, productos con unidad base activa y stock real disponible. Los artículos preparados necesitan una receta activa.

Seleccionar variantes concretas, por ejemplo Pizza grande, en lugar del producto padre que agrupa tamaños. Los productos deben estar activos y habilitados y visibles en la sucursal. Los artículos con vencimiento necesitan lotes iniciales identificados y vigentes.

### Crear canales y estaciones

1. Entrar en **Restaurante → Configuración**.
2. En **Canales de pedido**, pulsar **Agregar**, escribir el nombre y mantener **Activo**. Marcar **Plataforma externa** cuando corresponda.
3. En **Estaciones de cocina y despacho**, agregar Cocina, Barra o Despacho según la operación real.
4. Pulsar **Guardar configuración**. Si otro administrador guardó antes, recargar y revisar su versión.

Conviene guardar primero las estaciones para que estén disponibles al configurar las filas del menú. Los nombres usados en pedidos anteriores permanecen en sus datos históricos.

### Crear secciones y agregar productos al menú de la sucursal

1. Abrir **Restaurante → Configuración → Menú de esta sucursal**. Crear secciones visibles como **Pizzas**, **Acompañamientos** y **Bebidas**. Son grupos de la carta de esta sucursal; no cambian las categorías de inventario.
2. Crear una estación activa, por ejemplo Cocina o Barra, en **Atención y cocina**.
3. Pulsar **Agregar producto**. Buscar por nombre o código y, si hace falta, filtrar por categoría del catálogo. El selector muestra resultados por tandas y solo artículos vendibles habilitados en la sucursal. Insumos, ingredientes, subrecetas y empaques no aparecen. Un preparado sin receta activa tampoco aparece.
4. Pulsar **Agregar** junto al producto deseado. En **Productos seleccionados**, abrir su tarjeta y escoger la sección de carta, estación y orden. Marcar **Favorito** cuando convenga.
5. Guardar la configuración. Los artículos anteriores que aún no tenían sección aparecen bajo **Sin sección** hasta asignarlos.

**Ejemplo:** La gaseosa puede pertenecer a la categoría de inventario «Productos terminados» y mostrarse en la sección **Bebidas** de Jalapa. Zacapa puede organizarla en otra sección sin duplicar el producto del catálogo.

### Configurar opciones y extras

1. En el producto del menú, abrir **Grupos de opciones** y agregar un grupo, por ejemplo Extras.
2. Indicar mínimo y máximo. Mínimo 0 permite no elegir; mínimo 1 exige al menos una opción. El máximo limita cuántas opciones distintas se pueden marcar.
3. En **Opciones y extras**, agregar Extra queso, su precio adicional y su estado activo.
4. En **Consumo adicional en unidad base**, elegir el queso y su cantidad. Registrar 40 si la unidad base es gramo; registrar 0.040 si es kilogramo y admite tres decimales.
5. Guardar. El precio del extra se multiplica por la cantidad de producto pedida y su consumo se suma a la receta.

### Configurar exclusiones

Al abrir un producto con receta activa, la configuración informa si se elabora al pedir o si ya fue producido. Para una receta elaborada al pedir, **Ingredientes que el cliente puede quitar** muestra únicamente sus componentes directos. Marcar cebolla permite elegir **Sin cebolla** al tomar el pedido. La exclusión reduce su consumo y conserva el precio. Un producto ya producido no admite retirar retrospectivamente sus ingredientes.

<!-- pagebreak -->

## 4 Manual del administrador para la operación

### Crear combos

1. En **Configuración → Combos**, pulsar **Agregar** y escribir el nombre.
2. Indicar el precio total base y las fechas de inicio y fin. Mantener **Activo** mientras deba ofrecerse.
3. Crear grupos en **Selecciones del combo**, por ejemplo Pizza y Bebida. Definir la cantidad de elecciones y los productos elegibles.
4. Elegir productos que ya formen parte del menú. Guardar y comprobar su aparición dentro de la vigencia.

Al capturar el combo se selecciona cada componente. Sus extras se suman al precio base y sus exclusiones se validan como en una venta individual. El sistema reparte el importe entre componentes conservando el total exacto. Dos combos iguales en un pedido mantienen agrupaciones independientes.

### Crear promociones de producto

1. En **Promociones por producto**, agregar nombre, producto y porcentaje.
2. Definir prioridad, vigencia y estado activo. Guardar.
3. Si varias promociones coinciden, se elige la de mayor prioridad; ante empate, la de mayor porcentaje. Un empate completo tiene un desempate interno estable.

Las promociones no se acumulan entre sí ni se aplican nuevamente sobre un precio de combo. Un pedido enviado conserva la regla aplicada. Si una promoción cambia antes de enviar un borrador, hay que actualizar sus precios y revisar el total.

### Configurar mesas y delivery

- En **Áreas y mesas**, agregar área, nombre de mesa y estado activo. El nombre debe ser único dentro del área. No retirar una mesa ocupada.
- En **Zonas y tarifas de delivery**, agregar nombre, tarifa y estado activo. La tarifa quedará guardada en cada pedido.
- En **Motoristas**, agregar nombre y estado activo. Estos registros identifican al responsable de la entrega; no crean por sí solos un usuario de acceso al sistema.

### Responsabilidades y permisos habituales

| Perfil | Capacidades previstas en la configuración inicial |
|---|---|
| Administrador | Configuración, pedidos, cocina, mesas, delivery y cancelaciones |
| Mesero | Capturar y enviar pedidos, gestionar mesas y confirmar entrega |
| Cocina | Consultar comandas y cambiar estados de preparación |
| Punto de Venta o Cajero | Consultar pedidos; el cobro enlazado queda para fase 7 |
| Gerente | Consulta operativa según los permisos existentes del perfil |
| Responsable de delivery | Requiere permisos explícitos de consulta y gestión de delivery; para confirmar entrega necesita también el de entregar pedidos |

La autorización efectiva depende de los permisos del usuario. Cancelar requiere una capacidad distinta de editar. Reintegrar artículos intactos requiere, además, permiso para ajustar inventario.

<!-- pagebreak -->

## 5 Manual del mesero y del operador de pedidos

### Crear y enviar un pedido

1. Confirmar la sucursal de trabajo y entrar en **Restaurante → Nuevo pedido**.
2. Seleccionar **Bodega**, **Canal** y **Tipo de servicio**. Para Salón, seleccionar una **Mesa** activa.
3. Si proviene de una plataforma, capturar su **Referencia de plataforma**. Cuando se informa, debe ser única dentro del canal y la sucursal.
4. Completar cliente y contacto. Para Delivery son obligatorios teléfono, dirección y zona; la zona muestra su tarifa.
5. Buscar por nombre, código o categoría y pulsar **Agregar**. Ajustar cantidad, extras y exclusiones. Para un combo, completar cada elección.
6. Escribir las **Observaciones** que deba conocer el equipo. Usar las opciones configuradas para cambios de ingredientes; una nota libre no modifica el consumo.
7. Pulsar **Guardar borrador**. Revisar número, cantidades, opciones, tarifa y total calculado por el servidor.
8. Pulsar **Enviar a cocina y reservar**. Confirmar que el estado cambie a Enviado y que aparezcan las líneas por estación.

**Resultado esperado:** existe un pedido numerado, sus líneas están en cola y el inventario necesario quedó reservado. Guardar el borrador por sí solo no envía el pedido a cocina.

### Editar un borrador

Abrir el pedido desde **Pedidos** y pulsar **Editar productos**. Aunque ese sea el rótulo del enlace, el formulario permite revisar también los datos de servicio y ubicación antes del envío. Cambiar lo necesario y volver a **Guardar borrador**.

Si aparece un aviso de cambio de precio, receta o tarifa, utilizar **Actualizar precios del borrador** o editar y guardar otra vez. Revisar el nuevo importe antes de enviar. Un pedido de salón puede haber abierto un turno desde el borrador; revisar su mesa si se abandona o cancela la captura.

### Agregar productos después del envío

1. Abrir el pedido mientras esté Enviado, En preparación o Listo.
2. Desplegar **Agregar productos como nueva revisión**, agregar los artículos y completar sus opciones.
3. Pulsar **Enviar líneas adicionales**. Revisar las líneas nuevas y la revisión registrada en el historial.

Las líneas anteriores se conservan. Agregar productos a un pedido listo puede devolverlo a En preparación o Enviado según las líneas activas. Un pedido en ruta, entregado o cancelado ya no acepta adiciones.

### Consultar y entregar

En **Pedidos** buscar por número, cliente o referencia y utilizar filtros de estado, canal y servicio. Abrir el número para ver el detalle y el historial. En Salón o Para llevar, pulsar **Confirmar entrega** cuando todo esté listo y se entregue físicamente. Esta acción no registra pago.

<!-- pagebreak -->

## 6 Manual de cocina y despacho

### Leer la pantalla de cocina

Entrar en **Restaurante → Cocina** y elegir estación y canal si se necesita acotar la cola. Cada tarjeta muestra pedido, estación, producto, cantidad activa, opciones, observaciones, revisión, estado y tiempos de cola y preparación.

La pantalla se actualiza aproximadamente cada 15 segundos mientras esté visible y no haya una acción o mensaje pendiente. También se puede pulsar **Actualizar comandas**. Las adiciones y cancelaciones recientes aparecen como cambios, con un enlace al pedido.

### Preparar una línea completa

1. Localizar una línea **En cola** y revisar sus opciones y observaciones.
2. Pulsar **Preparar cantidad** y confirmar toda la cantidad activa propuesta.
3. Esperar la confirmación del servidor. La línea pasa a **Preparando** y se registra el consumo físico del inventario.
4. Al terminar, pulsar **Listo**. La acción registra la hora de finalización.
5. La persona responsable confirma la entrega desde el detalle del pedido cuando corresponda.

Para una bebida o artículo de reventa, iniciar la preparación representa iniciar su despacho. El sistema consume el artículo terminado. Para un plato elaborado al pedido, consume los componentes de la receta y los extras seleccionados.

### Preparar solo una parte

Si la línea contiene tres pizzas y se inicia una, pulsar **Preparar cantidad** e indicar 1. El sistema conserva la línea original como dividida y crea dos líneas relacionadas: una unidad en preparación y dos unidades en cola.

La parte que empieza consume su reserva. La parte pendiente sigue reservada y puede prepararse después. Los importes repartidos suman el total activo previo a la división. Si una división produce cantidades de componentes incompatibles con su unidad, el sistema la rechaza y solicita una cantidad mayor o la línea completa.

### Interpretar tiempos y revisiones

- **Cola:** tiempo entre el envío y el inicio de preparación; si no ha empezado, sigue acumulándose.
- **Preparación:** tiempo entre el inicio y el marcado como listo; si sigue preparándose, continúa acumulándose.
- **Revisión:** identifica el envío o cambio al que pertenece la línea. El historial permite consultar los cambios posteriores.

Las marcas se registran en el servidor. Las pantallas muestran horas según la operación configurada en el código, con desplazamiento de Guatemala de seis horas detrás de UTC.

### Cuando se presenta un error

Si aparece un aviso, leerlo antes de repetir la acción. Un pedido puede haber cambiado desde otra estación. Recargar, comprobar el estado actual y actuar sobre la cantidad que siga pendiente. Una falla de red requiere verificar si la acción se registró; el reintento del mismo comando está protegido contra duplicación. Un mesero sin permiso de cocina no puede iniciar preparación desde el servidor.

<!-- pagebreak -->

## 7 Manual de mesas y delivery

### Abrir y consultar una mesa

1. Crear un pedido de servicio Salón y seleccionar la mesa.
2. Guardar el borrador. Si la mesa no tiene turno abierto, se crea uno; si lo tiene, el pedido se incorpora a ese turno.
3. Entrar en **Mesas** para consultar si está Disponible u Ocupada, la hora de apertura y sus pedidos.

Una mesa admite un solo turno abierto. El turno puede contener varios pedidos compatibles de la misma ocupación.

### Cambiar o liberar una mesa

Para trasladar, abrir un pedido del turno, elegir la mesa destino en **Mesa y turno**, pulsar **Cambiar mesa** y escribir el motivo. La mesa destino debe estar libre y activa en la misma sucursal. La acción mueve el turno completo, incluidos sus pedidos.

Para liberar, comprobar que todos los pedidos del turno estén entregados o cancelados. Abrir uno de ellos, pulsar **Liberar mesa** e indicar el motivo. Si hay trabajo pendiente, el sistema impide el cierre. La liberación es operativa; el cierre de una cuenta pagada se enlazará en la fase 7.

### Capturar un delivery

Crear el pedido con servicio **Delivery**, seleccionar canal y bodega, y completar teléfono, dirección y zona. Agregar productos, guardar y revisar la tarifa antes de enviar. La tarifa de la zona se guarda en el pedido; sus cambios posteriores no alteran un pedido ya enviado.

### Asignar motorista y registrar salida

1. Esperar a que todas las líneas activas estén listas.
2. Abrir el pedido desde **Delivery** o **Pedidos**.
3. En la sección Delivery, seleccionar un motorista activo y escribir **Ruta / instrucciones**.
4. Pulsar **Asignar**. El historial conserva quién asignó y cuándo.
5. Cuando salga el pedido, pulsar **Registrar salida**. El estado cambia a **En ruta**.

La ruta es una descripción escrita. La pantalla no calcula recorridos ni comprueba automáticamente la posición del motorista.

### Incidencia y confirmación de entrega

Si el cliente no responde o no se puede completar un intento, abrir el pedido en ruta, pulsar **Registrar incidencia** y describir el hecho. El pedido permanece en ruta; una incidencia no lo marca como entregado.

Cuando se concrete la entrega, un usuario con permiso de entregar pedidos pulsa **Confirmar entrega**. Se registran estado y hora. Si se requiere cancelar un pedido que ya está en ruta o resolver una devolución después de entregarlo, escalar al responsable: esas operaciones no están disponibles desde la cancelación ordinaria de cocina.

<!-- pagebreak -->

## 8 Manual de cancelaciones y control de desperdicio

Cancelar exige permiso específico y un motivo. Antes de confirmar, comprobar el número de pedido, el producto, la cantidad y si la preparación ya comenzó. El efecto sobre inventario depende de ese estado físico.

| Situación | Efecto registrado | Acción del operador |
|---|---|---|
| Borrador | Cantidad activa e importe del pedido cancelado quedan en cero; no hubo reserva | Cancelar pedido con motivo |
| Línea pendiente | Se libera su reserva; la existencia física permanece igual | Cancelar cantidad pendiente |
| Línea preparada | Se conserva el consumo y se registra merma con costo y motivo | Cancelar cantidad preparada |
| Artículo intacto elegible | Se registra una entrada ligada al consumo y lote originales | Cancelar y reintegrar intacto |
| En ruta o entregado | Se bloquea la cancelación ordinaria | Escalar al responsable |

### Cancelar una cantidad o todo el pedido

1. Abrir el detalle del pedido y revisar estado y cantidades.
2. Para una línea enviada, pulsar **Cancelar cantidad**, indicar la cantidad y escribir el motivo.
3. Para el pedido completo, pulsar **Cancelar pedido** y explicar la causa. En un pedido enviado se procesan sus cantidades activas según el estado de cada línea.
4. Revisar las cantidades canceladas y la merma en el detalle, y comprobar el evento en **Historial de cambios**.

Si solo se preparó una parte mediante la división de la línea, cancelar la línea pendiente libera reservas; cancelar la parte preparada registra merma. No se deben reintegrar ingredientes crudos que ya fueron utilizados para cocinar.

### Reintegrar un artículo intacto

La acción **Cancelar y reintegrar intacto** está prevista para una línea consumida de un producto terminado, sin receta ni componentes adicionales, que realmente vuelve al inventario. Por ejemplo, una bebida envasada que se retiró para despacho y regresa sin utilizarse.

1. Verificar físicamente el artículo y su lote cuando aplique.
2. Abrir la línea elegible y pulsar **Cancelar y reintegrar intacto**.
3. Indicar cantidad y motivo. Se requieren permisos para cancelar pedidos y ajustar inventario.
4. Comprobar la cantidad reintegrada y el evento registrado.

El sistema rechaza lotes vencidos y líneas procedentes de varios lotes cuando esta pantalla no permite identificar cuál regresó. En esos casos, solicitar conciliación al responsable de inventario. La elegibilidad técnica del botón no sustituye la comprobación física del artículo.

### Relación con caja

Una cancelación de cocina no ejecuta un reembolso. La integración financiera posterior deberá relacionar pedidos, ventas, pagos y devoluciones. Hasta entonces, volver a capturar estos productos como una venta directa repetiría el flujo anterior de inventario; no debe utilizarse como mecanismo de cobro de estos pedidos.

<!-- pagebreak -->

## 9 Ejemplos explicados de operación

Los valores siguientes son ilustrativos. No representan precios ni inventarios cargados en la base de datos del restaurante.

### Pedido de salón con extra y exclusión

Supuestos: pizza a Q60.00, bebida a Q10.00 y extra de queso a Q5.00. El queso extra consume 40 g y la cebolla está configurada como excluible. No hay promociones en este ejemplo.

1. El mesero selecciona canal Propio, servicio Salón y Mesa 4.
2. Agrega una pizza con Extra queso y Sin cebolla, además de una bebida.
3. Guarda el borrador y revisa **Q75.00**: Q60.00 + Q5.00 + Q10.00.
4. Envía. Se reservan la receta de pizza sin su componente de cebolla, 40 g adicionales de queso y una bebida.
5. Cocina inicia la pizza y consume los componentes. Despacho inicia la bebida y consume una unidad terminada.
6. Aunque la bebida esté lista primero, el pedido sigue en preparación hasta que termine la pizza.
7. Se confirma la entrega física y, cuando todos los pedidos del turno estén resueltos, se libera la mesa.

La exclusión conserva el precio. Escribir “sin cebolla” únicamente en Observaciones no cambia la receta ni la reserva; debe seleccionarse la exclusión configurada.

### Combo enviado por delivery

Supuestos: combo pizza y bebida a Q70.00, extra a Q5.00 y tarifa de zona a Q12.00.

1. El operador selecciona servicio Delivery, zona, dirección y teléfono.
2. Elige un combo, completa pizza y bebida, y agrega el extra permitido.
3. El borrador muestra **Q87.00**: Q70.00 + Q5.00 + Q12.00.
4. Al enviar se conserva el precio de combo y se reserva el consumo de sus selecciones. El importe de productos se reparte entre componentes; la tarifa se conserva en el pedido.
5. Cuando todo está listo, se asigna motorista y ruta, y luego se registra la salida.
6. Si el cliente no responde, se registra una incidencia. Cuando se entrega efectivamente, se confirma la entrega.

### Preparación parcial y cancelación

Supuesto: una línea de tres pizzas iguales por Q180.00, sin descuentos. Se inicia una unidad y quedan dos en cola. La línea histórica se divide en Q60.00 preparados y Q120.00 pendientes.

Si se cancelan las dos pendientes, se liberan sus reservas y permanece activa una pizza por Q60.00. Si también se cancela la pizza preparada, se registra su merma y el pedido queda cancelado. Los ingredientes de esa pizza ya habían salido del stock al iniciar su preparación; registrar la merma conserva ese consumo y su trazabilidad.

<!-- pagebreak -->

## 10 Mensajes frecuentes y solución de problemas

| Mensaje o situación | Explicación | Qué hacer |
|---|---|---|
| No tienes permiso para esta operación | Falta una capacidad requerida | Solicitar al administrador que revise los permisos concretos de la acción |
| Sucursal fuera de tu alcance | El usuario no tiene acceso a la sucursal | Seleccionar una sucursal autorizada; no reutilizar enlaces de otra sucursal |
| El pedido cambió | Otra operación actualizó su revisión | Recargar, revisar el historial y actuar sobre el estado actual |
| El menú precio o receta cambió | El borrador ya no coincide con la configuración vigente | Actualizar el borrador, revisar el total y enviarlo otra vez |
| La zona o tarifa cambió | Cambió el costo o la definición del delivery antes del envío | Guardar y revisar el borrador actualizado |
| Existencia insuficiente después de reservas | Otros pedidos comprometieron parte del stock | Revisar bodega, existencias y pedidos pendientes con el responsable |
| No hay saldo suficiente en lotes disponibles | Hay saldo agotado, vencido o sin conciliar | Revisar lotes reales y vencimientos; corregir mediante inventario autorizado |
| La referencia ya corresponde a un pedido | La referencia externa ya fue utilizada | Abrir el número indicado y comprobar si se trata del mismo pedido |
| La mesa destino está ocupada | Otro turno utiliza esa mesa | Elegir una mesa libre o resolver primero su ocupación |
| El turno conserva pedidos sin entregar | Hay pedidos pendientes del mismo turno | Entregar o cancelar los pedidos que correspondan antes de liberar |
| La división no respeta la unidad | Una preparación parcial produciría cantidades inválidas de componentes | Preparar una cantidad compatible o la línea completa |
| La sesión venció o no hay conexión | La operación no recibió una confirmación utilizable | Recuperar sesión o conexión, consultar el pedido y reintentar si sigue pendiente |

### Buenas prácticas de uso

Revisar las opciones antes de enviar y el historial después de cambios importantes. Informar los motivos de cancelación con suficiente detalle para que otro responsable entienda lo ocurrido. Al cambiar de sucursal, comprobar también la bodega y los precios del menú.

Si un artículo no aparece, verificar primero que esté en el menú, que su estación esté activa y que el catálogo lo permita en la sucursal. Si un combo no aparece, revisar su vigencia y estado. La disponibilidad base del menú puede cambiar al elegir extras o exclusiones; la comprobación definitiva ocurre al enviar.

Cuando una operación falle, conservar el número de pedido y el mensaje mostrado para soporte. Una recarga o un segundo intento no reemplaza la revisión del estado actual.

<!-- pagebreak -->

## 11 Reporte técnico y activación

### Organización del código

| Capa o componente | Archivos principales | Responsabilidad |
|---|---|---|
| Dominio | RestaurantSetup.cs y RestaurantOrder.cs | Configuración, estados, líneas, turnos, reservas e históricos |
| Aplicación | RestaurantService.cs, RestaurantPricing.cs y RestaurantPreparation.cs | Permisos, precios, reserva, preparación, cancelaciones y delivery |
| Contratos | RestaurantContracts.cs | Entradas de operaciones y consulta para futura integración de ventas |
| Persistencia | RestaurantRepository.cs, RestaurantConfiguration.cs y AppDbContext.cs | Guardado transaccional, concurrencia y restricciones de PostgreSQL |
| Inventario | InventoryStock, InventoryMovement, InventoryLotAllocation e InventoryLotLedger | Reservas y movimientos por producto, bodega y lote |
| Interfaz | RestaurantController.cs, Views/Restaurant y archivos restaurant.js y restaurant-config.js | Pantallas, captura, acciones y configuración |
| Seguridad | PermissionCodes, SecurityBootstrapper, AccountController y Layout | Capacidades y accesos de usuarios |

Las carpetas nuevas se encuentran en Domain/Restaurant, Application/Restaurant e Infrastructure/Restaurant dentro de src. La interfaz pertenece a SistemiPos.Web.

### Migración y puesta en marcha

La migración **20261019170538_AddRestaurantOrdersKitchenDelivery** crea ocho tablas para configuración, pedidos, líneas, eventos, comandos, turnos, reservas y merma. Agrega una secuencia de numeración y campos de reserva y referencia al movimiento original en inventario.

El arranque existente de la aplicación ejecuta las migraciones y actualiza los permisos, excepto en el entorno Testing. El responsable técnico debe iniciar una compilación que incluya estos cambios usando la conexión prevista. Después, renovar la sesión del usuario si conserva permisos anteriores y configurar el restaurante por sucursal.

En la entrega original se utilizó la configuración de compilación Phase6Verification porque la aplicación abierta bloqueaba archivos de Debug. No se reinició esa instancia ni se aplicó la migración a la base activa durante el trabajo.

### Evidencia técnica registrada

La compilación de la aplicación Web y sus dependencias terminó con **0 errores y 0 advertencias**. Los archivos JavaScript pasaron la revisión de sintaxis. Entity Framework confirmó que el modelo y la última migración coinciden; la revisión de formato de cambios no detectó errores.

Estas comprobaciones son evidencia de compilación, sintaxis y consistencia del esquema. No se ejecutaron pruebas automatizadas, recorridos de interfaz, pruebas de concurrencia contra PostgreSQL ni una migración sobre históricos. No se ha medido rendimiento con un historial grande; las listas actuales cargan los pedidos de la sucursal.

<!-- pagebreak -->

## 12 Aceptación y continuidad del proyecto

### Recorrido de aceptación pendiente

Registrar para cada caso el número de pedido, usuario, sucursal, resultado y cualquier mensaje. Los siguientes resultados son criterios esperados que deben comprobarse en la aplicación activada.

| Caso | Resultado a comprobar |
|---|---|
| Pedido de pizza y bebida | Estaciones correctas; estados independientes y entrega completa |
| Extra y exclusión | Precio y consumo acordes con las selecciones configuradas |
| Combo con opciones | Total exacto, extras sumados y componentes identificables |
| Cambio de promoción o tarifa | Borrador requiere revisión; pedido enviado conserva históricos |
| Preparar una unidad de tres | Cantidades, importes y reservas se distribuyen correctamente |
| Cancelar pendiente y preparada | La primera libera reserva; la segunda registra merma sin otra salida |
| Reintegrar artículo intacto | Entrada vinculada al consumo y lote originales con permiso requerido |
| Delivery con incidencia | Permanece en ruta hasta la confirmación real de entrega |
| Dos operadores sobre stock o mesa | No se aceptan reservas excesivas ni turnos activos duplicados |
| Reintento tras una falla | Se registra un solo efecto de cada operación |
| Usuario sin permiso u otra sucursal | Se bloquea el acceso o la acción no autorizada |
| Ventas o traslados con stock reservado | Se respetan las cantidades comprometidas por pedidos |

### Pendientes de la fase 7

La fase 6 entrega una consulta técnica con pedido, revisión, cantidades activas, importes y movimientos de inventario consumidos. La fase 7 debe utilizarla para crear la venta, procesar pagos y enlazar devoluciones sin volver a consumir las mismas cantidades. También deberá relacionar el cierre financiero de mesa con su cuenta.

Antes de operar el ciclo completo de venta, se debe comprobar ese enlace. Los estados Listo, Entregado y mesa Disponible no equivalen a una cuenta pagada.

### Recomendaciones de seguimiento

Priorizar la aceptación de cancelaciones, preparación parcial y concurrencia, porque afectan inventario. Tras validar el recorrido operativo, revisar el comportamiento de listados con el volumen esperado de pedidos. Documentar cualquier ajuste de permisos o reglas que surja de la operación real antes de ampliar el alcance.

### Fuentes del reporte

Se revisaron el plan aprobado FASE_6_PEDIDOS_COCINA_DELIVERY_IMPLEMENTACION.md, la guía FASE_6_IMPLEMENTACION_Y_USO.md, el servicio de restaurante, el modelo de dominio, las vistas y sus acciones, la configuración de permisos y la migración generada. Los ejemplos monetarios de este manual son didácticos. El reporte no incluye capturas de pantallas en ejecución ni declara verificaciones funcionales que no se realizaron.
