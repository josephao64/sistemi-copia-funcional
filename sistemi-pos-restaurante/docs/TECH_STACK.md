# TECH_STACK.md

## Mensajería de comprobantes

RabbitMQ se incorpora para trabajos PDF de ventas, con cliente .NET `RabbitMQ.Client` 7.2.2, Outbox PostgreSQL y workers `BackgroundService` en Infrastructure. Es opcional y se activa con `ReceiptQueue:Enabled`. El entorno local usa `compose.receipts.yml` y credenciales fuera de Git. No se añaden servicios bancarios ni se convierte el cobro en una operación eventual. Véase `COLA_PDF_RABBITMQ.md`.

# Stack tecnológico

## Backend

- Lenguaje: C#.
- Runtime: .NET 10 LTS.
- Framework: ASP.NET Core 10.
- Tipo de aplicación: Web.
- Patrón de presentación: MVC.


## Frontend

- Razor Views / MVC.
- HTML5.
- CSS.
- JavaScript.
- Tailwind CSS 4.3.
- PWA con Web App Manifest, Service Worker e IndexedDB para el alcance offline exclusivo del Punto de Venta.

No introducir un framework SPA como React, Angular o Vue sin aprobación.

## Base de datos

- Motor: PostgreSQL (versión del servidor no fijada en el repositorio).
- Acceso a datos: Entity Framework Core 10.
- Migraciones: Entity Framework Core 10 Migrations.

Usar tipos `decimal` adecuados para valores monetarios.

## Arquitectura

Separación lógica:

- Domain.
- Application.
- Infrastructure.
- Web / Presentation.

La estructura física del proyecto debe reflejar estos límites sin introducir complejidad innecesaria.


## Estructura del repositorio

```text
src/
  SistemiPos.Web/
  SistemiPos.Application/
  SistemiPos.Domain/
  SistemiPos.Infrastructure/

tests/
  SistemiPos.UnitTests/
  SistemiPos.IntegrationTests/
  SistemiPos.FunctionalTests/
```

Los proyectos de pruebas deberán formar parte del repositorio Git y conservarse durante todo el ciclo de desarrollo.

No deberán eliminarse ni agregarse al `.gitignore`.

### Responsabilidades

- `SistemiPos.Web` — presentación, MVC, Razor, PWA, Service Worker y endpoints.
- `SistemiPos.Application` — casos de uso y coordinación.
- `SistemiPos.Domain` — entidades y reglas de negocio.
- `SistemiPos.Infrastructure` — persistencia, EF Core, servicios externos y adaptadores.
- `SistemiPos.UnitTests` — pruebas unitarias.
- `SistemiPos.IntegrationTests` — pruebas de integración.
- `SistemiPos.FunctionalTests` — pruebas funcionales de flujos completos.


## Autenticación y autorización

- ASP.NET Core Identity o mecanismo equivalente nativo de ASP.NET Core.
- Autorización mediante roles y permisos.
- Validación de permisos en backend.

No implementar autenticación externa mientras no exista requerimiento aprobado.

## Interfaz

Objetivo:

- Uso desde desktop.
- Uso desde laptop.
- Uso desde tablet.
- Uso desde teléfono.
- Uso desde puesto de caja.
- Compatible con lector de código de barras que funcione como entrada de teclado.
- Diseño responsive.
- Priorizar velocidad operativa sobre animaciones o efectos visuales.

## Persistencia

- PostgreSQL como fuente de verdad.
- Entity Framework Core 10 para persistencia.
- Migraciones versionadas dentro del repositorio.
- Transacciones para operaciones críticas.

## Logging

Usar el sistema de logging de ASP.NET Core.

No almacenar:

- Contraseñas.
- Tokens.
- Datos sensibles completos.
- Secretos.

en logs.

## Configuración

Usar:

- `appsettings.json`
- `appsettings.{Environment}.json`
- Variables de entorno para secretos o configuraciones sensibles.

No versionar credenciales reales.

## Configuración de moneda

La aplicación debe manejar una moneda operativa configurable desde administración.

Configurar como mínimo:

- Código de moneda (por ejemplo `GTQ`, `USD`).
- Nombre.
- Símbolo.
- Cantidad de decimales.
- Separador decimal.
- Separador de miles.
- Posición del símbolo: antes o después del importe.

El formato monetario no debe quedar hardcodeado en vistas, servicios ni reglas de negocio.

El cambio de configuración monetaria no debe recalcular transacciones históricas.

El MVP maneja una moneda operativa activa; esto no implica funcionalidad multi-moneda.

## Pruebas

Framework:

- xUnit.

Prioridad de pruebas:

- Reglas de negocio.
- Confirmación de venta.
- Inventario.
- Compras.
- Caja.
- Descuentos.
- Anulaciones.
- Devoluciones.
- Permisos críticos.
- Operación offline e idempotencia de sincronización.

## Control de versiones

- Git.
- Repositorio GitHub.
- CI/CD: GitHub Actions.
- Infrastructure as Code: Terraform.

No realizar commits de:

- Credenciales.
- Archivos generados.
- Binarios.
- Configuración local sensible.

## Desarrollo asistido

Herramientas permitidas:

- Codex.
- GitHub.
- Visual Studio o Visual Studio Code.

Codex debe respetar los documentos del repositorio y el stack definido aquí.

## Plataforma de despliegue

- AWS.

La topología, red y servicios de infraestructura se definen en `AWS_DEPLOYMENT.md`.

## Entornos

Definir como mínimo:

- Development.
- Production.

Agregar Staging únicamente cuando exista necesidad operativa.

## Dependencias

Preferir:

1. Funcionalidad nativa de .NET.
2. Paquetes oficiales de Microsoft.
3. Paquetes mantenidos y justificados.

No agregar paquetes para resolver funcionalidades simples ya disponibles en el framework.

## Convenciones generales

- Código y nombres técnicos en inglés.
- Texto visible al usuario en español.
- Fechas y moneda deben respetar la configuración regional del negocio.
- Zona horaria operativa inicial: Guatemala; configurable por implementación.
- Moneda operativa configurable.
- Valor inicial por defecto: quetzal (`GTQ`), sin quedar fijado en código.

## Versiones cerradas

- .NET 10 LTS.
- ASP.NET Core 10.
- Entity Framework Core 10.
- PostgreSQL (versión del servidor no fijada en el repositorio).
- Tailwind CSS 4.3.
