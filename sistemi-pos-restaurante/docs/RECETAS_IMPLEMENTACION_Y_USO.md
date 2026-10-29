# Recetas, producción y costos — fase 5

Implementación del alcance PRO-01 a PRO-08 del plan aprobado. Las pantallas están en **Inventario > Recetas** y **Inventario > Producción**. El catálogo sigue siendo único y compartido; los consumos y existencias pertenecen a una bodega autorizada.

## Productos del inventario

Los componentes se seleccionan de `Products`: productos activos, concretos (incluidas variantes) y con unidad base activa. Se incluyen artículos de reventa y otras clasificaciones inventariables existentes; no se crean ingredientes paralelos. Un registro que agrupa variantes no puede seleccionarse. No se exige saldo positivo para definir la receta: la disponibilidad se valida al operar. Las cantidades se convierten mediante las equivalencias del catálogo.

Preparados y subrecetas pueden tener receta. Una preparación utilizada como componente debe tener una receta activa configurada para producir existencias. Se mantienen las validaciones de duplicados, autorreferencias, ciclos y compatibilidad de unidades. Los productos usados en versiones históricas siguen protegidos frente a eliminación y cambios de tipo o unidad.

## Versiones y modos de consumo

Cada guardado conserva un `RecipeVersion` inmutable, con nombre del producto, rendimiento, unidades, precisión, instrucciones, modo y componentes. Las subrecetas apuntan a su versión concreta. La receta actual conserva `xmin` para impedir ediciones obsoletas; `Revision` es el número visible de versión.

- **Elaborar al vender:** consume los componentes directos al confirmar la venta. Una pizza consume masa preparada, queso, salsa y empaque. No explota nuevamente los ingredientes internos de la masa.
- **Producir primero:** una orden consume los componentes y crea existencias del producto elaborado. La venta consume esas existencias. Las subrecetas usan obligatoriamente este modo.

La venta guarda la versión y un detalle inmutable del consumo y costo de cada componente. Una edición posterior no cambia lo vendido. La anulación revierte ese detalle original. Una devolución de un preparado elaborado al vender debe registrarse sin reingreso de ingredientes: devolver comida elaborada no reconstruye materias primas. Las ventas históricas anteriores a esta ampliación conservan su comportamiento original y no reciben versiones inventadas.

## Producción

1. Crear una orden con receta, cantidad teórica, bodega de origen, bodega de destino, lote, vencimiento cuando corresponda y fecha prevista. El responsable es el usuario conectado. La orden fija la versión utilizada.
2. Consultar costo y disponibilidad desde el detalle.
3. Confirmar cantidad real y motivo obligatorio si difiere de la teórica. Se admite cero para pérdida total.
4. La confirmación consume los componentes correspondientes a la cantidad teórica y agrega solo la cantidad real al destino. Registra movimientos y lotes en una misma transacción serializable. No permite stock negativo y utiliza el control de lotes existente (FEFO para productos con vencimiento).

`WasteQuantity = max(teórico - real, 0)`. El rendimiento mayor al previsto también exige motivo y permanece trazado. El costo del lote se distribuye entre las unidades reales obtenidas; cuando el rendimiento es cero, no existe costo unitario de salida.

La creación usa un identificador de operación único. Reintentar el mismo formulario devuelve la orden existente; reutilizarlo con datos distintos se rechaza. Reconfirmar la misma orden con igual rendimiento no duplica movimientos. Una orden confirmada es inmutable. Una falla revierte consumos, entrada, lotes y confirmación juntos.

## Costo y disponibilidad

La consulta de una versión permite indicar cantidad, bodega y fecha/hora de costo (Guatemala). Utiliza el último costo histórico confirmado de compras o producción vigente a esa fecha; la migración guarda una referencia inicial del costo de los productos existentes. Cuando una subreceta todavía no tiene costo de producción o compra se calcula su costo teórico a partir de la versión fijada. Un costo desconocido se muestra como incompleto, sin inventar precios históricos. Los consumos de cada producción y venta conservan el costo y su referencia.

El costo por componente es cantidad base por costo unitario; se suma para el lote y se divide entre el rendimiento para obtener el costo unitario teórico. Los importes se identifican con la moneda configurada. El inventario, movimientos, lotes y costos operativos conservan hasta seis decimales.

La disponibilidad es actual incluso si se consulta un costo histórico. Se calcula el mínimo de `disponible del componente × rendimiento / cantidad base del componente`, redondeado hacia abajo a la precisión del producto elaborado. Se muestra el componente limitante. Se consideran los lotes vigentes y se consume la subreceta disponible, sin suponer producción automática. Es una consulta, no una reserva: el backend vuelve a validar al confirmar.

## Permisos y sucursales

- `recipes.view`: listado, versiones, costos y disponibilidad.
- `recipes.manage`: crear y editar recetas (requiere además acceso de consulta).
- `production.view`: listado y detalle de órdenes.
- `production.manage`: planificar (la selección de recetas requiere `recipes.view`).
- `production.confirm`: confirmar rendimiento y movimientos.

El administrador recibe estos permisos. Origen y destino deben estar activos y pertenecer a sucursales autorizadas. El listado de producción usa la sucursal activa para el origen; un acceso directo vuelve a validar ambas bodegas. La venta usa la bodega de la caja y conserva su ubicación. El POS consulta disponibilidad producible para recetas al vender.

## Migración y datos existentes

La migración `AddRecipeProductionAndHistory` agrega versiones, órdenes y referencia inicial de costos; amplía cantidades y costos del inventario a seis decimales; agrega referencias de receta y consumos a ventas. Conserva la receta vigente de cada producto como primera versión disponible y enlaza las subrecetas existentes. El modo inicial es producción para subrecetas y consumo al vender para preparados. Revisar esta elección operativa en cada preparado existente antes de venderlo.

No reconstruye fórmulas ni costos anteriores desconocidos. Los movimientos de producción usan el número `PRO-...` para enlazar Kardex y orden. La fecha prevista no retrodata movimientos: se usa la fecha real de confirmación. La reversión de esta migración elimina los nuevos históricos y reduce precisión; no debe aplicarse sobre operaciones reales confirmadas.

## Validación y revisión manual

Pruebas focalizadas: reglas y selección de catálogo, persistencia/versiones, modelo EF, flujo de producir masa y vender pizza, snapshots de venta, anulación histórica, idempotencia, merma, rollback y alcance de sucursales. La prueba PostgreSQL usa una base temporal con nombre `sistemi_production_test_*`, eliminada al terminar, mediante `SISTEMI_TEST_CONNECTION`.

La revisión visual corresponde al usuario conforme a la política del repositorio. Comprobar: selectores con productos reales y variantes; creación y edición en modal; cantidades fraccionarias; costo y limitante; producción con 72 previstas y 70 reales; lote y Kardex en origen/destino; venta y anulación de pizza después de editar la receta; permisos de encargado y consulta desde tableta.
