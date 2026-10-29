# SISTEMI POS Restaurante

Sistema web para restaurantes basado en la plataforma de punto de venta, inventarios, compras, ventas y administración de SISTEMI.

## Documentación de módulos

- [Fase 7: pedidos, preparación y POS](docs/FASE_7_POS_IMPLEMENTACION.md): avance, migración, pruebas, limitaciones y revisión manual del bloque inicial.

- [Recetas: implementación y guía de uso](docs/RECETAS_IMPLEMENTACION_Y_USO.md): alcance, reglas, arquitectura, permisos, migración, pruebas y revisión manual de la entrega.

## Persistencia

- Base de datos: PostgreSQL.
- ORM: Entity Framework Core con Npgsql.
- Producción exige `ConnectionStrings__DefaultConnection` mediante variables de entorno o un proveedor seguro. `appsettings.Development.json` contiene únicamente una cadena local con contraseña de marcador de posición.
- El administrador inicial está desactivado por defecto. Para habilitarlo, configure `Security__BootstrapAdmin__Enabled=true` y `Security__BootstrapAdmin__Password` mediante variables de entorno o User Secrets.
- Las migraciones históricas de SQL Server se conservan en `Persistence/Migrations/SqlServer` y se excluyen de la compilación; la línea activa de PostgreSQL se mantiene separada.
- PostgreSQL compara texto con sensibilidad a mayúsculas por defecto. Las reglas de equivalencia existentes se preservan mediante los valores normalizados por el dominio y ASP.NET Core Identity.
