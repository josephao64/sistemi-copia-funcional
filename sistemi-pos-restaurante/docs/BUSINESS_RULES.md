# BUSINESS_RULES.md

## Fase 7: pedidos, preparación y despacho

Las reglas aprobadas expresamente se conservan en [Fase 7: reglas aprobadas](FASE_7_REGLAS_PROPUESTAS.md). La implementación es incremental; el estado verificable se registra en `FASE_7_POS_IMPLEMENTACION.md`.

En el bloque inicial, confirmar un pedido fija precios, impuestos y fórmula expandida de receta de cada línea. El borrador no consume inventario. Iniciar preparación consume los componentes históricos proporcionalmente al rendimiento; marcar preparado no vuelve a consumir. Despachar reventa consume unidades; despachar un preparado no repite ingredientes o empaques ya incluidos en su receta. La cancelación exige motivo, conserva el histórico y no repone consumos de preparación. Se registran por separado las cantidades canceladas después de iniciar preparación. Cada evento es idempotente por pedido e identificador y debe conservar tipo, línea, cantidad y motivo para admitir su reintento.

Las cantidades de existencias, movimientos y lotes admiten seis decimales para conservar los consumos de recetas. PostgreSQL utiliza `numeric(22,6)`, preservando los dieciséis dígitos enteros disponibles anteriormente con `numeric(18,2)`. Las cantidades comerciales de pedidos y ventas conservan dos decimales; no cambia la precisión monetaria existente.

## Compatibilidad de caja y bodega al vender

Si existe una sucursal operativa seleccionada, la caja de la venta debe pertenecer a ella, también en reintentos idempotentes. Una caja con sucursal requiere una bodega activa de esa misma sucursal; no puede recurrir a la bodega de otro contexto cuando falta su configuración. La venta conserva la bodega utilizada. La vinculación explícita de terminal de venta permanece pendiente en POS-01.

# Reglas de negocio

## Restaurante — Fase 6

- Cada sucursal organiza su carta en secciones propias como Pizzas, Acompañamientos o Bebidas; una sección de menú no modifica la categoría del catálogo ni los productos de otra sucursal. Los menús existentes sin sección conservan sus productos y se muestran como Sin sección hasta asignarlos.
- El selector del menú admite artículos activos y vendibles para la sucursal. Excluye insumos, ingredientes, subrecetas y empaques; un preparado requiere receta activa. Una exclusión solo puede elegirse entre los componentes directos de una receta elaborada al pedir. Las recetas de productos producidos previamente no permiten excluir componentes al tomar el pedido.
- Canal, servicio, sucursal y bodega son datos diferentes del pedido. La referencia externa se reserva por canal/sucursal y las operaciones usan identificador idempotente.
- El pedido usa estados operativos independientes de su pago. Cada línea conserva configuración, precio, receta, opciones y estación históricos. Los cambios enviados generan revisiones auditadas.
- Al enviar se reserva stock y lote; `ReservedQuantity` protege también las ventas y salidas de otros módulos. Al iniciar preparación se libera la reserva y se registra una única salida `OrderConsumption` atómica con el estado de línea.
- Cancelación pendiente libera reservas. Cancelación preparada conserva el consumo y registra `OrderWaste` con cantidad, costo, motivo y referencias de movimiento/lote. No repone ingredientes ni duplica la salida.
- La preparación parcial conserva la línea original como `Split` y crea dos líneas hijas con importes y reservas repartidos: una empieza y otra permanece pendiente. Se conserva el total exacto en centavos y la precisión de unidad de los componentes.
- Un SKU intacto sin receta ni componentes adicionales puede reintegrarse con permiso de ajuste, motivo y movimiento `OrderReturn` ligado al consumo y lote originales. Se rechaza si está vencido o si la línea proviene de varios lotes sin identificar físicamente cuál regresó. La comida preparada se registra como merma.
- Una mesa tiene un turno activo; moverla requiere destino libre y conserva un evento. La liberación operativa exige pedidos entregados o cancelados; el enlace con liquidación financiera corresponde a Fase 7.
- Modificadores cumplen mínimos/máximos; exclusiones solo afectan componentes permitidos. Promociones se eligen por prioridad y porcentaje sin acumulación automática. El combo conserva su precio repartido exactamente entre componentes.
- Los combos admiten opciones y exclusiones de cada selección; los extras incrementan el precio del combo. Cada instancia tiene una secuencia comercial dentro del pedido y conserva vigencia histórica. No se aplican promociones de producto adicionalmente a un precio de combo.
- Un delivery conserva tarifa, zona, dirección, motorista, ruta y tiempos. Una incidencia no equivale a entrega. Solo un pedido listo y asignado puede salir a reparto.
- El contrato de checkout conserva el consumo previo: Fase 7 debe utilizarlo al facturar estos pedidos y nunca repetir la salida física.

## Ventas

### Facturación FEL

- Los datos del emisor se reutilizan desde la configuración de empresa y no se duplican por venta.
- Las credenciales de INFILE se almacenan protegidas y nunca se presentan nuevamente en texto plano.
- Número de autorización, serie, número DTE, fecha de certificación, certificador, QR y número de acceso solo se persisten desde una respuesta de certificación de INFILE.
- El sistema no genera localmente identificadores fiscales de FEL.
- Cada venta puede tener como máximo una certificación FEL.

### BR-001
Una venta confirmada descuenta inventario.

### BR-002
Una venta confirmada no se modifica directamente.

### BR-003
Las correcciones posteriores se realizan mediante anulación o devolución.

