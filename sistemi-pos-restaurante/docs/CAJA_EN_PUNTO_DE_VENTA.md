# Estado y apertura de caja en el punto de venta

Fecha: 24 de octubre de 2026.

## Presentación del cajero y arqueo - 26/10/2026

La segunda revisión extiende la identidad verde oscuro del comprobante al mostrador: navegación, cabecera «Cajero / Mostrador», acceso «Mi caja y arqueos», estado del turno, categorías, tarjetas del catálogo, pedido y diálogos de pago. La selección del catálogo y la revisión/cobro se distinguen visualmente; el total y el botón de cobro tienen mayor jerarquía. Las fotos mantienen su presentación sin recorte y no se añaden atajos de teclado, datos de ventas ni estados de caja inventados. La pérdida de conexión, conteo ciego, permisos y validación del turno conservan el comportamiento existente. Revisión manual adicional: catálogo vacío/agotado, modo lista/tarjetas, nombres largos, pedido vacío y con productos, medios de pago y pantallas pequeñas.

La apertura de caja muestra los pasos del turno, la sucursal y el responsable, las denominaciones de efectivo y un resumen lateral del fondo inicial. Billetes y monedas comparten el componente `_DenominationCount`: incluye botones táctiles para sumar/restar unidades, subtotales por denominación y tipo, resaltado de las cantidades utilizadas y número de piezas contadas. El resumen es fijo en escritorio y pasa al flujo normal en pantallas pequeñas.

El detalle de caja presenta el estado del arqueo y de los cierres bancarios. Un usuario con `cash.count` puede desplegar un reconteo y guardarlo mediante `RecordCount`, conservando los conteos anteriores. El cierre muestra el último importe declarado y agrupa la autorización de diferencias. El esperado sigue protegido por el conteo ciego; los nuevos indicadores se calculan únicamente con el efectivo introducido por el usuario y los estados ya entregados por el servidor.

El POS y sus diálogos de apertura, gestión y cierre comparten la presentación en `wwwroot/css/cashier.css`. No se agregan reglas de cobro, permisos, dependencias ni migraciones. Los campos enviados y las validaciones financieras del backend se mantienen. Revisión manual pendiente: apertura, cantidades con teclado y botones, reconteo, cierres bancarios, cierre con diferencia, conservación de errores de validación y distribución a 360 px, tableta y escritorio.

## Implementación

Al entrar a `/pos`, el cajero ve primero «Apertura de caja». Selecciona el puesto y confirma la apertura; solo después de que el servidor confirma un turno activo que puede operar se presenta el catálogo y el pedido. Si ya existe un turno propio compatible, se abre directamente el punto de venta tras validar su estado. Si no hay terminal, falta permiso o el turno corresponde a otra persona, se conserva la pantalla de apertura con el mensaje pertinente. El pedido no cobrado permanece en la página si el turno deja de estar disponible; el cobro sigue sujeto a la validación del servidor.

Para pruebas locales de desarrollo, `PosCash:SkipAdditionalCredentialsForDevelopment` permite omitir las credenciales adicionales de apertura, cierre con diferencia y movimientos de caja. Solo se aplica cuando el entorno es Development y la opción está activada; la sesión del usuario y los permisos de cada operación siguen vigentes. El motivo de una diferencia sigue siendo obligatorio y la auditoría indica que se omitió la autorización adicional. En otros entornos se conservan las autorizaciones originales.

La pantalla `/pos` puede abrirse sin turno activo. Con una caja abierta, la cabecera compacta muestra sucursal, terminal, cajero, estado, fondo y tiempo del turno. «Detalles del turno» despliega la fecha de apertura, la hora y la última sincronización. El fondo y los totales se entregan únicamente cuando el usuario tiene los permisos correspondientes. En pantallas estrechas, los datos se distribuyen en varias columnas sin cubrir la venta.

El catálogo y el pedido aprovechan el ancho disponible en portátiles; las categorías se desplazan horizontalmente y el catálogo tiene su propio desplazamiento. En pantallas estrechas las secciones se apilan. La pantalla de apertura sigue apareciendo antes del catálogo cuando no hay un turno válido.

No seleccionar terminal, no tener una caja abierta, perder conexión o no poder validar el turno bloquea el cobro. El pedido que está en pantalla se conserva. Iniciar sesión, refrescar, cerrar sesión o cruzar medianoche no genera ni cierra un turno.

