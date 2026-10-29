# Lineamiento de diseño — SISTEMI POS ERP

Fecha: 23 de octubre de 2026. Estado: propuesta de diseño para revisión; no aplicada al código.

## 1. Dirección visual

Interfaz clara, compacta y operativa. Conservar la identidad azul marino y verde del sistema. Usar superficies claras para lectura prolongada, jerarquía visible y acciones con nombres concretos. La misma identidad debe acompañar a todos los perfiles; la distribución cambia según la tarea.

Este documento complementa `SISTEMI_POS_Reglas_UI_Botones_Columnas_Filtros.md`. Las reglas de permisos, caja, ventas y procesos siguen en los documentos funcionales. Una propuesta visual no habilita funciones ni modifica autorizaciones.

## 2. Paleta principal

| Uso | Color | Aplicación |
|---|---|---|
| Marca principal | `#12263A` | Navegación, encabezados y estructura |
| Variante profunda | `#0B1928` | Fondos de navegación oscuros |
| Verde de marca actual | `#58B814` | Acentos de marca, indicadores y elementos decorativos |
| Verde de acción propuesto | `#347A0A` | Botón principal con texto blanco: Cobrar, Guardar o Confirmar según la pantalla |
| Verde vivo | `#7CE33B` | Acento puntual con texto azul marino; evitar grandes superficies |
| Fondo general | `#F4F7FA` | Área de trabajo |
| Superficie | `#FFFFFF` | Formularios, paneles y tablas |
| Superficie secundaria | `#F7F9FB` | Cabeceras de tabla y agrupaciones |
| Texto principal | `#102A43` | Títulos, importes y datos principales |
| Texto secundario | `#526579` | Etiquetas, ayudas y metadatos legibles |
| Borde suave | `#E2E8EE` | Separadores de tablas y paneles |
| Borde de control propuesto | `#8090A0` | Límites visibles de campos sobre blanco |

El cambio propuesto es separar el verde de marca del verde usado detrás de texto blanco. El verde actual `#58B814` con blanco tiene contraste calculado de 2.54:1; `#347A0A` con blanco tiene 5.34:1. Conservar el primero como identidad y usar el segundo en acciones mejora la lectura. El texto secundario `#526579` sobre blanco tiene 6.01:1. Reservar `#8090A0` para bordes o elementos no esenciales; sobre blanco tiene 3.27:1 y no conviene para texto pequeño.

Estas relaciones se calcularon para colores sólidos; transparencias, imágenes y estados hover requieren considerar su fondo real. No constituyen una certificación de accesibilidad del sistema.

## 3. Colores de estado

Propuesta de pares para etiquetas y mensajes:

| Estado | Texto | Fondo | Uso |
|---|---|---|---|
| Correcto / completado | `#24672C` | `#E6F4E5` | Operación registrada y estados completados |
| Pendiente / atención | `#875A08` | `#FFF2D6` | Pendientes o advertencias |
| En preparación | `#9A4815` | `#FFF0E5` | Trabajo en curso en cocina |
| Información | `#205DA0` | `#E6F0FF` | Avisos y estados informativos |
| Error / anulado | `#A12626` | `#FDECEC` | Fallos y anulaciones |
| Neutro | `#526579` | `#EDF1F5` | Borradores y estados sin alerta |

Usar siempre texto de estado; el color por sí solo no comunica el significado. Los nombres deben corresponder a estados reales del módulo. Para acciones destructivas, usar `#C93636` con texto blanco y la confirmación que corresponda. El verde se usa para avanzar y confirmar; el rojo identifica consecuencias destructivas o errores.

## 4. Tipografía y números

