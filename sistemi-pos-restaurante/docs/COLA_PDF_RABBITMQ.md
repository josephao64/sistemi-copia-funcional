# PDF de ventas en segundo plano con RabbitMQ

Fecha: 27 de octubre de 2026.

## Alcance implementado

Se encola únicamente la preparación del PDF solicitado desde «PDF» en una venta ya registrada. La autorización de tarjeta/transferencia, la confirmación de venta, stock y caja siguen siendo síncronos. Los tickets HTML y el comprobante imprimible A4 continúan disponibles inmediatamente y no esperan al broker.

El flujo anterior no generaba el PDF dentro de la confirmación de venta. Por tanto, esta entrega desacopla la descarga PDF y permite medir los pasos; no afirma que RabbitMQ acelere por sí mismo el registro de la venta. No se agregaron correo, facturación FEL, pasarela bancaria externa ni impresión física por colas.

## Persistencia y entrega

1. `GET /Sales/Pdf/{id}` verifica la venta y presenta la preparación del documento.
2. El navegador envía `POST /Sales/RequestPdf` con antiforgery y una clave UUID. El backend valida permiso `sales.view`, usuario y sucursal.
3. `SalePdfJobs` y `SalePdfOutbox` se guardan juntos en una transacción PostgreSQL. La solicitud no conecta con RabbitMQ. La unicidad y un bloqueo transaccional acotan los reintentos a usuario, sucursal, venta y clave. La clave queda en la URL para conservarla al recargar o reintentar.
4. El publicador toma lotes de diez eventos mediante `FOR UPDATE SKIP LOCKED`, publica mensajes persistentes con `mandatory` y publisher confirms y registra la publicación únicamente después de la confirmación. Las conexiones son persistentes, con reconexión del servicio ante fallos.
5. El consumidor usa confirmación manual y prefetch de uno. Bloquea el trabajo durante la generación, verifica la sucursal de origen y guarda el PDF antes de confirmar el mensaje. Un trabajo completado no se vuelve a generar al recibir una copia. El mensaje contiene solo el ID del trabajo.
6. `GET /Sales/PdfStatus?jobId=...` informa el estado sin cargar el archivo binario. `GET /Sales/PdfDownload?jobId=...` vuelve a comprobar autorización, sucursal y propietario antes de devolver el PDF guardado.

El artefacto se genera usando el formato PDF existente y los datos de la venta disponibles al procesar la solicitud. Una solicitud nueva puede reflejar cambios posteriores de configuración; una solicitud completada conserva su archivo. No se elimina el histórico ni se reutilizan las credenciales del usuario en el consumidor.

## Fallos y reintentos

Si el broker no está disponible, el evento permanece en PostgreSQL y el publicador vuelve a intentar. Si el proceso cae después de publicar y antes de guardar la confirmación, puede enviarlo de nuevo; el bloqueo e idempotencia del procesador evitan otro archivo para el mismo trabajo.

Una generación fallida se revierte antes de guardar su intento fallido en otra transacción. Se programa un reintento de 2, 4, 8... segundos, hasta 60 segundos por espera, con cinco intentos por defecto. El máximo es técnico y configurable. Si no se puede persistir el fallo, el mensaje no se confirma como completado y vuelve a la cola. Las solicitudes no desaparecen de la base.

Los trabajos agotados se marcan `Failed`; su mensaje y los mensajes con formato inválido se envían a `sistemi.receipts.pdf.failed`. RabbitMQ también puede enviar allí mensajes que superen su límite de entregas. Esa cola requiere supervisión y recuperación operativa; no se purga automáticamente ni se promete recuperación automática de cualquier mensaje ya apartado por el broker. La interfaz permite una solicitud nueva con otra clave después de un fallo registrado.

La consulta del navegador ocurre cada dos segundos y se detiene después de 45 segundos para evitar consultas indefinidas. Esto no cancela el trabajo: se informa que sigue pendiente y se permite consultar de nuevo o abrir el ticket. La generación y su descarga no registran otra venta ni otro cobro.

## Configuración local

Dependencia: `RabbitMQ.Client` 7.2.2, declarada en `Directory.Packages.props`. Configuración `ReceiptQueue` en `appsettings.json`; está deshabilitada por defecto para no exigir un broker en entornos que todavía no lo tienen.

```powershell
docker compose -f compose.receipts.yml up -d
```