La apertura, el resumen, las entradas/salidas y el cierre están integrados en diálogos de la pantalla. El detalle existente de caja mantiene el arqueo por denominaciones y los cierres físicos de POS bancarios. Si se asignaron dispositivos al abrir, el cierre exige completar sus cierres físicos, como ya hacía el sistema.

El diálogo de cierre del POS incluye un acceso directo a «Cierre POS / tarjetas» del turno. Allí se registra el total y la referencia obligatoria de cada dispositivo asignado. Si un dispositivo no se utilizó y su cierre es cero, el operador puede declarar cero e identificar la referencia como «SIN USO». No se crea un cierre automáticamente ni se altera la obligación de registrar los dispositivos asignados.

### Menú del cajero y presentación de ventas

- Los perfiles exclusivos «Cajero» y «Punto de Venta» comparten la navegación de ventas. Las acciones siguen sujetas a sus permisos; el cambio de presentación no concede acceso nuevo.
- El acceso principal es **Caja / Apertura**. Se retiraron «Entrada o salida» y el acceso aislado «Cerrar caja» del menú de ventas. Los movimientos autorizados quedan dentro de la gestión de caja, bajo «Movimientos de efectivo».
- «Ver caja» y «Caja / Apertura» abren el mismo menú con o sin turno registrado. Desde otras pantallas, el enlace lleva a `/pos#caja` para conservar el flujo de terminal del punto de venta.
- Sin terminal se explica cómo seleccionarla. Con caja cerrada se permite abrir únicamente con autorización. Con apertura activa se muestran sus datos y las acciones disponibles, sin ofrecer una segunda apertura. Si se muestra un turno cerrado, se identifica como última apertura y se informa que el nuevo turno tendrá su propio fondo.
- El catálogo presenta fotografías reales del producto sin recortarlas, categorías, existencias y botón «Agregar». El pedido conserva cantidades, descuentos, totales y medios de pago. «Limpiar» pide confirmación y quita únicamente el pedido pendiente de cobro, sin modificar ventas registradas ni existencias.
- El resumen visible del turno exige estado actualizado y permiso para ver totales. «Ventas cobradas» suma los medios de pago netos de devoluciones; excluye fondo inicial, movimientos manuales y cobros de cuentas por cobrar. «Efectivo esperado» procede del resumen calculado por el servidor. El conteo ciego sigue ocultando esos importes.

Revisión manual de esta actualización: abrir `/pos` con cada perfil; seleccionar terminal; comprobar estados sin apertura, abierta y cerrada; abrir y cancelar el formulario; confirmar que una apertura activa no ofrece duplicarla; consultar arqueo, cerrar y abrir un turno nuevo; comprobar fotografías, categorías, limpiar pedido y formas de pago; simular pérdida de conexión. Esta actualización de presentación no agrega una migración nueva.

## Configuración de terminales y selector (26/10/2026)

Una terminal representa un puesto de venta y vincula sucursal, código de caja física y bodega de consumo. Administración la registra en Pedidos → Terminales → Nueva terminal (`/pedidos/terminales/nueva`), con los permisos `orders.view` y `pos.terminals.manage`.

El acceso Pedidos → Terminales (`/pedidos/terminales`) muestra los puestos activos de la sucursal, su caja física y bodega. Desde ese listado se abre «Nueva terminal». Al guardar se confirma el registro y se actualiza el listado; si se creó desde el aviso del POS, se actualiza el selector de ventas. La página de Pedidos muestra el número de terminales en su acceso. Las terminales no aparecen como filas de pedidos, porque son puestos de venta, no órdenes.

El botón «Editar» reutiliza el modal para cambiar código, caja física y bodega. GET/POST `/pedidos/terminales/{id}/editar` exige los permisos de consulta de pedidos y administración de terminales; el servicio también valida permiso y sucursal. Se mantiene el identificador de la terminal y los registros históricos. La bodega debe estar activa en la misma sucursal y el código de terminal no puede repetirse. Cambiar caja o bodega exige que no haya turnos activos en las cajas involucradas ni líneas de pedidos de la terminal pendientes de venta o cancelación. Renombrar el código no cambia esas asociaciones. No se agregan migraciones.

