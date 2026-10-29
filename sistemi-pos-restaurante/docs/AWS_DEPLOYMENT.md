# AWS_DEPLOYMENT.md

## Worker PDF y RabbitMQ opcional

Para habilitar PDF en segundo plano, desplegar la migración `AddQueuedSalePdfReceipts`, configurar `ReceiptQueue` y un broker privado compatible. Usar TLS, secretos del entorno y usuario/vhost dedicado con permisos limitados; no publicar AMQP ni administración a Internet. El compose del repositorio es únicamente para desarrollo local. Mantener los workers activos en al menos una instancia de la aplicación; las réplicas se coordinan mediante bloqueos de Outbox y de trabajo. Supervisar trabajos fallidos y la cola `.failed`. No habilitar este flujo sin un procesador operativo: las solicitudes se conservarían pendientes. Venta y ticket HTML no dependen del broker. Detalles en `COLA_PDF_RABBITMQ.md`.

# Despliegue en AWS

## Objetivo

Desplegar el sistema POS en AWS con separación entre entrada pública, aplicación, base de datos, configuración y almacenamiento persistente.

## Arquitectura objetivo

Flujo principal:

`Usuarios -> Internet -> Route 53 -> Application Load Balancer -> EC2/IIS -> RDS SQL Server Express`

## Región AWS

- Región de producción: `us-east-1`.
- Nombre: US East (N. Virginia).

## Dominio

Configuración aprobada:

- Dominio principal: `sistemi.com.gt`.
- Subdominio del Punto de Venta: `pos.sistemi.com.gt`.
- DNS: Amazon Route 53.
- Hosted Zone: `sistemi.com.gt`.
- El subdominio del POS debe resolverse mediante registro Alias hacia el Application Load Balancer.

## DNS

- Usar Amazon Route 53.
- Hosted Zone: `sistemi.com.gt`.
- Registro del POS: `pos.sistemi.com.gt`.
- Crear registro Alias hacia el Application Load Balancer.

## HTTPS y entrada pública

- Servicio: Application Load Balancer.
- El ALB forma parte de la arquitectura objetivo.
- El ALB recibe tráfico HTTP/HTTPS.
- HTTPS debe utilizar certificado administrado mediante AWS Certificate Manager.
- HTTP debe redirigir a HTTPS.
- Configurar health checks para la aplicación.

## Red

Usar una VPC.

### Subredes públicas

Contienen:

- Application Load Balancer.

### Subredes privadas

Contienen:

- Amazon EC2.
- Amazon RDS for Microsoft SQL Server — Express Edition.

### Reglas

- Internet no accede directamente a EC2.
- Internet no accede directamente a RDS.
- El ALB puede comunicarse con EC2 mediante Security Groups.
- EC2 puede comunicarse con RDS mediante Security Groups.
- RDS no debe ser público.
- RDP no debe exponerse globalmente.
- La administración debe restringirse a mecanismos seguros y autorizados.

## Zonas de disponibilidad

La VPC debe utilizar al menos dos Availability Zones.

- El Application Load Balancer debe utilizar subredes públicas en al menos dos AZ diferentes.
- La capa de aplicación debe disponer de subredes privadas distribuidas en al menos dos AZ para permitir crecimiento futuro sin rediseñar la VPC.
- El DB Subnet Group de RDS debe incluir subredes privadas en al menos dos AZ, aunque la instancia inicial de SQL Server Express sea Single-AZ.

## Salida de Internet de EC2 privada

La instancia EC2 permanece sin entrada directa desde Internet.

La subred privada de aplicación debe disponer de salida controlada mediante NAT Gateway para:

- Actualizaciones del sistema operativo.
- Acceso a servicios AWS cuando no exista un VPC Endpoint específico.
- Operación de agentes y herramientas administrativas que requieran salida HTTPS.

RDS no requiere ni debe disponer de ruta pública a Internet.

## EC2

Configuración inicial aprobada:

- Tipo de instancia: `t3.medium`.
- vCPU: 2.
- Memoria: 4 GiB.
- Disco inicial: 50 GB.
- Tipo de volumen: EBS `gp3`.
- Sistema operativo: Windows Server.
- Servidor web: IIS.
- Aplicación: ASP.NET Core MVC.
- Ubicación: subred privada.


## Backups de EC2

Política aprobada:

- Snapshot automático diario del volumen EBS.
- Retención: 7 días.
- Crear snapshot manual antes de cambios importantes de infraestructura, sistema operativo, IIS o aplicación cuando exista riesgo operativo.


## Acceso administrativo a EC2

Estrategia aprobada:

- Usar AWS Systems Manager Session Manager como mecanismo principal de administración.
- No exponer RDP globalmente a Internet.
- Si se requiere RDP, habilitarlo únicamente de forma temporal y restringida a una IP autorizada.
- Registrar y controlar los accesos administrativos.
## RDS SQL Server Express

Configuración inicial aprobada:

- Motor: Microsoft SQL Server 2022 Express.
- Clase de instancia: `db.t3.small`.
- Almacenamiento inicial: 50 GB.
- Tipo de almacenamiento: `gp3`.
- Retención de backups automáticos: 7 días.
- Alta disponibilidad: Single-AZ.
- Ubicación: subred privada.
- Public access: deshabilitado.
- Acceso únicamente desde la aplicación y administración autorizada.



## Límite de capacidad de SQL Server Express

SQL Server 2022 Express admite como máximo 10 GB por base de datos relacional.

- Monitorear crecimiento de la base de datos.
- No confundir los 50 GB de almacenamiento RDS con el límite lógico de 10 GB por base de datos de Express.
- Antes de aproximarse al límite, planificar migración a una edición de SQL Server con mayor capacidad.

## Archivos persistentes

No usar el disco local de EC2 como almacenamiento definitivo para archivos que deban conservarse.

Usar Amazon S3 cuando el sistema necesite almacenar de forma durable:

- Logos.
- Comprobantes exportados.
- Reportes generados.
- Adjuntos.
- Archivos de importación/exportación.
- Otros archivos de aplicación.

No almacenar secretos en S3.

## Configuración y secretos

No versionar:

- Passwords.
- Connection strings de producción.
- Access keys.
- Secret keys.
- Tokens.

Usar:

- Variables de entorno.
- AWS Secrets Manager.
- AWS Systems Manager Parameter Store.

Preferir IAM Roles para acceso desde EC2 a servicios AWS.

No usar access keys permanentes dentro de la aplicación cuando un IAM Role pueda resolver el acceso.

## Observabilidad

Usar Amazon CloudWatch para:

- Logs de aplicación.
- Métricas de infraestructura.
- Alertas operativas.
- Estado de EC2.
- Estado de RDS.

No escribir datos sensibles completos en logs.


## Base de datos y migraciones

- Entity Framework Core es el ORM.
- Las migraciones deben permanecer versionadas en el repositorio.
- Aplicar migraciones de producción de forma controlada.
- No depender de creación automática destructiva de esquema.
- No ejecutar operaciones destructivas sin aprobación explícita.

## Estado de aplicación

La aplicación no debe depender de almacenamiento local de EC2 para estado durable.

No asumir que:

- La misma instancia EC2 existirá permanentemente.
- Los archivos temporales son persistentes.
- Una sesión en memoria sobrevivirá a reinicios o futuros escenarios con múltiples instancias.

## Seguridad

- Aplicar mínimo privilegio en IAM.
- Usar Security Groups.
- Mantener EC2 en subred privada.
- Mantener RDS en subred privada.
- Mantener RDS sin acceso público.
- Forzar HTTPS en producción.
- No almacenar credenciales en el repositorio.
- Mantener secretos separados por ambiente.

## Ambientes

### Development

Puede utilizar recursos locales o AWS según necesidad.

### Production

Debe utilizar la arquitectura AWS definida en este documento.

### Staging

No obligatorio en el MVP.

## CI/CD

Plataforma aprobada:

- GitHub Actions.

El pipeline debe contemplar como mínimo:

- Restaurar dependencias.
- Compilar la solución.
- Ejecutar pruebas unitarias.
- Ejecutar pruebas de integración cuando corresponda.
- Ejecutar pruebas funcionales cuando corresponda.
- Generar artefacto de publicación.
- Desplegar a producción mediante un flujo controlado.

No desplegar a producción si fallan las verificaciones obligatorias.

## Infrastructure as Code

Herramienta aprobada:

- Terraform.

La infraestructura AWS debe poder definirse y reproducirse mediante código.

Terraform debe cubrir, cuando corresponda:

- VPC.
- Subredes públicas y privadas.
- Security Groups.
- Application Load Balancer.
- EC2.
- RDS.
- Route 53.
- IAM.
- CloudWatch.
- Recursos de apoyo necesarios para el despliegue.

No realizar cambios manuales permanentes en producción que no queden reflejados posteriormente en Terraform.

## Servicios AWS fuera del alcance inicial

No introducir sin necesidad aprobada:

- ECS.
- EKS.
- Lambda para la aplicación principal.
- DynamoDB.
- ElastiCache.
- OpenSearch.
- API Gateway.
- CloudFront.
- WAF.
- Auto Scaling.
