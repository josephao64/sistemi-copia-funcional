# DOMAIN_RULES.md

## Ventas

### RB-001
Una venta confirmada descuenta inventario.

### RB-002
Una venta confirmada no puede modificarse directamente.

### RB-003
Las correcciones se realizan mediante anulación o devolución.

### RB-004
El total de una venta se calcula a partir de su detalle.

### RB-005
El precio utilizado en cada línea se conserva históricamente.

### RB-006
Cada venta registra una única forma de pago en el MVP.

### RB-007
No confirmar una venta dos veces.

### RB-008
No confirmar una venta cuando la existencia disponible sea insuficiente.

### RB-PRICE-001
Con multiprecios deshabilitado se usa el precio base. En modo de cantidad mínima se aplica a toda la línea la regla activa más alta menor o igual a la cantidad; en modo exacto solo se aplica el total especial cuando la cantidad de la línea coincide exactamente.

### RB-PRICE-002
Las cantidades de reglas son únicas por producto y mayores que cero; los precios no son negativos y las reglas inactivas no se aplican. Deshabilitar multiprecios conserva las reglas.

### RB-PRICE-003
El detalle de venta conserva el importe bruto y el precio unitario equivalente realmente aplicados, mientras el inventario descuenta la cantidad física vendida.

## Pagos

### RB-009 — Efectivo
`monto_recibido >= total_venta`

`cambio = monto_recibido - total_venta`

### RB-010 — Transferencia
La transferencia puede almacenar banco y referencia.

### RB-011 — POS
El pago POS puede almacenar tipo de tarjeta, autorización, voucher o referencia.

## Inventario

### RB-012
La existencia no se modifica directamente.

### RB-013
Todo cambio de existencia genera movimiento de inventario.

### RB-014
Una compra confirmada genera entrada.

### RB-015
Una venta confirmada genera salida.

### RB-016
Una devolución genera entrada cuando el producto regresa físicamente al inventario.

### RB-017
Una anulación revierte los movimientos afectados cuando corresponda.

### RB-018
No permitir inventario negativo.

### RB-019
La existencia actual debe ser consistente con los movimientos confirmados.

## Compras

### RB-020
Una compra confirmada aumenta inventario.

### RB-021
El costo utilizado en cada línea se conserva históricamente.

### RB-022
Una compra confirmada no se elimina físicamente.

## Caja

### RB-022A — Propiedad de la caja
Una sesión de caja pertenece a un usuario. Un usuario solo puede mantener una sesión abierta y la sesión persiste aunque cierre su sesión web. No se asocia a terminales o computadoras.

### RB-022B — Cierre inmutable
El cierre conserva todos los arqueos y reconteos, diferencias de efectivo, cierres POS y autorizaciones. Una sesión cerrada no admite movimientos ni ajustes posteriores.

### RB-023
Toda venta debe asociarse a una sesión de caja activa.

### RB-024
Una sesión de caja registra apertura y cierre.

### RB-025
Los totales se separan por:

- Efectivo.
- Transferencia.
- POS.

### RB-026
El efectivo esperado no incluye transferencia ni POS.

### RB-027
El cierre registra:

- Efectivo esperado.
- Efectivo contado.
- Diferencia.

### RB-028
Una caja cerrada no se modifica directamente.

## Descuentos

### RB-029
Los descuentos requieren permiso.

### RB-030
Todo descuento debe conservar:

- Tipo.
- Valor.
- Usuario.
- Motivo cuando aplique.

## Anulaciones

### RB-031
Una anulación conserva la venta original.

### RB-032
Una anulación registra motivo, usuario, fecha y hora.

### RB-033
Una anulación revierte inventario cuando corresponda.

## Devoluciones

### RB-034
Una devolución puede ser total o parcial.

### RB-035
Una devolución conserva relación con la venta original.

### RB-036
Una devolución registra motivo, usuario, fecha y hora.

### RB-037
El producto devuelto que regresa físicamente genera entrada de inventario.

## Persistencia

### RB-038
No eliminar físicamente:

- Ventas.
- Compras confirmadas.
- Movimientos de inventario.
- Cajas cerradas.
- Auditoría.

### RB-039
Usar estados explícitos para documentos.

Estados aplicables según entidad:

- Borrador.
- Confirmado.
- Anulado.
- Devuelto parcialmente.
- Devuelto.
- Cerrado.
- Inactivo.

## Seguridad

### RB-039A — Autorizaciones por permiso
Una autorización valida las credenciales normales de un segundo usuario y el permiso específico requerido. El nombre del rol no concede por sí mismo autoridad. Deben conservarse el solicitante y el autorizador.

### RB-040
Los permisos deben validarse en backend.

### RB-041
No confiar en totales, precios, descuentos ni permisos enviados por cliente.

## Auditoría

### RB-042
Las acciones críticas deben registrar:

- Usuario.
- Fecha y hora.
- Acción.
- Módulo.
- Registro afectado.
- Documento relacionado cuando aplique.

## Dinero

### RB-043
Los valores monetarios deben manejarse con precisión decimal.

## Transacciones

### RB-044
Deben ejecutarse de forma atómica:

- Confirmación de venta.
- Anulación.
- Devolución.
- Confirmación de compra.
- Ajuste de inventario.
- Cierre de caja.

### RB-045
Ante fallo parcial, la operación completa debe revertirse.

## Configuración

### RB-046
Las formas de pago iniciales son:

- Efectivo.
- Transferencia.
- POS.

### RB-047
El diseño debe permitir agregar nuevas formas de pago posteriormente sin cambiar la lógica central de ventas.

## Decisiones no definidas

No asumir sin requerimiento adicional:

- Integración FEL.
- Crédito.
- Cuentas por cobrar.
- Multisucursal.
- Traslados entre sucursales.
- Cotizaciones.
- Pedidos.
- Promociones avanzadas.
- E-commerce.
- Integraciones externas.
