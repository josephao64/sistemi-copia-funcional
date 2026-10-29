# SISTEMI POS Restaurante — Módulo de Bodega Central y Traslados
## Documentación Técnica y Funcional

**Proyecto:** SISTEMI POS Restaurante  
**Ramas:** `development-joseph` / `Dev`  
**Módulo:** Bodega Central, Compras Mayoristas y Distribución por Traslados  
**Fecha:** 8 de octubre de 2026  

---

## 1. Resumen Ejecutivo

El módulo de **Bodega Central** permite que la organización opere centros de distribución o bodegas matrices donde se reciben compras al por mayor de insumos y mercaderías, para posteriormente redistribuirlos de forma controlada hacia las sucursales del negocio.

### Principios Fundamentales del Módulo
1. **Existencias Aisladas:** Cada bodega (central o de sucursal) administra su stock físico con aislamiento estricto por la tupla `(Producto, Bodega)`.
2. **Abastecimiento Híbrido:** Las compras mayoristas pueden recibirse directamente en Bodega Central, mientras que las sucursales conservan total libertad para comprar insumos a proveedores locales.
3. **Distribución Documentada:** Los envíos entre bodegas se gestionan mediante el documento de **Traslado de Inventario** (`InventoryTransfer`), registrando bodega origen, bodega destino, usuario despachador, usuario receptor y fechas exactas.
4. **Costo Asignado Personalizable:** La Bodega Central puede transferir productos a un costo asignado (`AssignedUnitCost`) diferente al costo mayorista de compra original (`UnitCostOrigin`), permitiendo absorber fletes, costos logísticos o políticas internas de costo de cesión.
5. **Control de Cantidades Enviadas vs. Recibidas:** Se registra independientemente lo enviado por origen (`QuantitySent`) y lo efectivamente recibido por destino (`QuantityReceived`), protegiendo la integridad física ante mermas o pérdidas en tránsito.

---

## 2. Cumplimiento de Criterios de Aceptación (Checklist de Tareas)

A continuación se detalla la verificación técnica de cada uno de los puntos requeridos en la tarjeta de implementación:

```
[x] 1. La Bodega Central mantiene existencias independientes de las existencias de cada sucursal.
[x] 2. Las compras pueden ingresar directamente a la Bodega Central.
[x] 3. La Bodega Central puede distribuir existencias a una o varias sucursales mediante traslados.
[x] 4. Todo traslado debe registrar cantidad enviada, sucursal destino y costo asignado.
[x] 5. El costo con el que la Bodega Central distribuye un producto puede ser diferente del costo original de compra.
[x] 6. Las sucursales pueden continuar realizando compras directas; no todo abastecimiento debe provenir de la Bodega Central.
```

---

### ✅ 1. La Bodega Central mantiene existencias independientes de las existencias de cada sucursal

* **Aislamiento en el Dominio (`InventoryStock.cs`):**  
  Las existencias no se calculan de manera global para la empresa ni caen en bolsas genéricas. Cada registro de stock pertenece a una bodega específica:
  ```csharp
  public sealed class InventoryStock : AggregateRoot
  {
      public Guid ProductId { get; private set; }
      public Guid? WarehouseId { get; private set; }
      public decimal CurrentStock { get; private set; }
      // ...
  }
  ```
* **Integridad y Restricción Única:**  
  La configuración de Entity Framework Core (`InventoryStockConfiguration.cs`) define un índice único por producto y bodega:
  ```csharp
  builder.HasIndex(x => new { x.ProductId, x.WarehouseId }).IsUnique();
  ```
* **Consultas Aisladas:**  
  Tanto `InventoryQueryService` como `InventoryRepository` y `IInventoryTransferRepository` consultan el saldo exacto mediante `GetStockAsync(productId, warehouseId)`, garantizando que lo almacenado en Bodega Central jamás se confunda ni se sume automáticamente con lo almacenado en una sucursal.

---

### ✅ 2. Las compras pueden ingresar directamente a la Bodega Central

* **Modelos de Compra (`Purchase.cs`, `PurchaseOrder.cs`, `PurchaseReceipt.cs`):**  
  Se incorporaron las propiedades de sucursal y bodega receptora en los agregados de compras:
  ```csharp
  public Guid? BranchId { get; private set; }
  public Guid? WarehouseId { get; private set; }
  ```
