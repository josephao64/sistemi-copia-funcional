# Fase 8 — Control, reportes y respaldos

## Alcance y secuencia

Implementación sobre las fases existentes de organización, catálogo, inventario, compras, producción, restaurante, ventas y caja. Las consultas reutilizan sus documentos; abrir o exportar un reporte no registra ventas, no consume existencias y no revierte movimientos.

La fase tiene dos dependencias operativas explícitas: contratar el certificador/consulta NIT y configurar el respaldo en el servidor. El usuario confirmó que todavía no tiene contrato. La emisión FEL real y un simulacro de recuperación exitoso no se declaran terminados por disponer de pantallas o código.

## Cobertura del plan

| Clave | Implementación | Condición de cierre operativo |
|---|---|---|
| CTL-01 | Expediente fiscal por venta, empresa y sucursal; contenido conservado, clave estable, concurrencia y estados de certificación/conciliación. Solo una respuesta externa completa puede producir una certificación. | Pendiente contrato, adaptador específico y homologación del proveedor. El conector instalado permanece deshabilitado. |
| CTL-02 | Selección de consulta NIT por empresa, conservación del flujo local y captura manual cuando no hay proveedor disponible. | La consulta externa requiere contrato y adaptador. El ingreso manual funciona sin él. |
| CTL-03 | Auditoría con grupo, empresa, sucursal, bodega, terminal cuando está disponible, usuario, documento y cambios; protección de credenciales nuevas e historial no editable. | Revisar casos operativos con usuarios reales. Los registros antiguos sin ubicación no se reasignan artificialmente. |
| CTL-04 | Ventas por empresa/sucursal autorizadas, fechas, usuario, bodega, producto y código de caja. Cobros desglosados por medio de pago y moneda; reembolsos vinculados separados. | Validación funcional con datos del negocio. |
| CTL-05 | Existencias físicas/reservadas/disponibles, Kardex con traslados y ajustes, diferencias de conteo, mermas de restaurante y rendimiento teórico/real de producción. | Comparar con una toma física y un lote reales. |
| CTL-06 | Compras, órdenes pendientes de recepción y costos de entrada por producto/proveedor; evita contar una solicitud como compra recibida. | Validar recepción parcial, factura y anulación. |
| CTL-07 | Rentabilidad por línea con valores históricos, devolución económica separada de retorno físico y consumo del pedido capturado para ventas futuras. | Históricos incompletos se muestran sin costo/margen conocido; no hay reconstrucción con precios actuales. |
| CTL-08 | Centro de control con alcance visible, indicadores, alertas enlazadas a consultas y seguimiento separado de Restaurante/POS. Acceso por permisos. | Validación de navegación por rol y sucursal. |
| CTL-09 | Excel y PDF con la misma consulta autorizada y datos de la página visible, hasta 100 filas. Metadatos de alcance/filtros. | Descargar y comparar una página filtrada. No se presenta como exportación de toda la base. |
| CTL-10 | Respaldo PostgreSQL manual/programado, retención tras respaldo correcto, archivo temporal, SHA256, manifiesto y conteos bajo el mismo snapshot; procedimiento de simulacro aislado y reporte de tiempo de recuperación. | Configurar herramientas/carpeta privada, objetivos RPO/RTO y ejecutar el simulacro. No se ha restaurado una base desde esta implementación. |

## Reglas de datos

