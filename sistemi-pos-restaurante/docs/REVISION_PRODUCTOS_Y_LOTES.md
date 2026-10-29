# Revisión de Productos y lotes antes de Recetas

## Actualización vigente: checklist Productos de Fase 2

Por aprobación del usuario, las nuevas altas usan Insumo, Preparado, Reventa, Subreceta y Empaque; los otros tipos almacenados se conservan. Se habilita mínimo por sucursal con herencia del mínimo general y control por lote configurable por producto. Los productos existentes conservan lotes habilitados y no se cambia el indicador con historial o existencias. Caducidad requiere lotes. Esta actualización sustituye las restricciones anteriores de cuatro tipos, mínimo exclusivamente general y lotes obligatorios para todo producto. Las entregas descritas debajo se conservan como antecedente.

Comprobación manual: crear un producto de cada tipo; guardar presentaciones equivalentes a unidad base; asignar mínimos y precios distintos en dos sucursales; comprobar herencia con campo vacío y mínimo cero; comprar, vender y devolver un producto sin lotes; comprobar Kardex sin asignaciones de lote; activar caducidad y comprobar que exige lotes; intentar cambiar el indicador de un producto usado. Aplicar primero la migración PostgreSQL de control opcional por lote.

## Entrega actual

### Complemento: recepción de lotes y alertas

Compra directa y recepción final capturan código de lote, o generan uno al dejarlo vacío, con caducidad obligatoria cuando corresponda. La recepción asocia los datos al producto real de la línea de la orden, incluso si llega solo cantidad de empaques o no se envía el identificador del producto. Las alertas muestran lote y bodega, con saldo independiente y período configurado en el producto. Se conserva FEFO con desempate por ingreso para caducidad, FIFO sin caducidad y exclusión de vencidos en consumo automático.

Prueba manual: recibir el mismo producto en dos compras con lotes y vencimientos distintos; revisar cantidades y fecha de ingreso en Inventario > Ver lotes; vender una cantidad que abarque varios lotes y comprobar que comienza por el vencimiento más próximo; cambiar los días de alerta y revisar Productos > Por caducar; comprobar que vencidos y lotes sin saldo se tratan según las reglas; recibir también un producto sin control por lote. Esta entrega no requiere una migración adicional.

Se ajusta el maestro de productos: unidad base requerida y protegida con historial, umbral propio de días sin movimiento, cuatro tipos para nuevas altas, stock mínimo por producto, costos procedentes de compras y eliminación controlada. Los tipos históricos se conservan hasta su reclasificación manual. Los datos de nombres, marcas, imágenes y variantes existentes se conservan.

Los productos antiguos sin umbral propio no generan alertas; al editarlos se exige configurar el valor. No se asignan 30 días automáticamente. El cálculo conserva la referencia temporal documentada actualmente: última venta válida, primer ingreso o fecha de creación. El consumo por recetas deberá incorporarse cuando exista ese módulo.

## Revisión del adjunto

| Reglas | Resultado |
|---|---|
| 1–4, 19: eliminación, estados e historial | Eliminación protegida por relaciones operativas actuales; desactivación y reactivación conservan el registro. La auditoría del catálogo permanece. Las futuras relaciones de recetas deberán añadirse. |
| 5: código único | Se conserva la validación existente y el índice único de base de datos. |
| 6–7: unidad base | Se exige una unidad existente; se bloquean cambios cuando hay uso o relaciones. |
| 8: conversiones | Existen catálogo de conversiones y presentaciones con factor positivo. Falta certificar la aplicación del factor en todos los flujos reales de recepción antes de afirmar cobertura completa. |
| 9–10: tipos | Se agregan Ingrediente, Producto de venta y Bebida, conservando Insumo y los tipos históricos. No se cambian valores históricos automáticamente. La clasificación por sí sola no recalcula documentos. |
| 11–12: mínimos y días | Configuración por producto. Sin herencia de días generales; mínimo expresado en unidad base. |
| 13: caducidad | El producto configura caducidad y alerta; existen recepciones con fecha y lotes. Se protege el cambio del indicador cuando hay uso. |
| 14–15: ingredientes y recetas | Se conserva la detección de duplicados del catálogo. El módulo de recetas y sus cantidades aún no se implementan en esta entrega. |
| 16–18: proveedor y precios | El maestro no selecciona un proveedor único. Los costos no se capturan en el maestro. Se oculta precio de venta para ingredientes; la administración de precios de Menú sigue pendiente y el mecanismo comercial anterior se conserva para los demás tipos. |
| 20: datos de lote | Parcial: el modelo actual guarda producto, vencimiento, saldo y documento origen; faltan fecha de ingreso y cantidad inicial persistidas. |
| 21, 23–24: FEFO y vencidos | Se centraliza FEFO en ventas, se permite consumir varios lotes y se excluyen vencidos. Si los lotes vigentes no alcanzan, se rechaza sin consumirlos parcialmente. No equivale a validar todas las salidas de inventario. |
| 22: FIFO | Pendiente: no existen lotes para productos sin caducidad ni fecha de ingreso para desempatar vencimientos iguales. |
| 25: trazabilidad | Pendiente: falta una relación persistente movimiento–lote–cantidad. |

## Condiciones para la siguiente etapa

Antes de conectar recetas al descuento automático se necesita ampliar los lotes y sus asignaciones, definir la transición de existencias antiguas sin lote, unificar entradas/salidas/devoluciones y usar cantidades en unidad base. No deben inventarse fechas, cantidades originales ni lotes para reconstruir historia inexistente. FIFO, trazabilidad y Menú no se consideran terminados por esta revisión.

## Comprobación manual

Crear un ingrediente PEPPERONI con unidad Gramo, mínimo 500 y 30 días; editar un producto antiguo sin umbral; intentar cambiar la unidad de un producto usado; desactivar/reactivar; intentar eliminar productos usados y uno nuevo sin relaciones. Revisar que el listado y el formulario muestren los tipos antiguos correctamente y que los nuevos tipos estén disponibles.

## Segunda entrega: presentaciones y trazabilidad

Se conectan las presentaciones propias del producto con compra directa y recepción, guardando factor y unidad base en la factura. Se incorpora FIFO, desempate FIFO para FEFO, asignaciones movimiento–lote y reintegros a lotes originales. Inventario ofrece la consulta de lotes, apertura y baja explícita por lote.

Las limitaciones de lotes señaladas en la revisión inicial quedan sustituidas por esta implementación para movimientos nuevos. No se reconstruye trazabilidad anterior. Los lotes históricos pasan a apertura con su saldo y vencimiento conocido; las existencias sin lote y con caducidad requieren completar los vencimientos reales. Una devolución histórica sin asignaciones originales requiere conciliación.

Recetas y la administración de precios de Menú siguen fuera de esta entrega. La toma física positiva de un producto con caducidad no puede registrarse sin identificar el vencimiento de su entrada; se bloquea si falta ese dato.

Pruebas manuales: comprar dos bolsas de 2500 g y verificar 5000 g; cambiar la presentación y comprobar que la factura anterior conserva su factor; consumir entre varios lotes; comprobar que un vencido no se usa; registrar una baja con motivo; asignar saldo inicial sin alterar la existencia; revisar una devolución y sus lotes.