El archivo `.local/receipt-queue.env` contiene `RABBITMQ_DEFAULT_USER` y `RABBITMQ_DEFAULT_PASS`. No está versionado. En este equipo se generó una contraseña aleatoria y se configuró la aplicación mediante User Secrets. No copiar esos secretos al repositorio ni a un despliegue.

El compose utiliza `rabbitmq:4-management`, volumen persistente y puertos vinculados solo a localhost: AMQP `127.0.0.1:5678` y administración `127.0.0.1:15678`. La aplicación local configura `ReceiptQueue:Enabled=true`, `HostName=127.0.0.1` y `Port=5678`. El puerto AMQP interno del contenedor es 5672. No ejecutar `down -v` para reiniciar: eliminaría los datos del broker.

Parámetros disponibles: `Enabled`, `HostName`, `Port`, `VirtualHost`, `UserName`, `Password`, `UseTls`, `QueueName`, `MaximumAttempts` y `PollMilliseconds`. Los secretos deben venir de User Secrets o del almacén de secretos del entorno. En producción se requiere broker privado, TLS, usuario/vhost dedicados y permisos limitados. La imagen de producción debe fijarse a una versión/digest revisado, en lugar del tag mayor de desarrollo.

Migración: `20261027062922_AddQueuedSalePdfReceipts`. Agrega únicamente las tablas e índices de trabajos y Outbox, con FK restrictivas. No revertirla eliminando artefactos que deban conservarse. La aplicación mantiene su procedimiento existente de migrar al arrancar; revisar y respaldar el entorno antes de desplegar.

## Medición

Las respuestas de ventas y POS incluyen `Server-Timing`:

- `action`: ejecución del controlador y sus servicios, antes de renderizar la vista.
- `app`: procesamiento de la petición hasta comenzar a enviar las cabeceras; incluye trabajo adicional del pipeline y renderizado cuando corresponde.

El servidor registra duración y estado HTTP de cada ruta del flujo. El procesador registra por separado cuánto tarda cada trabajo PDF. No se registran cuerpos de formulario, contraseñas, documentos binarios ni datos bancarios en esta instrumentación.

Para diagnosticar la espera, el usuario puede comparar en las herramientas de red del navegador `/pos/caja/estado`, el POST de venta, `/Sales/Print` o `/Sales/Ticket`, y los endpoints PDF. El tiempo de cola es distinto de la latencia del cobro; una caída del broker no debe hacer que el cajero espere para registrar ventas.

## Verificación y revisión manual

`QueuedSalePdfTests` usa bases PostgreSQL temporales para comprobar solicitud/Outbox atómicos, repetición de clave, separación por usuario/sucursal, reintentos y límites. La prueba de RabbitMQ usa una cola de nombre aleatorio para generar un PDF real, verificar mensajes inválidos en la cola de fallos y procesar una copia sin duplicar el artefacto. Borra únicamente sus colas y base temporales.

Resultado de la entrega: compilación correcta, 4 pruebas de integración aprobadas (incluye PostgreSQL y RabbitMQ reales y correspondencia del modelo/migración), 45 pruebas unitarias de ventas/caja aprobadas y JavaScript válido. Las pruebas de cola no generaron documentos de ventas del negocio. El servicio local aplica la migración de trabajos al arrancar mediante el mecanismo existente.

Para ejecutarla, configurar `SISTEMI_TEST_CONNECTION` y `SISTEMI_TEST_RABBIT_PASSWORD` (broker local en 5678, usuario `sistemi_receipts`) y usar el filtro `FullyQualifiedName~QueuedSalePdfTests`. La migración/modelo se verifica con `RecipeModelTests`. No se prueban interfaces visuales ni navegación automática.

Revisión manual pendiente: cobrar y abrir el ticket; solicitar PDF, recargar durante la espera, consultar/descargar, verificar el aislamiento de otro usuario/sucursal y probar con el broker detenido/reanudado conservando su volumen. Comparar `Server-Timing` antes de atribuir al broker mejoras de latencia del cobro.

Referencias técnicas: [API .NET de RabbitMQ](https://www.rabbitmq.com/client-libraries/dotnet-api-guide), [publisher confirms](https://www.rabbitmq.com/tutorials/tutorial-seven-dotnet), [fiabilidad](https://www.rabbitmq.com/docs/reliability) y [Transactional Outbox](https://docs.aws.amazon.com/prescriptive-guidance/latest/cloud-design-patterns/transactional-outbox.html).