- Sin permiso `reports.consolidate`, el centro de control restringe resultados a la sucursal activa. Con permiso, solo consolida sucursales asignadas al usuario. La empresa seleccionada y la bodega se validan nuevamente en el servicio.
- Los reportes anteriores de inventario, compras y caja también aplican la sucursal activa. Una ubicación ausente no concede alcance global.
- Los documentos antiguos sin ubicación permanecen fuera de las consultas restringidas; su regularización exige revisión, no un relleno automático.
- Las ventas se ubican por su sesión de caja. Los totales mantienen monedas separadas; no existe conversión contable implícita.
- El rango comprende días completos en Guatemala, de la fecha inicial inclusive al día siguiente de la final exclusive. Máximo: 366 días de diferencia. Los saldos de inventario y las colas pendientes son actuales, como indica cada reporte.
- Un pago mixto se obtiene de sus partes reales. El total original y el reembolso se muestran separados; no se etiqueta como efectivo recibido un importe que se pagó con tarjeta.
- La rentabilidad usa base imponible histórica menos la proporción de reembolsos. El costo solo disminuye por devolución física registrada. Cuando varias líneas contienen el mismo producto, el retorno físico se reparte por cantidad, porque el histórico de devolución identifica producto y no línea original.
- El consumo de nuevos pedidos POS guarda costo unitario en el movimiento y costo acumulado en la línea. La venta conserva su proporción al cobrar. Un empaque agregado después del cobro permanece en Kardex y no reescribe la venta anterior. Un costo cero de catálogo o un preparado antiguo sin consumo documentado exige revisión.
- Margen bruto no equivale a utilidad neta: no incorpora nómina, alquiler ni otros gastos que no pertenezcan al documento de venta.
- Las auditorías nuevas protegen propiedades de contraseñas, tokens y credenciales. Esto no sanea automáticamente secretos que pudieran haberse guardado antes del cambio.
- Un documento fiscal preparado sin contrato no es una factura certificada. Nunca se generan UUID, series ni números de autorización ficticios. Una respuesta incierta bloquea reintentos hasta conciliar con el proveedor; esa conciliación depende del futuro adaptador contratado.

## Manual de usuario

### Consultar información

1. Inicia sesión y selecciona tu sucursal de trabajo.
2. Abre **Control por ubicación** en el menú de reportes (`/control/reportes`).
3. Escoge Ventas, Compras, Órdenes pendientes, Costos de compra, Existencias, Kardex, Rentabilidad, Mermas, Producción, Diferencias de conteo o Seguimiento POS. Auditoría aparece con su permiso específico.
4. Selecciona empresa/sucursal y fechas. El texto **Alcance aplicado** muestra las ubicaciones efectivas.
5. Busca un producto o proveedor y selecciónalo en las sugerencias. No basta escribir un nombre sin seleccionar el resultado. Usa **Limpiar** para eliminar filtros anteriores.
6. Aplica solo los filtros pertinentes: caja y usuario para ventas; proveedor para compras; bodega/producto para inventario; usuario para movimientos, compras, conteos y auditoría. Las colas pendientes no dependen del rango histórico.
7. Pulsa **Consultar**, revisa notas e indicadores y abre **Ver** para consultar el documento. El acceso al documento conserva sus propios permisos y sucursal activa; al consolidar puede ser necesario cambiar de sucursal antes de abrirlo.
8. Usa **Anterior/Siguiente** para recorrer páginas. Las alertas conservan el alcance de ubicación y abren el reporte correspondiente.

Ejemplo: para revisar ventas de una caja, selecciona Ventas y cobros, el día, la sucursal y su código de caja. Compara el total original, pagos por medio/moneda y reembolsos vinculados; una devolución posterior puede aparecer vinculada a la venta del día consultado.

Ejemplo: en Diferencias de conteo, teórico 20 y contado 18 produce diferencia -2. El ajuste aplicado se muestra por separado porque pudieron existir movimientos mientras se realizó el conteo. No sumes diferencias de unidades incompatibles.

### Descargar

Con permiso de exportación, usa **Excel de esta página** o **PDF de esta página**. Cada descarga conserva hasta 100 filas de la página seleccionada. Para descargar otra página, navega primero a ella. Los textos de auditoría extensos pueden abreviarse en PDF; el detalle completo permanece en Excel y en pantalla.

### Preparar un expediente fiscal

1. Abre **Facturación por empresa** (`/control/fiscal`). La lista muestra las últimas 200 ventas dentro de tus sucursales autorizadas.
2. Revisa la venta, empresa y receptor antes de prepararla. Una venta anulada o devuelta requiere resolver su tratamiento fiscal.
3. Pulsa **Preparar**. Sin contrato/conector, queda **Pendiente de configuración** y no se envía a ningún certificador.
4. Cuando exista contrato, se debe implementar y homologar el adaptador y definir consulta de estado, rechazo, anulación/notas y conciliación según el proveedor. No actives emisión productiva solo por completar credenciales.