### BR-004
El total se calcula a partir del detalle.

### BR-005
El precio utilizado en cada línea se conserva históricamente.

### BR-006
Cada venta utiliza una sola forma de pago en el MVP.

### BR-007
No confirmar una misma venta más de una vez.

### BR-CASH-POS-001
Al abrir una caja se asignan uno o más dispositivos POS activos. Los pagos con tarjeta de esa caja solo pueden utilizar dispositivos asignados y cada uno requiere un único cierre con código y monto antes de cerrar la caja.

### BR-CASH-COUNT-001
Cada sesión de caja admite un único arqueo de cierre por denominaciones.

### BR-CASH-AUTH-001
La apertura y el cierre de la caja usan la identidad de la sesión autenticada y no solicitan nuevamente la contraseña del usuario propietario.

### BR-008
No confirmar una venta con existencia insuficiente.

### BR-PRICE-001
Un producto con multiprecios deshabilitado utiliza su precio de venta base.

### BR-PRICE-002
En modo `Cantidad mínima`, se utiliza la regla activa con la cantidad mínima más alta que sea menor o igual a la cantidad de la línea. Su precio unitario aplica a todas las unidades de esa línea.

### BR-PRICE-003
En modo `Cantidad exacta`, una regla activa se utiliza únicamente cuando su cantidad coincide exactamente con la cantidad de la línea; el precio configurado representa el total bruto de esa línea.

### BR-PRICE-004
Las cantidades de reglas no se repiten dentro de un producto, deben ser mayores que cero y sus precios no pueden ser negativos. Las reglas inactivas no participan en el cálculo.

Un producto sin variantes representa un SKU vendible. En un producto con variantes, el registro base agrupa productos hijos vendibles; cada hijo tiene una sola combinación de valores, inventario, precio, costo, código y código de barras propios. Todos reutilizan los mismos flujos de compras, ventas, inventario y Kardex.

Cada producto puede conservar nombre y descripción de factura/proveedor separados de su nombre y descripción del sistema. Los datos del sistema se utilizan en ventas y operación general; ambos juegos de referencias son consultables y visibles en compras, pedidos y recepción cuando existen.

El código del producto se conserva exactamente cuando es manual. Cuando se genera, usa el prefijo único de la categoría y seis dígitos. Los códigos de variantes usan el código base y sufijos `-01`, `-02`, etc.; no se reutilizan ni pueden editarse después de guardar.

Categoría y marca son opcionales en captura y se asignan a `Sin categoría` y `Sin marca` cuando se omiten. Código, categoría, marca y tipo de producto son inmutables después de la creación. Las medidas que distinguen productos o variantes se expresan mediante atributos y valores; la unidad técnica heredada no se captura en información general.

### BR-PROD-001
No se crea un producto cuando ya existe otro con el mismo nombre base, marca, categoría y conjunto de atributos. La validación no depende del orden de los atributos y debe informar el código interno, nombre visible, marca, código de barras, precio y existencia del producto encontrado.

### BR-PRICE-005
Deshabilitar multiprecios no elimina sus reglas. Todas las modalidades utilizan el mismo inventario y descuentan la cantidad física total vendida.

### BR-PRICE-006
El cálculo se realiza en el servicio central de precios y el detalle de venta conserva el precio unitario equivalente y el importe bruto realmente aplicados.

### BR-CLI-001
La búsqueda por NIT debe priorizar siempre `Customers` antes de consultar un proveedor externo.

### BR-CLI-002
La ausencia o falla de un proveedor de consulta NIT no debe impedir la venta; debe habilitar la captura manual del nombre o razón social.

### BR-CLI-003
Una venta con un NIT nuevo debe crear el cliente y asociarlo a la venta en la misma transacción. Si el NIT ya pertenece a un cliente activo, debe reutilizar ese cliente.

Las ventas a consumidor final usan `C/F` únicamente como indicador del documento, nunca como identificador único de cliente. Nombre y ciudad pueden conservarse solo en la venta sin crear un cliente; si se proporciona teléfono o procedencia, se registra o reutiliza un cliente por teléfono. Ninguno de estos datos es obligatorio para una venta C/F.

### BR-CLI-004
El NIT normalizado se utiliza para localizar clientes independientemente de espacios, guiones o diferencias entre mayúsculas y minúsculas.

### BR-CLI-005
La venta a crédito usa los días de crédito del cliente para calcular el vencimiento y conserva en la cuenta los días de crédito y de gracia vigentes al crearla. Con saldo pendiente, la cuenta está Pendiente antes del vencimiento, Vencida dentro de la gracia y Morosa después de agotarla; con saldo cero está Pagada.

El historial de ventas debe ofrecer un único buscador que consulte número, NIT normalizado, nombre del cliente, fecha y nombre del producto. Debe mostrar el NIT asociado a cada venta; las ventas sin NIT se presentan como consumidor final (`CF`).

La creación de ventas requiere simultáneamente el rol `Punto de Venta` y el permiso de administración de ventas. Los perfiles de supervisión pueden consultar las ventas cuando tengan autorización, pero no iniciar una nueva venta.

## Pagos

### BR-009 — Efectivo
`monto_recibido >= total_venta`

`cambio = monto_recibido - total_venta`

## Inventario

### BR-012
La existencia no se modifica directamente.

### BR-013
Todo cambio de existencia genera un movimiento.

### BR-014
Compra confirmada: entrada de inventario.

### BR-015
Venta confirmada: salida de inventario.