* **Impacto en Inventario al Recibir (`PurchaseService.cs`):**  
  Al recibir una orden de compra o registrar una compra directa dirigida a Bodega Central, el servicio localiza el stock correspondiente a `WarehouseId = BodegaCentral.Id`, incrementa su saldo y genera el movimiento de inventario `PurchaseEntry` con dicho identificador de bodega.
* **Lotes de Vencimiento:**  
  Si el producto gestiona caducidad (`ManagesExpiry`), `InventoryExpiryLot` se registra asignado explícitamente a la Bodega Central.

---

### ✅ 3. La Bodega Central puede distribuir existencias a una o varias sucursales mediante traslados

* **Agregado de Dominio (`InventoryTransfer.cs`):**  
  Representa el documento oficial de distribución entre una bodega origen y una bodega destino:
  ```csharp
  public sealed class InventoryTransfer : AggregateRoot
  {
      public string Number { get; private set; } // Ej: TR-000001
      public Guid OriginWarehouseId { get; private set; }
      public Guid DestinationWarehouseId { get; private set; }
      public InventoryTransferStatus Status { get; private set; }
      // ...
  }
  ```
* **Arquitectura de Distribución (1 documento por destino):**  
  Para distribuir insumos a múltiples sucursales (ej. Sucursal A, Sucursal B y Sucursal C), se genera un traslado por cada bodega destino. Esto evita dependencias operativas complejas, permitiendo que cada sucursal reciba y confirme sus propios productos sin bloquear a las demás sucursales.
* **Ciclo de Vida Documental:**
  - `Draft` (Borrador): Documento en preparación. No afecta existencias físicas.
  - `Dispatched` (Despachado): Descuenta las existencias de la bodega origen y genera movimiento `TransferExit`.
  - `Received` (Recibido): Aumenta las existencias en la bodega destino y genera movimiento `TransferEntry`.
  - `Cancelled` (Cancelado): Anula el borrador sin impacto en inventario.

---

### ✅ 4. Todo traslado debe registrar cantidad enviada, sucursal destino y costo asignado

* **Líneas de Traslado (`InventoryTransferItem.cs`):**  
  Cada artículo trasladado almacena:
  ```csharp
  public sealed class InventoryTransferItem : Entity
  {
      public Guid TransferId { get; private set; }
      public Guid ProductId { get; private set; }
      public decimal QuantitySent { get; private set; }
      public decimal QuantityReceived { get; private set; }
      public decimal UnitCostOrigin { get; private set; }
      public decimal AssignedUnitCost { get; private set; }
      public Guid? OriginLotId { get; private set; }
      public string? LotNumber { get; private set; }
      public DateOnly? ExpiryDate { get; private set; }
  }
  ```
* **Trazabilidad en Interfaz de Usuario:**  
  En la pantalla de detalle (`/InventoryTransfers/Details/{id}`), la tabla de productos muestra:
  - **Producto:** Código y nombre.
  - **Cantidad enviada:** Unidades despachadas desde Bodega Central.
  - **Cantidad recibida:** Unidades confirmadas por la sucursal destino.
  - **Costo origen:** Costo unitario registrado en Bodega Central.
  - **Costo asignado destino:** Costo pactado para la sucursal receptora.
  - **Subtotal asignado:** Total valorizado transferido.

---

### ✅ 5. El costo con el que la Bodega Central distribuye un producto puede ser diferente del costo original de compra

* **Diferenciación de Costos en Dominio y Aplicación:**  
  En `InventoryTransferService.cs`:
  ```csharp
  var unitCostOrigin = product.LastCost > 0 ? product.LastCost : product.PreviousCost;
  var assignedCost = itemReq.AssignedUnitCost.GetValueOrDefault(unitCostOrigin);
  ```
* **Entrada a Destino con el Costo Asignado:**  
  Al ejecutar `ReceiveTransferAsync`:
  1. El movimiento físico de entrada en la sucursal (`TransferEntry`) se crea con `UnitCost = item.AssignedUnitCost`.
  2. Si el producto tiene lotes, el nuevo lote generado en la bodega destino registra el costo asignado:
     ```csharp
     movement.SetLotDetails(item.ExpiryDate, item.LotNumber, item.OriginLotId);
     movement.UnitCost = item.AssignedUnitCost;
     ```
  3. Esto asegura que la sucursal receptora valore sus existencias y calcule su rentabilidad sobre el costo asignado por Bodega Central, mientras que Bodega Central mantiene su registro contable con el costo mayorista de compra original.