- Familia: Inter; respaldo Segoe UI y fuentes del sistema. No incorporar una dependencia nueva únicamente por la tipografía.
- Texto operativo: 16 px. Tablas administrativas: 14 px como base.
- Ayudas y metadatos: 12–13 px; no colocar información crítica en ese tamaño.
- Título principal: 24–28 px. Títulos de sección: 18–20 px.
- Total de venta: 30–36 px. Datos prioritarios de cocina: 24–32 px, ajustados a la distancia real de lectura.
- Pesos: 400 para lectura, 500–600 para etiquetas y 700 para importes principales.
- Interlineado: aproximadamente 1.4–1.5 en texto; evitar párrafos completos en mayúsculas.
- Importes y cantidades alineados a la derecha, con números tabulares. Respetar moneda, símbolo y decimales configurados; no fijar `Q` ni dos decimales en los componentes compartidos.

## 5. Espaciado, medidas y superficies

- Escala común: 4, 8, 12, 16, 24 y 32 px.
- Separación habitual entre campos: 12–16 px; entre secciones: 24 px.
- Formularios y administración: controles de 40–44 px de alto.
- Operación táctil de ventas, meseros y cocina: objetivos de interacción de al menos 44 × 44 px; acciones principales de 48–56 px de alto.
- Bordes redondeados: 8 px en controles y 12 px en paneles. Evitar mezclar radios distintos sin una razón funcional.
- Sombras suaves únicamente para separar capas, menús y modales.
- Una sola cabecera principal por pantalla. Mantener los filtros y el contenido cerca del título.
- Evitar tarjetas que repitan el título, bloques vacíos y gráficos decorativos sin utilidad operativa.

## 6. Botones, campos y mensajes

- Acción principal: verde oscuro, texto blanco y verbo concreto. Una acción principal por grupo de trabajo.
- Acción secundaria: fondo blanco, borde visible y texto azul marino.
- Navegación: mantener `← Volver a [pantalla anterior]` cerca del título. El breadcrumb conserva su función informativa.
- Íconos: una sola familia de trazo, tamaño habitual 18–20 px. Acompañar las acciones importantes con texto.
- Foco de teclado: contorno sólido y visible con separación del control; no depender únicamente de una sombra tenue.
- Campos: etiqueta visible, formato esperado y validación junto al dato. El placeholder no sustituye la etiqueta.
- Errores: conservar los datos capturados y explicar cómo corregirlos.
- Guardado: mostrar progreso, impedir envíos duplicados y confirmar el resultado real del servidor.
- Toast para confirmaciones generales; validación junto al campo; banner para desconexión o problemas de pantalla; diálogo para decisiones sensibles según el proceso.
- Crear y editar registros relacionados mediante los modales previstos, conservando el contexto del proceso.
- Distinguir estados vacío, cargando, error y sin permisos. No representar todos como una tabla vacía.

## 7. Distribución por perfil y tarea

### Ventas / Punto de Venta

En escritorio, catálogo o buscador a la izquierda y pedido a la derecha; proporción orientativa 60/40. Total y acción de cobro visibles sin cubrir productos. En pantallas menores, apilar las áreas y mantener accesible el resumen.

Mostrar producto, cantidad, precio, descuento permitido y subtotal. La captura debe facilitar lector de código y teclado. Mostrar sucursal, usuario y contexto de sesión sin convertir la cabecera en un panel administrativo.

El acceso de creación de ventas corresponde al perfil Punto de Venta conforme a la regla vigente. El diseño no debe extenderlo a Cajero ni a otros perfiles por su nombre. Los productos y pedidos disponibles deben corresponder al flujo realmente implementado; no simular integración entre módulos.

### Caja y arqueos

La caja corresponde al usuario autenticado: no agregar un selector para escoger otra caja. Separar visualmente apertura, movimientos, arqueo y cierre.

En arqueo, billetes a la izquierda y monedas a la derecha. Mostrar cantidades y subtotales legibles. El efectivo esperado y las diferencias se presentan según la configuración y las reglas del conteo ciego, sin anticipar información restringida.

En administración, distinguir consulta, revisión y conciliación. Una caja cerrada no se presenta como editable. No mostrar una acción de editar arqueo o reconteo hasta que esa operación esté implementada y autorizada. La interfaz debe describir con precisión lo que hace cada acción disponible.