### BR-016
Devolución física aceptada: entrada de inventario.

### BR-017
Anulación: reversión de movimientos cuando corresponda.

### BR-018
No permitir inventario negativo.

### BR-019
La existencia actual debe permanecer consistente con los movimientos confirmados.

### BR-INV-001
Una toma física se gestiona mediante una sesión en proceso o finalizada. Crear la sesión define sus productos y un producto no puede pertenecer simultáneamente a dos sesiones en proceso.

### BR-INV-002
Cada producto captura su existencia de referencia y la versión del stock al comenzar su propio conteo. Si la versión cambia antes de guardar, el producto queda en estado `Reconteo requerido`; el movimiento operativo no se bloquea.

### BR-INV-003
Guardar una fila conserva históricamente existencia de referencia, existencia contada, diferencia, comentario opcional, usuario y fecha. No modifica la existencia operativa ni se altera por movimientos posteriores.

### BR-INV-004
Una toma solo puede finalizar cuando todos sus productos están contados y no existen reconteos pendientes. La finalización es transaccional y, para cada producto, aplica `existencia operativa actual + diferencia guardada`.

### BR-INV-005
La finalización genera movimiento de Kardex únicamente cuando la diferencia es distinta de cero y conserva relación con la toma y su detalle. Una toma finalizada no puede editarse ni volver a aplicar ajustes.

## Recetas

### BR-REC-001
Existe una definición de receta por producto Preparado o Subreceta concreto, incluyendo variantes. Las recetas usan el catálogo compartido existente; no definen fórmulas distintas por sucursal. El rendimiento representa la cantidad obtenida por elaboración en la unidad base del producto.

### BR-REC-002
Los componentes se seleccionan de los productos inventariables activos y concretos del catálogo con unidad base activa. Las preparaciones usadas como componentes requieren una receta activa configurada para producir existencias. Cada producto aparece una sola vez y no puede ser componente de sí mismo. No se admiten ciclos entre subrecetas.

### BR-REC-003
Las cantidades y el rendimiento son positivos y respetan la precisión de cada unidad, hasta seis decimales. La conversión reutiliza reglas generales y específicas del producto. Se conservan cantidad ingresada, unidad y cantidad base convertida; guardar una receta no genera movimientos de inventario.

### BR-REC-004
Una subreceta utilizada por una receta activa no puede desactivarse. La gestión conserva los registros mediante activación/desactivación, sin eliminación física. La unidad base y el tipo de productos relacionados se protegen desde Catálogo, y sus referencias impiden eliminar esos productos.

### BR-REC-005
Cada guardado de receta y componentes es transaccional y auditado. Las ediciones incluyen la versión leída por el usuario; una edición obsoleta se rechaza. La validación del grafo de subrecetas se ejecuta bajo aislamiento serializable para impedir ciclos por operaciones concurrentes.

## Compras

### BR-020
Una compra confirmada aumenta inventario.

### BR-021
El costo utilizado en cada línea se conserva históricamente.

### BR-022
Una compra confirmada no se elimina físicamente.

Una compra recibida puede anularse con el permiso `purchases.cancel`, motivo obligatorio (hasta 500 caracteres), usuario y fecha/hora. La operación conserva compra, detalle, documento y recepción originales. No permite reconfirmar ni editar una compra confirmada o anulada, ni eliminar físicamente sus recepciones o detalles.

La anulación retira las cantidades de la bodega y de los lotes originales de la compra, incluso si ya vencieron, dejando movimientos y asignaciones inversas vinculados a los originales. Se rechaza si falta trazabilidad, si los lotes no conservan toda la cantidad o si la cuenta por pagar tiene pagos. Para compras a crédito se conserva la cuenta anulada con saldo cero; no se revierten pagos ni movimientos de caja desde este flujo. Compra, inventario, cuenta y pedido se actualizan en una sola transacción con control de concurrencia.

Por decisión del usuario, anular una recepción reabre las cantidades pendientes de su orden, descontando únicamente lo recibido en esa recepción del acumulado vigente. Los adicionales no modifican cantidades solicitadas. La orden vuelve a `Solicitada` si no quedan cantidades recibidas o a `Recibida parcialmente` si quedan recepciones vigentes; conserva su solicitud original y todas las recepciones, incluidas las anuladas. Una orden con historial de recepciones no puede editarse ni cancelarse, aunque vuelva a `Solicitada`. Los totales operativos excluyen las recepciones anuladas. El documento del proveedor sigue reservado para evitar duplicar la misma factura histórica.

### BR-COM-001
Una orden de compra inicia en `Borrador`, puede pasar a `Solicitada`, continuar como `Recibida parcialmente` y termina en `Recibida` o `Cancelada`. Borradores y solicitadas sin recepciones son editables; una orden con historial de recepción no puede modificar su solicitud original ni cancelarse. Las recibidas son de consulta y solo reabren pendientes mediante la anulación trazable de una recepción; las canceladas son inmutables.

### BR-COM-002
Una orden solicitada admite recepciones parciales sucesivas mientras conserve cantidades pendientes. Las cantidades reales pueden ser menores o mayores a las pendientes y pueden incluir productos adicionales, sin duplicar productos dentro de una misma recepción. Una línea completada no admite nuevas cantidades.

### BR-COM-003
Son órdenes pendientes del Dashboard las que se encuentran `Solicitada` o `Recibida parcialmente` y conservan cantidades pendientes.

### BR-COM-004
La fecha de solicitud de una orden se establece con el reloj del sistema en backend. Un mismo producto no puede repetirse dentro de la orden.