### Permisos

| Permiso | Función |
|---|---|
| `reports.view` | Consultar el centro de control. |
| `reports.consolidate` | Consultar conjuntamente sucursales autorizadas. |
| `reports.export` | Descargar la página consultada. También requiere consulta. |
| `audit.operations.view` | Ver auditoría operativa dentro del alcance autorizado. |
| `fiscal.manage` | Gestionar expedientes fiscales de sucursales asignadas. |
| `backups.manage` | Consultar/crear respaldos completos del sistema. Es un permiso administrativo global. |

El superadministrador recibe los códigos mediante el mecanismo existente de inicialización. Asigna explícitamente los permisos necesarios a otros roles; no se conceden consolidación, auditoría, exportación ni respaldo a todos los usuarios.

## Instalación y recuperación

1. Desplegar el código y la migración de fase 8 en una ventana de mantenimiento, con respaldo previo. El arranque de la aplicación utiliza su mecanismo existente de migraciones. No se modificó una base en ejecución durante este trabajo.
2. Usar PostgreSQL y herramientas `pg_dump`, `pg_restore` y `psql` compatibles con el servidor. La ruta de ejemplo en `phase8-backups.example.json` es ilustrativa: se debe sustituir por la instalada.
3. Configurar la sección `Backups` desde variables de entorno o configuración privada. Ejemplo de claves: `Backups__Enabled`, `Backups__Directory`, `Backups__PgDumpPath`, `Backups__IntervalHours`, `Backups__RetentionDays`, `Backups__RpoMinutes`, `Backups__RtoMinutes`.
4. Usar una carpeta absoluta privada, fuera de `wwwroot`, con acceso exclusivo a la cuenta del servicio y administradores. Resguardar por separado las claves de Data Protection, archivos subidos y configuración/secretos necesarios para reconstruir el servidor.
5. Mantener `Enabled=false` hasta verificar herramientas, capacidad de disco, permisos y respaldo manual. La ejecución tiene un límite de 60 minutos. El worker revisa cada cinco minutos y genera un respaldo al superar el intervalo; el proceso del servidor debe estar activo.
6. La retención elimina únicamente archivos propios completados y vencidos después de producir un respaldo nuevo correcto. No reemplaza una copia externa cifrada e independiente. En despliegues con varias instancias, habilitar un único worker o programar el respaldo desde infraestructura para evitar concurrencia entre procesos.
7. Definir RPO y RTO con el negocio. No se asignaron valores como si ya estuvieran acordados. El RPO observado depende de antigüedad del último respaldo válido; el intervalo debe permitir cumplirlo, considerando el ciclo de revisión. SHA256 solo verifica integridad al generar, no capacidad de recuperar.

### Simulacro aislado

El script **no se ejecuta automáticamente**, no crea ni borra bases y exige PowerShell 7. Preparar una base vacía e independiente cuyo nombre comience por `sistemi_restore_`. Configurar `PGHOST`, `PGPORT`, `PGUSER`, `PGPASSFILE` o `PGPASSWORD` y SSL en el entorno del operador. No colocar contraseñas en argumentos, commits ni reportes.

```powershell
pwsh -File .\scripts\Restore-BackupDrill.ps1 `
  -Archive 'D:\SistemiPrivate\Backups\sistemi-FECHA.dump' `
  -TargetDatabase 'sistemi_restore_simulacro' `
  -PgRestorePath 'C:\ruta-postgresql\pg_restore.exe' `
  -PsqlPath 'C:\ruta-postgresql\psql.exe' `
  -ReportPath 'D:\SistemiPrivate\simulacro.json' `
  -RtoMinutes 60
```

El valor 60 del ejemplo no establece un SLA. El script comprueba SHA256, exige destino vacío, restaura transaccionalmente y compara los conteos de ventas, compras, movimientos y sesiones de caja con el snapshot del archivo. El JSON registra duración y cumplimiento del objetivo suministrado. Además, revisar acceso a la aplicación aislada, últimos documentos y consistencia de stock/caja; el tiempo de restauración de datos no incluye por sí solo reconstruir toda la infraestructura. Conservar evidencia y repetir según la política operativa acordada.

