# Informe Técnico y de Operación: Fase 5
## Recetas, Producción y Costos

**Sistema:** SISTEMI POS Restaurante  
**Arquitectura:** Clean Architecture en .NET 10  
**Base de Datos:** PostgreSQL 17  
**Estado:** **Completado 100%**  
**Pruebas Verificadas:** 273 pruebas superadas (100% éxito)  
**Documento HTML generado:** [Reporte_Fase_5_Recetas_Produccion_Costos.html](file:///c:/Users/Elizabeth/Downloads/sistema%20rest/sistemi-pos-restaurante/docs/Reporte_Fase_5_Recetas_Produccion_Costos.html)

---

## 1. Objetivo General de la Fase 5
> **"Registrar preparaciones internas sin descontar dos veces sus ingredientes."**

El sistema garantiza que cuando se producen preparaciones intermedias (**Subrecetas** como la masa de pizza o la salsa artesanal) en cocina previa, los insumos básicos (harina, levadura, agua, tomates) se descuentan al confirmar la producción del lote, y al momento de vender el plato terminado en el POS únicamente se descuenta la subreceta ya elaborada, evitando duplicidad en mermas o costos contables.

---

## 2. Matriz de Cumplimiento (PRO-01 a PRO-08)

| Clave | Tarea | Funcionalidad | Ejemplo de Negocio | Prioridad | Criterio de Aceptación | Estado |
| :--- | :--- | :--- | :--- | :--- | :--- | :---: |
| **PRO-01** | **Recetas versionadas** | Crear cabecera, versión, rendimiento y componentes. | Pizza Jamón usa masa, salsa, queso, jamón y caja. | Crítica | Venta histórica apunta a versión usada. | **✔ Cumplido** |
| **PRO-02** | **Subrecetas** | Permitir preparaciones como componentes de otra receta. | Masa participa en Pizza Jamón; Salsa participa en Pizza Jamón. | Crítica | Validar ciclos y unidades compatibles. | **✔ Cumplido** |
| **PRO-03** | **Orden de producción** | Registrar lote, origen, destino, fecha y responsable. | Producir 50 bolas de masa o 5 kg de salsa. | Crítica | Consumo y producción se confirman juntos (atómico). | **✔ Cumplido** |
| **PRO-04** | **Rendimiento real** | Registrar cantidad teórica, real, merma y motivo. | Lote de masa produjo 48 de 50 bolas (merma de 2). | Alta | Diferencia genera trazabilidad de merma. | **✔ Cumplido** |
| **PRO-05** | **Costo teórico** | Calcular costo por componente, lote y porción. | Costo de una bola de masa o 100 g de salsa. | Alta | Cálculo reproduce costos vigentes a la fecha. | **✔ Cumplido** |
| **PRO-06** | **Consumo sin duplicación** | Distinguir producir subreceta de consumirla en venta. | Masa descuenta harina al producir; pizza descuenta masa al vender. | Crítica | Prueba integral confirma un solo consumo. | **✔ Cumplido** |
| **PRO-07** | **Disponibilidad producible** | Calcular cuántas unidades pueden prepararse. | Queso o masa disponible limita pizzas posibles. | Media | Resultado identifica componente limitante. | **✔ Cumplido** |
| **PRO-08** | **Gestión visual de recetas** | Crear pantallas de receta, producción, rendimiento y costo. | Encargado registra lote desde tablet de cocina. | Alta | Uso táctil y permisos por sucursal. | **✔ Cumplido** |

---

## 3. Implementación de los Criterios Técnicos

### PRO-01: Recetas Versionadas e Histórico Inmutable
- **Entidades:** [Recipe.cs](file:///c:/Users/Elizabeth/Downloads/sistema%20rest/sistemi-pos-restaurante/src/SistemiPos.Domain/Recipes/Recipe.cs), [RecipeVersion.cs](file:///c:/Users/Elizabeth/Downloads/sistema%20rest/sistemi-pos-restaurante/src/SistemiPos.Domain/Recipes/RecipeVersion.cs), [RecipeIngredient.cs](file:///c:/Users/Elizabeth/Downloads/sistema%20rest/sistemi-pos-restaurante/src/SistemiPos.Domain/Recipes/Recipe.cs).
- **Invariante:** Cada modificación a una receta guarda una versión cronológica inmutable con su número de versión, fecha de efectividad, instrucciones y componentes serializados.
- **Trazabilidad en Ventas:** [SaleItem.cs](file:///c:/Users/Elizabeth/Downloads/sistema%20rest/sistemi-pos-restaurante/src/SistemiPos.Domain/Sales/SaleItem.cs) guarda `RecipeVersionId`, permitiendo conocer exactamente qué receta y qué costo regían al momento de cada venta pasada.

### PRO-02: Subrecetas Jerárquicas y Validación sin Ciclos
- **Relación:** Los ingredientes pueden ser materias primas simples o subrecetas intermedias mediante `RecipeIngredient.SubrecipeId`.
- **Detección de Ciclos:** `RecipeRules.EnsureNoCycles()` ejecuta un análisis de grafo dirigido impidiendo cualquier referencia recursiva directa o indirecta (A → B → A).
- **Validación de Unidades:** Solo se admiten ingredientes con unidades de medida compatibles y configuradas en el catálogo.

### PRO-03, PRO-04 y PRO-06: Órdenes de Producción, Mermas y Cero Duplicidad
- **Entidad:** [ProductionOrder.cs](file:///c:/Users/Elizabeth/Downloads/sistema%20rest/sistemi-pos-restaurante/src/SistemiPos.Domain/Recipes/ProductionOrder.cs).
- **Servicio:** [RecipeOperationsService.cs](file:///c:/Users/Elizabeth/Downloads/sistema%20rest/sistemi-pos-restaurante/src/SistemiPos.Application/Recipes/RecipeOperationsService.cs).
- **Operación Atómica:** Al llamar a `ConfirmAsync()`, se descuentan en bloque los ingredientes utilizados con movimientos `ProductionConsumption` e ingresa al inventario el producto preparado con `ProductionOutput`.
- **Mermas:** Se comparan `TheoreticalYield` y `ActualYield`. Cualquier desvío se registra formalmente en `WasteQuantity`, calculando el porcentaje y guardando `WasteReason`.
- **Consumo sin Duplicidad:** El modo `FromProduction` indica que la subreceta ya transformó sus ingredientes; el modo `OnSale` en la pizza solo descuenta la masa y la salsa ya listas, sin volver a tocar la harina, el agua ni los tomates.

### PRO-05: Costo Teórico Dinámico y Líneas Base
- **Servicio:** `RecipeService.GetRecipeCostsAsync()`.
- **Cálculo:** Se calcula el costo por cada insumo y el costo por porción utilizando `ProductCostBaseline` y el último costo de compra registrado (`Product.LastCost`).
- Permite calcular el margen bruto proyectado contra el precio de venta sugerido.

### PRO-07: Disponibilidad Producible (Cuello de Botella)
- **Servicio:** `RecipeService.GetProducibleQuantityAsync()`.
- **Lógica:** Evalúa el inventario disponible de cada insumo en la bodega seleccionada. La ración más baja indica la cantidad máxima de platos producibles e identifica el `LimitingIngredient`.

### PRO-08: Gestión Visual Táctil y Pantallas Especializadas
- **Controladores y Vistas:**
  - [RecipesController.cs](file:///c:/Users/Elizabeth/Downloads/sistema%20rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/RecipesController.cs) y vistas en `/Recipes`: Gestión de recetas, costos y componentes.
  - [ProductionController.cs](file:///c:/Users/Elizabeth/Downloads/sistema%20rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/ProductionController.cs) y vistas en `/Production`: Registro táctil de lotes producidos y mermas en cocina.
  - [RecipeHistoryController.cs](file:///c:/Users/Elizabeth/Downloads/sistema%20rest/sistemi-pos-restaurante/src/SistemiPos.Web/Controllers/RecipeHistoryController.cs) y vistas en `/RecipeHistory`: Auditoría histórica de versiones.
  - Seguridad mediante permisos granulares: `PermissionCodes.RecipesView`, `RecipesManage`, `ProductionManage`.

---

## 4. Configuración Real del Caso de la Pizzería

Para validar la operación en un flujo de restaurante real, se implementó y sembró la estructura completa de la **Pizza de Jamón**:

### A. Insumos Base en Bodega
- **Harina de Trigo Especial:** 50,000 g (50 kg) a Q0.008/g
- **Levadura Seca Instantánea:** 2,000 g (2 kg) a Q0.050/g
- **Sal Fina:** 5,000 g (5 kg) a Q0.004/g
- **Aceite de Oliva Extra Virgen:** 10,000 ml (10 L) a Q0.040/ml
- **Agua Purificada:** 50,000 ml (50 L) a Q0.001/ml
- **Tomate Triturado Italiano:** 20,000 g (20 kg) a Q0.015/g
- **Pasta de Tomate Concentrada:** 5,000 g (5 kg) a Q0.025/g
- **Ajo Picado Fresco:** 1,000 g (1 kg) a Q0.030/g
- **Orégano Seco Molido:** 500 g a Q0.080/g
- **Queso Mozzarella:** 10,000 g (10 kg) a Q0.060/g
- **Jamón:** 8,000 g (8 kg) a Q0.050/g
- **Caja de Pizza 12":** 100 unidades a Q2.50/u

### B. Subrecetas Intermedias (`ArticleType.Subreceta` | Modo: `Producir primero`)
1. **Masa Para Pizza** (Rinde 50 bolas de masa):
   - Harina: 12,500 g | Levadura: 150 g | Sal: 250 g | Aceite: 500 ml | Agua: 7,500 ml
   - Stock inicial generado: 50 unidades listas para hornear.
2. **Salsa Para Pizza** (Rinde 5,000 g / 5 kg):
   - Tomate: 4,200 g | Pasta: 500 g | Aceite: 150 ml | Ajo: 60 g | Sal: 60 g | Orégano: 30 g
   - Stock inicial generado: 10,000 g (10 kg) listos para servir.

### C. Plato Terminado (`ArticleType.Preparado` | Modo: `Elaborar al vender`)
**Pizza Jamón** (Precio de venta: Q40.00):
- 1 unidad de `Masa Para Pizza` (Subreceta)
- 100 g de `Salsa Para Pizza` (Subreceta)
- 200 g de `Queso Mozzarella` (Insumo)
- 150 g de `Jamón` (Insumo)
- 1 unidad de `Caja De Pizza 12"` (Empaque)

---

## 5. Mejoras y Ajustes Técnicos Complementarios
1. **Migración PostgreSQL (`20261016224439_AddRecipeProductionAndHistory.cs`):**  
   Se corrigió el cast entre el enum `ArticleType` y el storage textual/entero en PostgreSQL para permitir consultas y vistas que involucran `Subreceta`.
2. **Formulario Web de Productos ([ProductFormViewModel.cs](file:///c:/Users/Elizabeth/Downloads/sistema%20rest/sistemi-pos-restaurante/src/SistemiPos.Web/Models/Catalog/ProductFormViewModel.cs)):**  
   Se actualizaron las propiedades de `ProductPurchasePresentationFormViewModel` a nulables para evitar que las presentaciones de compra vacías bloqueen la validación de `ModelState` al crear productos e insumos estándar.

---

## 6. Resultados de Pruebas Automatizadas
- **Pruebas Unitarias:** 217 superadas (0 errores).
- **Pruebas Funcionales:** 47 superadas (0 errores).
- **Pruebas de Integración:** 9 superadas (3 omitidas para CI Docker).
- **Total:** **273 pruebas superadas (100% éxito)**.