### BR-COM-008
Cada línea de una orden de compra conserva la cantidad de presentaciones solicitadas, la presentación seleccionada, el factor aplicado y su unidad base. Esta información debe mantenerse al guardar el borrador, solicitar la orden, consultar su detalle o PDF y realizar cada recepción.

### BR-COM-007
El número de factura del proveedor es único por proveedor en las compras confirmadas. La misma factura puede existir para proveedores distintos, pero no puede repetirse para un mismo proveedor, sin importar si la compra se registró de forma directa o mediante recepción de una orden.

### BR-COM-005
Confirmar una recepción genera en una sola transacción la actualización acumulada del pedido, una compra confirmada independiente, el aumento de inventario, los movimientos de Kardex y la cuenta por pagar si la condición es crédito. El pedido queda `Recibido parcialmente` mientras conserve pendientes y `Recibido` al completarse.

### BR-COM-009
Toda orden y compra directa requiere sucursal y bodega de destino. La bodega debe estar activa, pertenecer a la sucursal indicada y la sucursal debe formar parte del alcance autorizado del usuario.

### BR-COM-006
El historial incluye todas las compras confirmadas, tanto directas como generadas por la recepción de una orden, y excluye borradores. Una compra a crédito conserva factura, vencimiento, saldo y pagos en cuentas por pagar. El número interno de compra y la factura o documento del proveedor son datos independientes.

## Caja

### BR-023
Toda venta debe asociarse a una sesión de caja activa.

### BR-024
La sesión registra apertura y cierre.

### BR-025
Los totales se separan en efectivo, transferencia y POS.

### BR-026
El efectivo esperado no incluye transferencias ni POS.

### BR-027
El cierre registra efectivo esperado, efectivo contado y diferencia.

El cajero confirma el cierre reingresando únicamente su propia contraseña. Una diferencia no bloquea el cierre ni requiere un autorizador; permanece registrada para la revisión administrativa posterior.

La revisión puede marcar una diferencia como cuadrada, conservando sin alteración el efectivo esperado, el efectivo contado y la diferencia original, junto con el usuario, la fecha y una observación opcional de conciliación.

### BR-028
Una caja cerrada no modifica sus importes ni movimientos históricos. Únicamente puede agregarse la trazabilidad de revisión de su diferencia.

Una caja que cierre sin diferencia se considera cuadrada automáticamente y debe conservar la fecha y el usuario autenticado que realizó el cierre como trazabilidad de conciliación.

### BR-CAJ-001
Un usuario no puede tener más de una sesión de caja activa simultáneamente en el mismo punto de caja.

### BR-CAJ-002
Un punto de caja no puede tener más de una sesión activa simultáneamente.

### BR-CAJ-003
Una sesión de caja cerrada no debe reabrirse directamente.

### BR-CAJ-004
Los errores detectados después del cierre deben corregirse mediante un movimiento o ajuste auditado.

### BR-CAJ-005
Toda corrección posterior al cierre debe registrar:

- Usuario.
- Fecha y hora.
- Motivo.
- Monto.
- Tipo de ajuste.
- Sesión de caja relacionada.

### BR-CAJ-006
Un ajuste de caja no debe modificar ni eliminar el cierre original.

### BR-CAJ-007
El historial de apertura, movimientos, cierre y ajustes debe conservarse.

## Descuentos

### BR-DES-001
La capacidad de aplicar descuentos debe poder habilitarse o deshabilitarse desde administración.

### BR-DES-002
Un usuario sin permiso de descuento no puede aplicar descuentos.

### BR-DES-003
Cuando el usuario tenga permiso de descuento, el porcentaje máximo permitido debe ser configurable.

### BR-DES-004
El límite puede definirse por rol y, cuando exista configuración específica por usuario, esta debe prevalecer sobre la configuración general del rol.

### BR-DES-005
Todo descuento ingresado por un vendedor debe quedar pendiente hasta que otro usuario con permiso para autorizar descuentos lo apruebe al momento del cobro. La aprobación no modifica el límite configurado del vendedor.

### BR-DES-006
Todo descuento debe conservar:

- Usuario que lo aplicó.
- Porcentaje o valor aplicado.
- Motivo cuando sea requerido.
- Usuario que autorizó, cuando corresponda.
- Fecha y hora.

### BR-DES-007
La autorización de un descuento superior no debe modificar permanentemente el límite configurado del usuario que originó la venta.

### BR-DES-008
En el Punto de Venta, el vendedor puede ingresar descuentos por producto como porcentaje o monto fijo. Al cobrar, el sistema crea una solicitud con el detalle del carrito y mantiene abierto y bloqueado el modal de cobro hasta que un usuario distinto, con el permiso configurable de autorizar descuentos, la apruebe o rechace. Cuando se aprueba, la venta continúa automáticamente desde ese modal. Si cambian productos, cantidades o descuentos, la aprobación deja de ser válida y debe solicitarse otra. El descuento no puede ser negativo ni superar el importe bruto de la línea, y su auditoría conserva producto, tipo, valor, monto descontado, usuario que vende, usuario que autoriza y fecha y hora.

## Anulaciones

### BR-031
La anulación conserva la venta original.

### BR-032
Registrar motivo, usuario, fecha y hora.

### BR-033
Revertir inventario cuando corresponda.

## Devoluciones

### BR-034
La devolución puede ser total o parcial.

### BR-035
Debe conservar relación con la venta original.

### BR-036
Registrar motivo, usuario, fecha y hora.