El selector muestra las terminales activas de la sucursal antes de abrir caja. Se corrigió el filtro que exigía una sesión abierta y bloqueaba la primera apertura. Si el cajero ya tiene una sesión, se conserva la compatibilidad con su caja y, cuando corresponde, con la terminal vinculada al turno. Si no hay opciones, el POS muestra un aviso de configuración y el acceso autorizado al formulario; no crea terminales, bodegas ni cajas automáticamente. Los códigos de terminal y caja se muestran juntos para facilitar la selección.

Comprobación manual: registrar una terminal con la sucursal y bodega correctas; recargar `/pos` sin turno abierto; seleccionar el puesto y abrir caja. Con un turno activo, comprobar que solo aparece la terminal compatible.

La terminal no tiene una asignación fija a un usuario: el cajero autorizado en la sucursal la selecciona y queda como responsable al abrir el turno. Si existe una caja anterior del usuario sin terminal compatible (por ejemplo, una caja de demostración `USR-...`), el POS informa el código de esa caja y ofrece «Consultar mi caja actual», sujeto al permiso de consulta. Debe resolverse y cerrarse ese turno antes de abrir otra caja. El aviso distingue esta situación de la ausencia de terminales configuradas; no modifica ni cierra sesiones automáticamente.

## Persistencia y concurrencia

- Se reutilizan `CashSessions`, `CashMovements`, los pagos de cada venta y la auditoría financiera.
- Las aperturas nuevas vinculan `TerminalId`, moneda y zona horaria. La fecha, el responsable y los instantes de cierre los determina el servidor en UTC.
- Se conservan los índices de caja física y cajero activo; se añade unicidad por sucursal/terminal para los turnos nuevos. Los datos históricos permanecen sin una terminal inventada.
- `CashCommandReceipts` conserva la clave acotada por sucursal, usuario y operación, una huella de datos funcionales y el turno resultado. Se guarda en la misma transacción que apertura, movimiento o cierre.
- Las credenciales no se almacenan en la huella ni en el recibo de idempotencia. La huella incluye los datos financieros de la operación; los campos de autorización se validan antes de registrar la operación.
- Los comandos POS se serializan mediante bloqueos transaccionales PostgreSQL. El token `xmin` de la sesión arbitra las ventas, los reembolsos, los movimientos y el cierre. Si otro proceso modifica la caja, el guardado pierde la concurrencia y su transacción no puede dejar pagos o stock aplicados parcialmente.
- El cierre recalcula desde los movimientos confirmados y verifica la revisión presentada. Una diferencia exige motivo y conserva las autorizaciones existentes. Un umbral configurado puede exigir autorización adicional incluso a un usuario que normalmente cierra sin autorización.
- Los movimientos automáticos nuevos identifican su componente de pago o reembolso, con índice único. Las correcciones manuales son movimientos inversos vinculados; no editan ni eliminan el original y no se pueden reversar dos veces.
- Los pagos con estado pendiente impiden cerrar. Los cobros nuevos requieren pagos recibidos/verificados o aprobados antes de registrar la venta. No se añadió un proveedor bancario ni una confirmación automática de pagos pendientes de sistemas anteriores.

Los importes se validan y persisten como `decimal`; las capturas nuevas de efectivo exigen cero o positivo, según la operación, y máximo dos decimales. Una venta de 70 con recibido 100 registra 70 de ingreso, no 100 ni 40. El resumen utiliza el libro de movimientos, sin sumar otra vez los pagos de la misma venta.

## Reloj y estado

La respuesta contiene `serverNow`. El navegador sincroniza en el punto medio de la petición y usa `performance.now()` para avanzar cada segundo. La apertura es un dato fijo y la duración no se reinicia a las 24 horas.

El estado se consulta cada 30 segundos mientras la pestaña esté visible, además de al volver, recuperar conexión y completar una operación. El reloj se resincroniza con esas respuestas; no hay una petición por segundo. Tras una pausa prolongada, se solicita una nueva referencia. Las respuestas anteriores a una consulta nueva o con revisión inferior se descartan.

Las claves pendientes se conservan por usuario, sucursal, terminal y operación; no son autoridad sobre el turno ni contienen contraseñas. Antes de reintentar se consulta el resultado registrado. Si se cambian los datos mientras el resultado es incierto, se bloquea esa nueva solicitud hasta resolver la anterior.

## Conexión y compatibilidad offline

Esta implementación aplica la política nueva de la especificación del 24/10/2026: los cobros nuevos del POS requieren conexión y validación del turno. Se desactiva el guardado automático de ventas nuevas offline en esta pantalla y se deja de almacenar su HTML autenticado en la caché del service worker.