## Archivos principales

- Aplicación: `Reports/ControlReports.cs`, `Reports/HistoricalProfit.cs`, `Reports/IBackupService.cs`, `Sales/IFiscalService.cs`.
- Infraestructura: `Reports/ControlReportService.cs`, `Reports/TablePdfWriter.cs`, `Reports/PostgresBackupService.cs`, `Sales/FiscalService.cs`, interceptor de auditoría y repositorio de reportes existentes.
- Dominio/persistencia: `FiscalSubmission`, auditoría contextual, costos históricos adicionales en líneas de pedido/venta y sus configuraciones EF.
- Web: controladores/vistas `ControlReports`, `Fiscal`, `Backups`; navegación y recursos `control.css`/`control.js`.
- Operación: `scripts/Restore-BackupDrill.ps1` y `docs/phase8-backups.example.json`.

## Aceptación manual pendiente

Probar usuarios sin/con consolidación, intento de consultar una bodega ajena, cambio de sucursal, venta mixta, devolución parcial sin retorno físico, devolución con retorno físico, preparado nuevo frente a histórico incompleto, recepción parcial, toma con movimientos concurrentes, exportación paginada y auditoría sin credenciales visibles. Revisar interfaz/navegación con el usuario. Habilitar los respaldos y ejecutar el simulacro solo en el entorno preparado para ello. La certificación y consulta externa NIT quedan pendientes del contrato.


## Validación técnica de la entrega inicial

- Compilación de la solución en Release: correcta, cero errores y cero advertencias. El intento inicial de usar una configuración de solución personalizada fue rechazado por MSBuild antes de compilar; se utilizó la configuración Release existente.
- Migración generada: `20261021054815_PhaseEightControl`. Agrega campos contextuales de auditoría, expediente fiscal con claves únicas/concurrencia y campos de costo histórico nullable. No elimina datos ni recalcula históricos en Up. No se aplicó a ninguna base.
- JavaScript: comprobación de sintaxis de `control.js` correcta. PowerShell: análisis sintáctico de `Restore-BackupDrill.ps1` correcto; no se ejecutó una restauración.
- Pruebas focalizadas solicitadas al ejecutor: rentabilidad histórica, estados fiscales, NIT, transiciones de pedidos y modelo EF. Ambas ejecuciones se abortaron porque `vstest.console` no pudo conectar con `testhost` en 90 segundos. No se consideran aprobadas y no hubo resultados de casos individuales.
- Sin pruebas visuales, navegación manual, certificación externa, ejecución de respaldo o recuperación. No se inició la aplicación para evitar aplicar migraciones automáticamente a una base en uso.

Antes del despliegue, repetir las pruebas focalizadas en un entorno donde funcione `testhost`, revisar consultas sobre una base de preparación, aplicar la migración con respaldo y realizar la aceptación manual descrita arriba. La compilación no demuestra por sí sola traducción/ejecución de todas las consultas sobre datos reales.


## Actualización de pruebas del 21 de octubre de 2026

Se resolvió el bloqueo del ejecutor: el entorno restringido impedía el socket local de testhost (error 10013). Al ejecutar las comprobaciones focalizadas fuera de esa restricción, aprobaron **44 casos: 21 unitarios y 23 de componentes/modelo, cero fallidos y cero omitidos**. La nueva compilación de las pruebas y sus dependencias terminó sin errores ni advertencias.

La cobertura incluye rentabilidad, NIT, estados fiscales, regresión de transiciones de pedidos, rechazos de autorización/alcance, protección de credenciales de auditoría, bloqueo de borrado de auditoría y cambio de contenido fiscal, estructura de PDF/Excel, configuración segura de respaldos y coherencia del modelo/migración. No se conectó una base de datos, no se aplicó la migración y no se ejecutó un respaldo o restauración real. La aceptación manual, consultas sobre datos reales y las dependencias fiscales/operativas continúan pendientes.

Evidencia detallada: [REPORTE_PRUEBAS_FASE_8.md](REPORTE_PRUEBAS_FASE_8.md). Informe para compartir: [Reporte PDF de la fase 8](../output/pdf/Reporte_Fase_8_SISTEMI_POS.pdf).