### BR-037
Un producto devuelto genera entrada únicamente cuando vuelve físicamente al inventario.

## Persistencia

### BR-038
No eliminar físicamente ventas, compras confirmadas, movimientos de inventario, cajas cerradas ni bitácora.

### BR-039
Representar el ciclo de vida mediante estados explícitos.

## Seguridad

### BR-040
Validar permisos en backend.

### BR-041
Recalcular y validar en backend valores críticos.

### BR-SEG-LOCK-001
Tras cinco intentos fallidos de autenticación, la cuenta queda bloqueada temporalmente durante quince minutos. Los fallos de credenciales responden con un mensaje genérico que no distingue usuario de contraseña.

### BR-SEG-AUDIT-001
Los eventos de autenticación (inicio exitoso, fallo, bloqueo y cierre de sesión) se registran en `AuditLogs` bajo el módulo `Seguridad`, sin almacenar contraseñas ni tokens. El último acceso exitoso se conserva en el usuario y es consultable en perfil y administración.
El registro de auditoría presenta el detalle de cada evento con mensajes claros en español, sin exponer JSON, nombres técnicos de propiedades ni caracteres codificados. Los intentos con usuarios inexistentes se distinguen de los fallos de contraseña dentro de la vista protegida de auditoría, mientras la respuesta pública del inicio de sesión continúa siendo genérica.

### BR-SEG-SESSION-001
La sesión autenticada expira según la duración configurada de la cookie (ocho horas por defecto) con renovación deslizante. Al expirar, el usuario debe volver a autenticarse y recibir un indicador claro de sesión expirada.

### BR-SEG-LOGIN-001
Solo los usuarios activos, con exactamente un rol, permisos activos y al menos una sucursal autorizada pueden iniciar sesion. Al crear la sesion se carga una fotografia de roles, permisos y sucursales autorizadas, y las validaciones criticas de permisos se mantienen en backend contra la base de datos.

### BR-SEG-PASS-001
Las contrasenas se administran mediante ASP.NET Identity y no se almacenan, registran ni devuelven en texto plano. Los eventos de auditoria de autenticacion no incluyen contrasenas ni tokens.

## Dinero

### BR-042
Usar precisión decimal para valores monetarios.


## Moneda

### BR-CUR-001
La moneda operativa es configurable y no debe quedar fijada en código.

### BR-CUR-002
La configuración debe permitir código, nombre, símbolo, decimales, separadores y posición del símbolo.

### BR-CUR-003
`GTQ` es únicamente el valor inicial por defecto del modelo base.

### BR-CUR-004
Cada transacción debe conservar el código de moneda aplicado para preservar el histórico.

### BR-CUR-005
Cambiar la moneda o su formato no debe recalcular ni reinterpretar transacciones históricas.

### BR-CUR-006
El MVP utiliza una sola moneda operativa activa; no se realizan conversiones ni operaciones multi-moneda.

### BR-CUR-007
La moneda operativa se selecciona de `Currencies`. Cada entrada conserva código, nombre, símbolo, decimales, separadores y posición; crear una moneda no activa operaciones multimoneda.

## Caja chica

### BR-PC-001
Cada Caja chica tiene nombre y un único usuario responsable. La asignación autenticada, no una autorización por gasto, determina quién puede verla y operarla.

### BR-PC-002
Una Caja chica debe estar abierta para aceptar movimientos. Cada apertura crea un `PettyCashCycle` distinto y todo movimiento queda vinculado a ese ciclo.

### BR-PC-003
La liquidación cierra el ciclo, conserva saldo esperado, contado y diferencia. Una reapertura crea un ciclo nuevo y la vista operativa no mezcla movimientos de ciclos anteriores.

### BR-PC-004
Las categorías de Caja chica son reutilizables. Los movimientos conservan la relación con la categoría y su nombre histórico.

## Transacciones

### BR-043
Ejecutar de forma atómica confirmación de venta, anulación, devolución, confirmación de compra, recepción de pedido, ajuste de inventario y cierre de caja.

### BR-044
Ante fallo parcial, revertir la operación completa.

## Configuración

### BR-045
Formas de pago iniciales: efectivo, transferencia y POS.

### BR-046
La incorporación futura de nuevas formas de pago no debe requerir reescribir la lógica central de ventas.

### BR-047
Los días sin movimiento se definen exclusivamente por producto, entre 1 y 3650 días. No se heredan de Configuración General. Los registros antiguos sin valor propio no generan esa alerta y deben definirlo al editarse.

### BR-048
El stock bajo comprende existencias mayores a cero e iguales o inferiores al mínimo; los productos con existencia cero pertenecen exclusivamente a agotados.

### BR-049
La existencia contada en una toma física debe ser un número entero igual o mayor que cero. La diferencia se calcula como `existencia contada - existencia de referencia`; el comentario es opcional y no existe catálogo ni campo de motivo.


## Operación offline

### BR-OFF-001
La operación offline se limita al Punto de Venta y utiliza el último stock sincronizado menos las ventas pendientes del mismo dispositivo.

### BR-OFF-002
Una venta offline debe tener un `ClientOperationId` único. Su reenvío es idempotente y no puede duplicar venta, pago, inventario ni caja.

### BR-OFF-003
Las ventas pendientes se conservan localmente hasta que el servidor confirme su sincronización. Los errores y conflictos no eliminan la operación.

### BR-OFF-004
El servidor valida nuevamente sesión, existencia, precios, forma de pago y demás reglas al sincronizar.