La base IndexedDB y las ventas pendientes anteriores no se borran. Sus mecanismos de consulta y sincronización permanecen para no perder operaciones históricas. Un carrito abierto permanece en el documento durante una caída de conexión; no se añadió persistencia del carrito tras cerrar o recargar la página.

## Moneda, zona y políticas

La moneda se resuelve desde la empresa de la sucursal autorizada y conserva la configuración general como respaldo del sistema anterior. Apertura y venta capturan esa moneda. La caja no puede vender con un código de moneda distinto del vigente. La zona horaria procede de la sucursal y se conserva al abrir. Guatemala usa `America/Guatemala`.

Configuración opcional en `appsettings` o variables de entorno:

```json
"PosCash": {
  "Default": {
    "MaximumOpeningAmount": null,
    "DifferenceApprovalThreshold": null,
    "LongShiftHours": null
  },
  "Companies": {},
  "Branches": {}
}
```

En `Companies` y `Branches`, la clave es el GUID de la empresa o sucursal y el valor tiene los mismos tres campos. Prevalece la política de sucursal, después empresa, después Default. La política elegida es completa; los campos nulos no heredan parcialmente la política inferior.

`null` en apertura significa que no se impone un máximo comercial adicional al límite técnico del tipo monetario. `null` en umbral conserva las autorizaciones existentes. `null` en horas desactiva la alerta de turno prolongado. No se eligieron límites comerciales arbitrarios. Se mantiene la política actual de un turno por cajero y operación exclusiva del responsable; no se habilitó un cambio silencioso de responsable.

## Permisos reutilizados

| Operación | Control |
|---|---|
| Estado POS | `sales.manage`, sucursal y terminal autorizadas |
| Apertura | `cash.open`; contraseña propia si corresponde |
| Cobro | `sales.manage`, turno activo del responsable y terminal compatible |
| Entrada / salida | `cash.movements.income` / `cash.movements.expense`; autorización cuando corresponda |
| Totales | `cash.view`, propiedad o consulta autorizada de otros usuarios y protección de conteo ciego |
| Cierre | `cash.close.own`, revisión vigente, responsable y autorizaciones de diferencia |
| Comprobante | `cash.closing-report`, alcance y acceso a totales |

El resumen oculta esperado y diferencias durante el conteo ciego. Después del cierre conserva el contado, esperado, diferencia y responsables. El comprobante de cierre es imprimible y requiere una caja cerrada vinculada a terminal.

## Rutas

- `GET /pos/caja/estado?terminalId=...`
- `POST /pos/caja/abrir`
- `POST /pos/caja/movimiento`
- `POST /pos/caja/cerrar`
- `GET /pos/caja/resultado?terminalId=...&kind=abrir|movimiento|cerrar&key=...`
- `GET /pos/caja/{sessionId}/comprobante`

Las mutaciones usan antiforgery e `Idempotency-Key`. Los errores incluyen un código estable y un mensaje legible; se distinguen permisos (403), conflicto (409) y datos inválidos (422). El servidor ignora actor, reloj y totales no autoritativos enviados por el cliente.

## Instalación en prueba

1. Respaldar la base y revisar los permisos y las terminales/bodegas activas de cada sucursal.
2. Configurar la conexión del entorno de prueba y los límites comerciales que se hayan aprobado. Usar HTTPS fuera de localhost.
3. Compilar:

```powershell
dotnet build SistemiPosRestaurante.slnx --configuration Release --no-restore
```

4. Aplicar la migración PostgreSQL `20261024090000_PosCashContextAndCommands` al entorno de prueba:

```powershell
dotnet ef database update 20261024090000_PosCashContextAndCommands --project src/SistemiPos.Infrastructure --startup-project src/SistemiPos.Web --configuration Release --no-build
```

La aplicación también ejecuta las migraciones al arrancar fuera del entorno `Testing`, como ya estaba configurado. Por tanto, revisar la conexión antes de iniciar. Esta entrega no inicia ni migra la aplicación contra la base operativa.

Script SQL idempotente de revisión: `artifacts/pos-cash/PosCashContextAndCommands.sql`. Fue generado desde `20261021054815_PhaseEightControl` hasta esta migración y no se ejecutó contra la base operativa. Para una base más antigua, usar el comando EF anterior para aplicar también sus migraciones previas.