### Cocina

Priorizar número de pedido, origen o mesa, hora, tiempo transcurrido, productos, cantidades y observaciones. Organizar según los estados reales del flujo y la estación asignada. Usar controles grandes y etiquetas explícitas para acciones por producto y por pedido. No mezclar ambos alcances.

Mantener colores de estado constantes y acompañarlos con texto. Evitar animación continua y parpadeos. Los indicadores de demora deben usar umbrales definidos por la operación, no valores inventados por diseño.

### Meseros

Priorizar área, mesa, pedido y envío a cocina. Controles táctiles, selección de cantidades clara y observaciones cerca del producto. Mantener visible el estado real de envío y conservar el pedido cuando ocurra un error.

### Administración y gerencia

Tablas compactas, filtros arriba, acciones consistentes y detalle de auditoría fuera de las columnas operativas cuando no sea necesario. Mantener el contexto de sucursal visible y las restricciones vigentes. Mostrar los indicadores que apoyen decisiones reales; evitar llenar la pantalla de tarjetas.

### Bodega

Priorizar código, producto, unidad, ubicación cuando exista y cantidades. Diferenciar captura, guardado y finalización del conteo. Mantener las restricciones vigentes sobre costos y acciones de inventario.

## 8. Tablas y filtros

- Texto a la izquierda; números e importes a la derecha; fechas con formato uniforme.
- Acciones con nombres consistentes: Ver, Editar y Eliminar únicamente cuando correspondan.
- Cabecera de tabla con fondo secundario; separadores suaves y altura suficiente para leer cada fila.
- Evitar columnas técnicas y observaciones extensas en el listado principal.
- Aplicar filtros automáticamente donde el patrón del módulo lo permita y conservar Limpiar.
- En historiales con rango definido, conservar el período desde el primer día del mes hasta hoy.
- Mantener filtros activos al consultar y regresar cuando el flujo lo permita. Las exportaciones deben respetar el contexto definido para cada módulo.

## 9. Adaptación y operación

En escritorio se prioriza la operación simultánea; en tableta se amplían objetivos táctiles; en móvil se apilan áreas y se evita ocultar la acción principal. Las tablas que requieran desplazamiento horizontal deben conservar legibles sus datos y acciones.

Mostrar conexión, sincronización y pendientes solamente cuando apliquen al flujo. Una venta local pendiente no se comunica como registrada definitivamente. Los demás módulos requieren conexión según las reglas actuales.

Ocultar acciones no autorizadas y mantener la autorización en servidor. No usar solamente un color o un botón deshabilitado para explicar un bloqueo operativo; mostrar el motivo cuando corresponda.

## 10. Aplicación sugerida

1. Centralizar los colores y medidas compartidos, separando marca, acción y estados.
2. Unificar botones, campos, etiquetas de estado, mensajes y foco de teclado.
3. Aplicar la distribución operativa de ventas y caja.
4. Homologar cocina, meseros y administración con los mismos componentes.
5. Revisar manualmente lectura, teclado, pantalla táctil y contenido visible por perfil antes de dar el diseño por aplicado.

Las medidas nuevas y el verde de acción son propuestas. Los colores principales, la tipografía base y la escala de espaciado proceden de los estilos actuales. No se modificaron vistas, CSS, permisos ni lógica de negocio al preparar este documento.

## Referencias locales revisadas

- `src/SistemiPos.Web/wwwroot/css/site.css`
- `src/SistemiPos.Web/wwwroot/css/control.css`
- `src/SistemiPos.Web/wwwroot/css/restaurant.css`
- `docs/SISTEMI_POS_Reglas_UI_Botones_Columnas_Filtros.md`

Validación realizada: lectura de referencias de diseño y cálculo de contraste de combinaciones seleccionadas. No se ejecutaron pruebas ni se hizo una validación visual de la aplicación.