### BR-OFF-005
La operación offline admite efectivo, transferencia y POS. Crédito y descuentos requieren conexión con el servidor.

### BR-OFF-006
El dispositivo bloquea nuevas ventas después de 7 días sin sincronización o al alcanzar 500 operaciones pendientes.

### BR-OFF-007
El comprobante offline es provisional, no consume numeración definitiva y debe indicar `Pendiente de sincronización`.

## Impuestos

### BR-TAX-001
Los precios de venta ya incluyen impuesto.

### BR-TAX-002
El impuesto se configura como porcentaje, no como monto fijo.

### BR-TAX-003
Cada producto debe indicar si aplica impuesto o si es exento.

### BR-TAX-004
Cuando un producto aplica impuesto, el sistema debe desglosar internamente:

- Base imponible.
- Porcentaje de impuesto.
- Monto de impuesto.

### BR-TAX-005
El impuesto incluido en el precio no debe sumarse nuevamente al total de la venta.

### BR-TAX-006
Un producto exento debe manejar porcentaje de impuesto igual a cero o condición equivalente de exención.

### BR-TAX-007
El porcentaje de impuesto utilizado en una venta debe conservarse históricamente para no alterar ventas anteriores cuando cambie la configuración futura.



## Costeo

### BR-COST-001
Cada producto debe mantener como referencia operativa:

- `LastCost`: último costo de compra.
- `PreviousCost`: costo inmediatamente anterior al último.

### BR-COST-002
El costo actual del producto será `LastCost`.

### BR-COST-003
Al confirmar una nueva compra:

- `PreviousCost` toma el valor anterior de `LastCost`.
- `LastCost` toma el costo unitario de la nueva compra.

### BR-COST-004
El histórico completo de costos no depende únicamente de `LastCost` y `PreviousCost`.

Los costos históricos deben conservarse en los detalles de compras confirmadas.

### BR-COST-005
Una compra histórica no debe recalcularse ni modificarse por cambios posteriores de costo.



## Numeración de documentos

### BR-NUM-001
Cada tipo de documento debe utilizar una serie/prefijo propio y un correlativo independiente.

### BR-NUM-002
La numeración inicial será:

- Venta: `V-000001`.
- Compra: `C-000001`.
- Devolución: `D-000001`.
- Anulación: `A-000001`.

### BR-NUM-003
Cada tipo de documento mantiene su propia secuencia.

La numeración de ventas no debe consumir correlativos de compras, devoluciones o anulaciones.

### BR-NUM-004
Un número asignado a un documento confirmado no debe reutilizarse.

### BR-NUM-005
La generación del correlativo debe realizarse de forma segura ante concurrencia para evitar números duplicados.



## Código de barras

### BR-BAR-001
El código de barras no es obligatorio para todos los productos.

### BR-BAR-002
Cuando un producto tenga código de barras, este debe ser único dentro del sistema.

### BR-BAR-003
No permitir registrar dos productos activos con el mismo código de barras.

### BR-BAR-004
Los productos sin código de barras deben poder localizarse mediante código interno, nombre u otros criterios permitidos.



## Transferencias

### BR-TRF-001
Toda venta pagada mediante transferencia debe registrar:

- Banco origen.
- Banco y cuenta destino del negocio.
- Referencia.
- Monto.
- Fecha de transferencia.
- Estado.
- Usuario que registró la operación.

### BR-TRF-002
Las cuentas destino del negocio deben administrarse desde configuración y seleccionarse desde el Punto de Venta.

### BR-TRF-003
Estados permitidos para una transferencia:

- `Pending`.
- `Received`.
- `Verified`.
- `Rejected`.

### BR-TRF-004
Significado de estados:

- `Pending`: transferencia registrada pero todavía no confirmada.
- `Received`: el cliente presentó la transferencia o comprobante.
- `Verified`: un usuario autorizado confirmó que el pago ingresó correctamente.
- `Rejected`: la transferencia no pudo validarse o presenta una inconsistencia.

### BR-TRF-005
Cuando una transferencia sea verificada o rechazada, registrar:

- Usuario que realizó la verificación.
- Fecha y hora de verificación.
- Observación cuando corresponda.

### BR-TRF-006
La verificación debe requerir permiso explícito.

### BR-TRF-007
Una transferencia registrada en el servidor puede quedar inicialmente en estado `Received` y pendiente de verificación posterior.

### BR-TRF-008
La verificación de una transferencia no debe eliminar ni reemplazar la información original registrada en la venta.

### BR-TRF-009
El estado `Received` permite confirmar la venta. `Verified` representa una validación posterior para conciliación y auditoría.



## Pago con POS / tarjeta

### BR-POS-001
Toda venta pagada mediante POS/tarjeta debe registrar como mínimo:

- Tipo de tarjeta.
- Últimos 4 dígitos.
- Número de autorización.
- Número de voucher o referencia.
- Monto.
- Estado.
- Usuario que registró el pago.

### BR-POS-002
Tipos de tarjeta permitidos inicialmente:

- Débito.
- Crédito.

### BR-POS-003
Estados permitidos:

- `Approved`.
- `Rejected`.
- `Pending`.

### BR-POS-004
Una venta con pago POS/tarjeta solo puede considerarse pagada cuando el estado sea `Approved`.

### BR-POS-005
No almacenar el número completo de tarjeta.

### BR-POS-006
No almacenar CVV, PIN ni datos sensibles de autenticación de tarjeta.

### BR-POS-007
El número de autorización, voucher o referencia debe conservarse históricamente junto con la venta.

