# AGENTS.md

## Scope

Sistema de Punto de Venta (POS) reutilizable como modelo base.

Este archivo aplica a todo el repositorio.

## Project docs

- `docs/Política de pruebas y consumo de tokens.md` — política de validación, pruebas y optimización del consumo de tokens durante el desarrollo.
- `docs/REQUIREMENTS.md` — requerimientos funcionales y no funcionales.
- `docs/BUSINESS_RULES.md` — reglas e invariantes del negocio.
- `docs/ARCHITECTURE.md` — arquitectura lógica, módulos y relaciones.
- `docs/TECH_STACK.md` — tecnologías, herramientas y estructura del repositorio.
- `docs/AWS_DEPLOYMENT.md` — arquitectura y reglas de despliegue en AWS.
- `docs/DEVELOPMENT_PLAN.md` — secuencia de implementación.
- `docs/SISTEMI_POS_Reglas_UI_Botones_Columnas_Filtros.md` — lineamientos de interfaz, botones, columnas, filtros, modales, navegación y comportamiento visual del sistema.




No resolver contradicciones por inferencia.

No convertir decisiones pendientes en reglas de negocio.

## Constraints

- Implementar únicamente el alcance aprobado.
- No agregar funcionalidades futuras por anticipación.
- El sistema requiere conexión al servidor y a la base de datos para registrar operaciones, excepto ventas offline exclusivamente en el POS, autorizadas para la fase 7. Estas se conservan como pendientes locales y se validan y registran definitivamente al sincronizar; los demás módulos requieren conexión.
- Respetar el stack, la arquitectura y la estructura del repositorio documentados.
- Mantener reglas de negocio fuera de controladores, vistas y componentes de UI.
- Validar autorización y reglas críticas en backend.
- Mantener integridad transaccional en operaciones críticas.
- No eliminar históricos definidos como persistentes.
- Usar precisión decimal para dinero.
- No introducir dependencias sin necesidad técnica.
- Los proyectos de pruebas definidos en `docs/TECH_STACK.md` deben conservarse en Git y no agregarse al `.gitignore`.
- Actualizar la documentación cuando cambien reglas, arquitectura, stack o alcance.


## Verification

Usar los comandos de build, test, lint y migraciones definidos por el repositorio.

Ejecutar las verificaciones relevantes después de modificar código.