5. Reiniciar el servicio y recargar el navegador para actualizar JavaScript y el service worker. La nueva caché estática elimina las versiones estáticas anteriores; conserva los datos IndexedDB.
6. Revisar manualmente a 360 px, con terminal sin seleccionar, apertura cero, lector, cobro mixto, devolución, movimientos, arqueo, cierres POS, cierre con diferencia y desconexión.

## Pruebas técnicas

Las pruebas nuevas de dominio cubren importes, apertura fija, vínculo de terminal, turno superior a 24 horas, política y el ejemplo de efectivo esperado 270. Las pruebas PostgreSQL usan una base temporal `sistemi_pos_cash_test_<GUID>` y la eliminan al terminar. Cubren apertura concurrente/repetida, propietarios, separación de sucursales, conteo ciego, movimientos, cierre, reversos y conflicto de cierre con un ingreso de venta.

Para repetir las pruebas PostgreSQL, configurar `SISTEMI_TEST_CONNECTION` con una conexión de pruebas cuyo usuario pueda crear bases aisladas:

```powershell
dotnet test tests/SistemiPos.IntegrationTests --configuration Release --no-build --filter FullyQualifiedName~PosCash
dotnet test tests/SistemiPos.UnitTests --configuration Release --no-build --filter 'FullyQualifiedName~Cash|FullyQualifiedName~Sales'
```

No se realizan pruebas visuales ni navegación automática, conforme a la política del proyecto. El resultado concreto de compilación y pruebas se registra en la entrega.

### Resultado del 24/10/2026

- Compilación Release de la solución: correcta, 0 errores y 0 advertencias. Se corrigieron el acceso al conjunto de terminales y el orden de declaración del índice en el snapshot durante la validación.
- Pruebas unitarias de caja y ventas: **55 aprobadas, 0 fallidas, 0 omitidas**.
- Pruebas de integración POS/caja contra PostgreSQL local: **7 aprobadas, 0 fallidas, 0 omitidas**. La migración se aplicó en la base temporal; el modelo y el snapshot quedaron consistentes, sin cambios pendientes.
- Revisión de sintaxis de `pos-cash.js`, `site.js`, `pos-sync.js` y `service-worker.js`: correcta.
- `git diff --check`: correcto.
- Evidencia de integración: `artifacts/pos-cash/test-results/pos-cash.trx`.

Las pruebas de cierre concurrente verifican el conflicto de versión y la reversión del movimiento perdedor en la misma transacción. Las pruebas unitarias de ventas complementan la validación de stock y cobros. No sustituyen la prueba manual del recorrido completo de caja, dispositivos y proveedor bancario del entorno.

## Archivos principales

- Contrato y políticas: `src/SistemiPos.Application/Cash/IPosCashService.cs`.
- Servicio transaccional y estado: `src/SistemiPos.Infrastructure/Cash/PosCashService.cs`.
- Reglas y libro existentes: `CashService.cs`, `CashSession.cs`, `SaleService.cs` y sus repositorios.
- API: `src/SistemiPos.Web/Controllers/PosCashController.cs`.
- Barra y diálogos: `src/SistemiPos.Web/Views/Sales/_PosCash.cshtml` y `Form.cshtml`.
- Reloj, actualización y reintentos: `src/SistemiPos.Web/wwwroot/js/pos-cash.js`.
- Comprobante: `src/SistemiPos.Web/Views/PosCash/Receipt.cshtml`.
- Migración y snapshot: `src/SistemiPos.Infrastructure/Persistence/Migrations/PostgreSql/`.
- Pruebas: `PosCashRulesTests.cs`, `PosCashModelTests.cs`, `PosCashPersistenceTests.cs` y casos relacionados en `CashServiceTests.cs`.

## Reversión sin perder historia

Detener temporalmente la operación, respaldar y volver al binario anterior manteniendo las columnas y la tabla añadidas. No ejecutar `Down` en una base con turnos u operaciones nuevos: eliminaría sus vínculos y recibos de idempotencia. El rollback de esquema se reserva a una copia de prueba sin datos que deban conservarse.

Si se revierte el frontend, actualizar también su versión del service worker. No eliminar IndexedDB ni movimientos confirmados para resolver incidencias. Las cajas cerradas no se reabren ni se reescriben; cualquier corrección financiera debe quedar como una nueva operación autorizada.