### BR-POS-008
Cada dispositivo POS bancario debe identificar nombre, banco o proveedor, Terminal ID, cuenta bancaria asociada y ubicación o caja. Puede incluir una observación y estado activo o inactivo. No debe almacenar PIN, contraseñas ni datos sensibles de tarjetas.

### BR-POS-009
Toda venta pagada mediante tarjeta debe quedar asociada al dispositivo POS seleccionado para permitir totales por dispositivo y su cierre de caja.

### BR-POS-008
Los cambios de estado deben quedar auditados cuando correspondan.



## Reembolsos

### BR-REF-001
Todo reembolso requiere autorización de un usuario con permiso de alto nivel para reembolsos.

### BR-REF-002
El usuario que procesa el reembolso no necesita ser el mismo usuario que lo autoriza.

### BR-REF-003
El medio utilizado para devolver el dinero no está obligado a coincidir con el medio de pago original.

### BR-REF-004
Todo reembolso debe registrar:

- Venta original.
- Monto reembolsado.
- Medio de devolución.
- Usuario que solicitó o procesó el reembolso.
- Usuario que autorizó.
- Fecha y hora.
- Motivo.
- Referencia o datos del medio de devolución cuando corresponda.

### BR-REF-005
El sistema debe conservar tanto el medio de pago original como el medio utilizado para el reembolso.

### BR-REF-006
Los reembolsos deben reflejarse en la sesión y cierre de caja correspondiente.

### BR-REF-007
El cierre debe mostrar los reembolsos separados por medio de devolución.

### BR-REF-008
Cuando el reembolso sea en efectivo, debe disminuir el efectivo esperado de caja.

### BR-REF-009
Cuando el reembolso sea por transferencia o POS/tarjeta, debe registrarse para conciliación y auditoría sin afectar el efectivo esperado como si hubiera sido una salida de efectivo.

### BR-REF-010
Un reembolso no debe eliminar ni modificar destructivamente la venta original.

### BR-REF-011
Todo reembolso y su autorización deben quedar en auditoría.



## Comprobante de venta

### BR-CMP-001
El comprobante debe poder generarse en al menos:

- Formato ticket térmico.
- Formato PDF.

### BR-CMP-002
El comprobante debe incluir como mínimo:

- Nombre del negocio.
- NIT cuando esté configurado.
- Dirección y teléfono cuando estén configurados.
- Número de venta.
- Fecha y hora.
- Cajero.
- Cliente o consumidor final.
- Detalle de productos.
- Cantidad.
- Precio unitario.
- Subtotal.
- Descuento.
- Total.
- Forma de pago.
- Referencia cuando aplique.
- Cambio cuando el pago sea en efectivo.
- Leyenda configurable al pie.

### BR-CMP-003
La visualización del impuesto en el comprobante debe ser configurable desde administración.

### BR-CMP-004
Aunque el impuesto no se muestre en el comprobante, el sistema debe conservar internamente:

- Base imponible.
- Porcentaje de impuesto.
- Monto de impuesto.

### BR-CMP-005
Ocultar el impuesto en el comprobante no debe alterar el cálculo ni el total de la venta.

### BR-INV-EXP-001
Los productos que manejan caducidad requieren una fecha por cada entrada de inventario. La existencia disponible se conserva por lote y se descuenta primero del lote con fecha de caducidad más próxima.

### BR-INV-EXP-002
El reporte de productos por caducar muestra únicamente lotes con existencia disponible y dentro de los días de alerta configurados para cada producto; los lotes vencidos también se conservan visibles.

Cada fila identifica el lote y su bodega, además del producto, saldo disponible, vencimiento y días restantes. El período se configura en el producto mediante `Alertar antes de caducar`; el día límite está incluido y se usa la fecha operativa de Guatemala. La consulta usa la bodega del contexto operativo cuando está seleccionada.

## Nombres de la estructura empresarial

Al crear o editar grupos empresariales, empresas, sucursales y bodegas, sus nombres se normalizan con inicial mayúscula por palabra, según la cultura `es-GT`, y espacios uniformes. Esto incluye el nombre comercial y la razón social; los códigos mantienen su conversión a mayúsculas. Los registros existentes se normalizan cuando se guardan, sin modificar históricos en bloque.

## Validaciones de sucursales

Toda sucursal requiere una empresa, un código, un nombre y una zona horaria válida. El código se normaliza a mayúsculas y debe ser único dentro de la empresa. Los límites son 32 caracteres para el código, teléfono y código de establecimiento, 128 para el nombre, 256 para la dirección y 64 para la zona horaria. Estas reglas se validan en el formulario y se vuelven a validar en el dominio antes de guardar.

## Rol único por usuario

Cada usuario debe tener exactamente un rol. Las altas y ediciones de usuarios requieren seleccionar un rol existente; el servidor rechaza selecciones vacías o múltiples. Cambiar el rol reemplaza la asignación anterior en la misma transacción que los datos del usuario. Se mantiene la protección del último Super Administrador activo.

Si un usuario existente tiene varios roles, al editarlo debe elegirse explícitamente el único rol que conservará; el sistema no decide cuál conservar automáticamente.

## Maestro de productos para restaurante

