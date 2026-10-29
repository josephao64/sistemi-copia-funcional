# OFFLINE_SYNC.md

# Operación offline y sincronización

## Alcance

La capacidad offline del MVP aplica únicamente al Punto de Venta.

Objetivos:

- Vender sin Internet.
- Guardar ventas localmente.
- Limitar ventas al último stock sincronizado disponible en el dispositivo.
- Sincronizar operaciones pendientes cuando vuelva Internet.

No se implementa operación offline para compras, inventario administrativo, usuarios, permisos, reportes, proveedores, configuración ni cambios de precios.


## Tecnologías

- PWA.
- Service Worker.
- Cache Storage para recursos estáticos.
- IndexedDB para datos estructurados locales.
- API ASP.NET Core para sincronización.

No depender exclusivamente de Background Sync del navegador.

La sincronización debe ejecutarse también:

- Al detectar recuperación de conexión.
- Al abrir o reanudar la aplicación.
- Mediante `Sincronizar ahora`.

## Datos locales

Mantener localmente únicamente lo necesario:

- Productos habilitados para venta.
- Código interno.
- Código de barras.
- Nombre.
- Precio.
- Estado, modo y reglas activas/inactivas de multiprecio.
- Último stock sincronizado.
- Clientes básicos previamente sincronizados.
- Sesión de caja necesaria para operación.
- Ventas pendientes.
- Estado de sincronización.
- Configuración monetaria vigente.

## Regla de stock offline

El dispositivo recibe un stock durante la última sincronización.

Ese valor se convierte en su stock local disponible.

Ejemplo:

```text
Último stock sincronizado: 10
Venta offline: 3
Stock local disponible: 7
```

El dispositivo no puede vender más de su stock local disponible.

Si el stock local llega a cero:

```text
Stock local disponible: 0
```

la venta offline de ese producto queda bloqueada hasta una nueva sincronización.

El stock offline no representa stock central en tiempo real.

## Venta offline

Cada venta debe incluir como mínimo:

- `ClientOperationId` único.
- Fecha y hora de creación local.
- Usuario.
- Sesión de caja.
- Cliente o consumidor final.
- Detalle.
- Precio histórico.
- La venta offline no admite descuentos porque requieren aprobación en línea.
- Total.
- Forma de pago.
- Datos específicos del pago.
- Estado de sincronización.
- Configuración monetaria vigente.

## Estados

```text
Pending
Syncing
Synced
Error
Conflict
```

### Pending
Operación guardada localmente y no confirmada por servidor.

### Syncing
Operación en proceso de envío.

### Synced
Servidor confirmó persistencia exitosa.

### Error
Falló el proceso técnico.

### Conflict
El servidor detectó una inconsistencia que requiere tratamiento.

## Idempotencia

`ClientOperationId` debe ser único.

El servidor debe mantener una restricción o mecanismo equivalente que impida registrar dos veces la misma operación.

Reenviar una operación ya sincronizada debe devolver el resultado existente sin repetir:

- Venta.
- Pago.
- Movimiento de inventario.
- Movimiento de caja.

## Flujo de venta offline

1. Validar sesión local.
2. Buscar producto en IndexedDB.
3. Validar stock local disponible.
4. Calcular venta.
5. Crear `ClientOperationId`.
6. Guardar venta con estado `Pending`.
7. Descontar stock local.
8. Generar comprobante local cuando aplique.
9. Mostrar operación pendiente de sincronización.

## Flujo de sincronización

1. Detectar conexión.
2. Leer operaciones `Pending` o reintentables.
3. Cambiar a `Syncing`.
4. Enviar operación a API.
5. Validar idempotencia en servidor.
6. Validar reglas de negocio.
7. Confirmar venta en transacción.
8. Registrar movimientos.
9. Responder resultado.
10. Marcar `Synced`, `Error` o `Conflict`.
11. Actualizar catálogo y stock local cuando corresponda.

## Interfaz

La captura se realiza siempre desde el formulario normal de `Ventas`. No debe existir un segundo formulario exclusivo para ventas offline.

Cuando el dispositivo pierde conexión, el formulario normal debe guardar la venta en IndexedDB y generar su comprobante provisional. Al recuperar conexión, la aplicación debe intentar sincronizar automáticamente, incluso mediante Background Sync cuando el navegador lo soporte.

La pantalla `POS offline` funciona únicamente como monitor para:

- Consultar conexión y última actualización.
- Ver operaciones pendientes, sincronizadas, con error o conflicto.
- Consultar el último comprobante provisional.
- Ejecutar manualmente `Sincronizar ahora` o actualizar los datos locales.

Mostrar de forma visible:

```text
Conectado
```

o:

```text
Sin conexión
3 operaciones pendientes
[Sincronizar ahora]
```

Cuando el dispositivo esté offline, mostrar el stock como:

`Último stock sincronizado`

No mostrarlo como stock central actual.

## Persistencia local

Solicitar almacenamiento persistente del navegador cuando sea posible.

No recomendar uso del POS en modo incógnito o privado.

Las operaciones pendientes no deben eliminarse hasta confirmación del servidor.

## Alcance offline inicial

Permitido:

- Punto de venta.
- Efectivo.
- Transferencia.
- POS.
- Clientes sincronizados.
- Consumidor final.
- Comprobante local.
- Consulta del último stock sincronizado.

No permitido inicialmente:

- Compras.
- Ajustes de inventario.
- Administración.
- Usuarios y permisos.
- Cambios de precios.
- Configuración.
- Proveedores.
- Reportes globales.

## Aprobación de descuentos

La autorización de descuentos se conserva como una regla obligatoria del flujo actual de Ventas.
Una venta nueva no puede guardarse offline si contiene descuentos, porque no es posible solicitar ni comprobar la aprobación de un superior sin conexión.
El formulario debe informar esta condición antes de crear la operación local. La API de sincronización también rechaza ventas offline con descuento para impedir que se omita la validación del servidor.


## Transferencia offline

Cuando una venta offline utilice transferencia:

- Registrar banco origen.
- Registrar cuenta destino seleccionada del catálogo previamente sincronizado.
- Registrar referencia.
- Registrar monto.
- Guardar inicialmente como `Received`.
- Una transferencia offline en estado `Received` permite confirmar la venta.
- Mantener la operación pendiente de sincronización.
- La verificación se realiza posteriormente por un usuario autorizado cuando exista conexión.



## Límites operativos offline

La operación offline se limita por dispositivo a:

- Máximo 7 días sin sincronización.
- Máximo 500 ventas pendientes de sincronización.

Se aplica el límite que ocurra primero.

Cuando se alcance cualquiera de los dos límites, el sistema debe bloquear nuevas ventas offline y exigir sincronización antes de continuar.

Las operaciones ya registradas localmente deben conservarse hasta sincronización exitosa.



## Comprobante offline

Cuando una venta se realice sin conexión, el comprobante debe ser provisional.

Debe indicar de forma visible:

- `Pendiente de sincronización`.
- Identificador único de operación local.
- Fecha y hora local.
- Datos del negocio configurados.
- Productos.
- Cantidades.
- Precios.
- Descuentos.
- Total.
- Forma de pago.
- Cambio o referencia cuando corresponda.
- Impuesto visible u oculto según configuración.

El comprobante provisional puede generarse en:

- Ticket térmico.
- PDF.

La venta offline no recibe numeración definitiva `V-xxxxxx` hasta sincronizarse correctamente con el servidor.

Al sincronizar:

`Operación local -> Venta definitiva`

Ejemplo:

`OFF-8F42A1 -> V-000153`

El identificador local debe conservarse relacionado con la venta definitiva para trazabilidad.