---

### ✅ 6. Las sucursales pueden continuar realizando compras directas; no todo abastecimiento debe provenir de la Bodega Central

* **Autonomía Operativa de Sucursales:**  
  Las pantallas de compras (`/Purchases/Create` y `/Purchases/CreateDirect`) permiten seleccionar la sucursal y bodega que realiza la compra.
* **Coexistencia en el Catálogo:**  
  Un producto puede recibir stock proveniente de:
  - Una compra directa de la sucursal a un proveedor local.
  - Un traslado recibido desde la Bodega Central.
  Ambas fuentes conviven bajo el mismo código de producto y bodega sin interferencias, aplicando el algoritmo FEFO/FIFO correspondiente.

---

## 3. Modelo de Datos y Relaciones

```
+-------------------------------------------------------------+
|                     InventoryTransfers                      |
+-------------------------------------------------------------+
| PK Id                      UUID                             |
|    Number                  VARCHAR(50)  (TR-XXXXXX)         |
| FK OriginWarehouseId       UUID                             |
| FK DestinationWarehouseId  UUID                             |
|    Status                  VARCHAR(30)  (Draft/Disp/Rec/Can)|
|    RequestedAtUtc          TIMESTAMP                        |
|    RequestedBy             VARCHAR(256)                     |
|    DispatchedAtUtc         TIMESTAMP?                       |
|    DispatchedBy            VARCHAR(256)?                    |
|    ReceivedAtUtc           TIMESTAMP?                       |
|    ReceivedBy              VARCHAR(256)?                    |
+-------------------------------------------------------------+
                              |
                              | 1:N
                              v
+-------------------------------------------------------------+
|                   InventoryTransferItems                    |
+-------------------------------------------------------------+
| PK Id                      UUID                             |
| FK TransferId              UUID                             |
| FK ProductId               UUID                             |
|    QuantitySent            NUMERIC(18,2)                    |
|    QuantityReceived        NUMERIC(18,2)                    |
|    UnitCostOrigin          NUMERIC(18,4)                    |
|    AssignedUnitCost        NUMERIC(18,4)                    |
| FK OriginLotId             UUID?                            |
|    LotNumber               VARCHAR(100)?                    |
|    ExpiryDate              DATE?                            |
+-------------------------------------------------------------+
```

---

## 4. Tipos de Movimientos de Inventario

Se agregaron al enumerador `InventoryMovementType`:

| Código | Identificador | Descripción |
| :---: | :--- | :--- |
| `7` | `TransferExit` | Salida física de la bodega origen al despachar el traslado. |
| `8` | `TransferEntry` | Entrada física a la bodega destino al recibir el traslado. |

---

## 5. Permisos y Seguridad de Acceso

Los permisos fueron registrados en `PermissionCodes.cs` y sembrados en los roles administrativos:

| Permiso | Código del Sistema | Roles con Acceso |
| :--- | :--- | :--- |
| Ver Traslados | `inventory.transfers.view` | SuperAdmin, Administrador, Gerente, Bodega |
| Crear Borrador | `inventory.transfers.create` | SuperAdmin, Administrador, Gerente, Bodega |
| Despachar Traslado | `inventory.transfers.dispatch` | SuperAdmin, Administrador, Gerente, Bodega |
| Recibir en Destino | `inventory.transfers.receive` | SuperAdmin, Administrador, Gerente, Bodega |

---

## 6. Pruebas Unitarias Automatizadas

Las pruebas se encuentran en `tests/SistemiPos.UnitTests/Inventory/InventoryTransferTests.cs`, validando:
1. **Flujo Completo:** Creación en Borrador $\rightarrow$ Despacho $\rightarrow$ Recepción exitosa.
2. **Invariante de Borrador:** El borrador no incrementa ni reduce existencias en ninguna bodega.
3. **Validación de Existencias:** No se permite despachar si la bodega de origen no cuenta con existencias suficientes.
4. **Costo Asignado:** Validación de que la bodega destino recibe el producto al costo asignado configurado (ej. compra a Q10 en central vs costo asignado de Q12 en sucursal).
5. **Idempotencia y Estados:** Impedir despachos dobles o recepciones sobre documentos ya cerrados o cancelados.