- Las nuevas altas usan Insumo, Preparado, Reventa, Subreceta o Empaque, según el checklist aprobado de Productos. Ingrediente, Producto de venta y Bebida se conservan en registros existentes hasta una reclasificación manual; sus valores almacenados no se reinterpretan. La clasificación define el artículo para los procesos del sistema; no implica implementar recetas ni producción anticipadamente.
- La unidad base debe existir y estar activa para seleccionarla. No puede cambiarse si hay operaciones o registros relacionados. Una unidad inactiva ya asignada puede conservarse.
- El stock mínimo se expresa en la unidad base y puede configurarse por sucursal, junto con habilitación, visibilidad y precio. Un precio o mínimo local vacío hereda el valor general; cero es un valor explícito válido. Inventario y alertas usan el mínimo de la sucursal de la bodega, o el general cuando no existe configuración local. Solo se permite modificar sucursales autorizadas.
- El POS y su catálogo offline respetan la visibilidad local. El backend rechaza ventas de productos no habilitados para la sucursal de la caja y calcula el precio base local sin modificar el precio general. Las reglas de multiprecios existentes mantienen su prioridad; cuando ninguna aplica, se usa el precio base de la sucursal. Las solicitudes de descuento utilizan el mismo cálculo.
- El último costo y el costo anterior se obtienen de compras y recepciones. El maestro no permite introducirlos ni sobrescribirlos, tampoco mediante un envío manipulado.
- Un ingrediente no captura precio de venta en el formulario. La reclasificación conserva importes anteriores para no alterar datos preexistentes.
- La eliminación física solo se permite sin uso ni relaciones operativas. Se retira el saldo inicial vacío y la configuración dependiente del maestro, conservando su bitácora. Si hay historial, existencias, variantes, presentaciones o conversiones asociadas, se exige desactivar.
- Desactivar no borra historia ni existencias; la edición permite reactivar el mismo registro. Las variantes siguen el estado inactivo del padre.
- No se cambia el manejo de caducidad con registros relacionados sin una conciliación de lotes.
- Las ventas consumen primero los lotes vigentes con vencimiento más próximo y continúan en otros lotes si es necesario. Los vencidos no se consumen automáticamente.

La revisión de cobertura y las limitaciones previas a recetas se registran en `REVISION_PRODUCTOS_Y_LOTES.md`.

## Presentaciones, lotes y apertura de existencias

- Compras y recepción permiten seleccionar las presentaciones configuradas para el producto. El servidor verifica pertenencia, estado y factor positivo. La cantidad ingresada es cantidad de paquetes por factor a unidad base. La factura conserva presentación, factor y unidad aplicados; editar el catálogo no recalcula documentos anteriores.
- Los costos unitarios de referencia conservan seis decimales para representar costos por gramo. Los importes totales conservan precisión monetaria de dos decimales.
- Cada producto permite configurar control por lote. Los productos existentes conservan este control habilitado y sus lotes e históricos. Con historial, registros operativos o existencias no se cambia el indicador desde el maestro. La caducidad requiere control por lote. Las variantes heredan esta configuración.
- Toda entrada nueva de un producto con control por lote genera un lote con identificador, fecha de ingreso, cantidad inicial y saldo. La caducidad es obligatoria si el producto la maneja. Los productos con control por lote sin caducidad usan FIFO. Sin control por lote se actualizan existencias y Kardex sin generar ni consumir lotes; no se admite seleccionar un lote ni registrar apertura o baja por lote.
- Compra directa y cada recepción permiten capturar un identificador de lote de hasta 100 caracteres. Si se deja vacío, se genera automáticamente; el vencimiento se exige únicamente cuando el producto maneja caducidad. El servidor usa la configuración del producto y, en líneas solicitadas, resuelve el producto por la línea de la orden. Valida estos datos antes de generar la compra o modificar existencias. Las entradas conservan saldos separados por producto, bodega y lote, con su fecha real de ingreso.
- FEFO selecciona vencimiento más cercano y desempata por fecha de ingreso. FIFO selecciona fecha de ingreso. Se consume la cantidad necesaria entre varios lotes sin usar vencidos automáticamente.
- Cada movimiento nuevo de un producto con control por lote conserva sus asignaciones de lote y cantidades. Las bajas por lote requieren motivo y permiso de ajuste; pueden retirar vencidos explícitamente.
- Las devoluciones y anulaciones reintegran cantidades a lotes originales y conservan la relación con las asignaciones de salida. No pueden devolver más de lo vendido y aún no reintegrado. Si la venta antigua no tiene trazabilidad de lote, se requiere conciliación; no se inventa un lote original.
- Por decisión del usuario, las existencias anteriores sin historia de lotes se identifican como saldo inicial con fecha de apertura actual. Los lotes antiguos conservan su vencimiento y saldo; su cantidad de apertura no se presenta como cantidad histórica recibida. Los saldos de productos con caducidad sin lote requieren registrar sus vencimientos reales antes de consumirlos. La apertura no aumenta la existencia total.
- Los ingredientes no se ofrecen ni se aceptan para venta directa. Su consumo corresponderá a recetas.

## Ampliación aprobada de fase 5

PRO-01 a PRO-08 se describen en [Recetas, producción y costos](RECETAS_IMPLEMENTACION_Y_USO.md). Cada guardado publica una versión inmutable. Las órdenes de producción fijan versión y bodegas, consumen componentes y registran el rendimiento real en una sola transacción. La diferencia de rendimiento requiere motivo y conserva la merma. Las ventas distinguen elaboración al vender y consumo de existencias producidas, guardando el detalle histórico para no descontar dos veces los ingredientes de subrecetas. Costos históricos y disponibilidad por bodega se consultan en el detalle; los componentes siempre son productos inventariables existentes. Cantidades del inventario y lotes conservan seis decimales.
