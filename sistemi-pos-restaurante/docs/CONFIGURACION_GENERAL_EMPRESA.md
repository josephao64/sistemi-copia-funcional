# SISTEMI POS Restaurante — Configuración General de Empresa
## Informe Técnico y Documentación de Arquitectura

**Repositorio:** `SISTEMI-GT/sistemi-pos-restaurante`  
**Rama activa:** `development-joseph`  
**Fecha:** 29 de septiembre de 2026  
**Unidades tratadas:** `ORG-09A` (Base de configuración) y `ORG-09B` (Excepciones y resolución jerárquica)  

---

## 1. Contexto y Objetivos del Negocio

El sistema operaba previamente con una configuración plana en `SystemSetting`, la cual almacenaba de forma global los parámetros de la empresa, moneda, impuestos y facturación electrónica FEL.

Con la incorporación del modelo multitenant empresarial (Grupo Empresarial -> Empresa -> Sucursales -> Bodegas), se requería implementar la **Configuración General de Empresa**:
1. Cada **Empresa (`Company`)** posee su propia configuración de identidad, fiscal, moneda y credenciales FEL.
2. Cada **Sucursal (`Branch`)** puede definir **excepciones locales** (ej. pie de comprobante, nombre y código de establecimiento ante SAT, y activación selectiva de métodos de pago).
3. **Resolución Jerárquica Centralizada:** Las ventas, reportes y comprobantes consultan la configuración efectiva resolviendo:  
   $$\text{Valor Efectivo} = \text{Excepción Sucursal} \;\;??\;\; \text{Valor Empresa} \;\;??\;\; \text{Fallback Sistema}$$
4. **Trazabilidad del Origen:** Cada parámetro informa explícitamente si su valor fue definido a nivel de `Company`, `Branch` o `System`.
5. **Aislamiento Multitenant Estricto:** Se valida que ninguna sucursal acceda o herede parámetros de una empresa distinta (`DomainRuleException`).

---

## 2. Clasificación de Parámetros por Nivel

| Nivel | Parámetros | Regla de Negocio e Invariante |
| :--- | :--- | :--- |
| **Empresa (`CompanySetting`)** | Razón Social, Nombre Comercial, NIT, Dirección Fiscal, Teléfono, Correo, Logo, Moneda principal, Símbolo, Decimales, Separadores, Tasa de Impuesto (IVA), Límite de descuentos, Credenciales FEL (INFILE: API Keys, usuario y firmas). | Parámetros legales, fiscales y monetarios institucionales ante SAT y entidades reguladoras. Rigen a todas las sucursales de la empresa por defecto. |
| **Sucursal (`BranchSetting`)** | Nombre Comercial de Establecimiento, Código de Establecimiento SAT, Pie de recibo (`ReceiptFooter`), Métodos de pago permitidos (`Cash`, `Transfer`, `Pos`, `Credit`). | Ajustes operativos locales. Permite que sucursales express o quioscos restrinjan crédito o transferencias, o tengan un mensaje de pie de recibo particular. |
| **Sistema (`SystemSetting`)** | Registro global de respaldo para retrocompatibilidad. | Utilizado como salvaguarda en caso de consultas legacy sin empresa asignada. |

---

## 3. Modelo de Dominio y Datos

### 3.1. Entidades Creadas

1. **`CompanySetting`** (`src/SistemiPos.Domain/Organizations/CompanySetting.cs`):
   - Relación 1:1 con `Company` mediante llave foránea única `CompanyId`.
   - Propiedades de negocio, configuración de moneda (`CurrencyCode`, `CurrencySymbol`, etc.), facturación electrónica FEL protegida y métodos de pago base.
   - Métodos de dominio: `Update(...)`, `SelectCurrency(...)`, `ConfigurePaymentMethods(...)`.

2. **`BranchSetting`** (`src/SistemiPos.Domain/Organizations/BranchSetting.cs`):
   - Relación 1:1 con `Branch` mediante llave foránea única `BranchId`.
   - Propiedades de excepción: `EstablishmentCode`, `EstablishmentName`, `ReceiptFooter`, `OverridePaymentMethods`, `CashPaymentsEnabled`, `TransferPaymentsEnabled`, `PosPaymentsEnabled`, `CreditPaymentsEnabled`.
   - Métodos de dominio: `Update(...)`, `ConfigurePaymentMethods(...)`.

3. **`SettingOrigin`** (`src/SistemiPos.Domain/Organizations/SettingOrigin.cs`):
   - `System = 1`
   - `Company = 2`
   - `Branch = 3`

### 3.2. Mapeos de Base de Datos y Migraciones

Se aplicaron las migraciones correspondientes en PostgreSQL con Entity Framework Core:
- **`20260930023930_AddCompanySettingsTable`**:
  - Tabla: `CompanySettings`
  - Restricción: Clave foránea `FK_CompanySettings_Companies_CompanyId` con borrado restringido (`Restrict`).
  - Índice único: `IX_CompanySettings_CompanyId`.
- **`20260930025313_AddBranchSettingsTable`**:
  - Tabla: `BranchSettings`
  - Restricción: Clave foránea `FK_BranchSettings_Branches_BranchId` con borrado restringido (`Restrict`).
  - Índice único: `IX_BranchSettings_BranchId`.

---

## 4. Capa de Aplicación y Servicio de Resolución

### 4.1. DTOs de Resolución Jerárquica

En `src/SistemiPos.Application/Organizations/EnterpriseSettingsDtos.cs`:
```csharp
public sealed record EffectiveValue<T>(T Value, SettingOrigin Origin)
{
    public static implicit operator T(EffectiveValue<T> effective) => effective.Value;
}
```
`EffectiveEnterpriseSettingsDto` expone cada propiedad encapsulada en `EffectiveValue<T>`, permitiendo conocer simultáneamente el valor resuelto y la procedencia (`Origin`).

### 4.2. Servicio `EnterpriseSettingsResolver`

Implementa `IEnterpriseSettingsResolver`:
- **`ResolveEffectiveSettingsAsync(Guid companyId, Guid? branchId)`**:
  1. Valida existencia de la Empresa.
  2. Si se proporciona `branchId`, verifica que la Sucursal exista y que `branch.CompanyId == companyId`. De lo contrario, lanza `DomainRuleException`.
  3. Carga `CompanySetting`. Si no existe, recurre al fallback de `SystemSetting`.
  4. Carga `BranchSetting`. Si la sucursal tiene excepciones activas (ej. `OverridePaymentMethods == true` o pie de recibo no nulo), prevalece el valor local con origen `SettingOrigin.Branch`. Los demás campos heredan el valor institucional con origen `SettingOrigin.Company`.
- **`SaveBranchSettingAsync(Guid companyId, Guid branchId, SaveBranchSettingRequest request)`**:
  - Guarda o actualiza de manera transaccional las excepciones locales de la sucursal, validando pertenencia organizacional.

---

## 5. Resumen de Pruebas y Validación

La suite completa de pruebas unitarias y de integración fue ejecutada y verificada:
- **`CompanySettingTests`**: Valida invariantes del dominio, separadores de moneda, porcentajes no negativos y asignación de datos.
- **`BranchSettingTests`**: Valida constructores, inicialización de banderas y actualización de excepciones.
- **`EnterpriseSettingsResolverTests`**:
  - Herencia completa desde la empresa cuando no hay excepciones.
  - Sobreescritura local y asignación del origen `Branch` cuando existen excepciones.
  - Aislamiento entre sucursales (los cambios en Sucursal A no afectan a Sucursal B).
  - Rechazo de cruce inválido entre empresas (`branch.CompanyId != companyId`).
  - Persistencia idempotente de excepciones.

**Resultado:** 100% aprobado (140 UnitTests, 44 FunctionalTests, 7 IntegrationTests).

---

## 6. Siguientes Pasos

1. **`ORG-09C` (Pantalla de Administración y Seguridad UI):**
   - Adaptar `SettingsController` y las vistas Razor para seleccionar la Empresa y la Sucursal del usuario en sesión.
   - Mostrar badges visuales de procedencia en cada campo (`[Empresa]` o `[Sucursal]`).
   - Aplicar permisos de backend `SettingsView` y `SettingsManage` y protección antiforgery.
2. **`ORG-09D` (Auditoría y Endurecimiento):**
   - Asegurar que `AuditSaveChangesInterceptor` redacte/enmascare credenciales y secretos (API keys) en `ChangesJson`.
   - Transaccionalidad atómica entre configuración y bitácora de auditoría.

---

## 7. Criterios de Aceptación y Verificación Puntual (Checklist de Requerimientos)

A continuación se detalla cómo el sistema cumple de manera demostrable y verificable cada uno de los criterios del requerimiento:

### ✅ 1. El sistema manejará una empresa con múltiples sucursales.
* **Modelo de Datos:** La entidad `Company` (`src/SistemiPos.Domain/Organizations/Company.cs`) mantiene la relación de uno a muchos `IReadOnlyCollection<Branch> Branches`. Cada sucursal (`Branch.cs`) tiene la clave foránea obligatoria `CompanyId` hacia su empresa padre.
* **Base de Datos (PostgreSQL):** La tabla `Branches` tiene la restricción de clave foránea `FK_Branches_Companies_CompanyId` con política `OnDelete: Restrict` e índice de búsqueda.
* **Aislamiento Multitenant:** En la capa de aplicación (`EnterpriseSettingsResolver.cs`), el backend valida estrictamente que la sucursal pertenezca a la empresa (`if (branch.CompanyId != companyId) throw new DomainRuleException(...)`). No se permite la mezcla o consulta cruzada de sucursales entre empresas distintas.
* **Operatividad:** El módulo organizacional (`OrganizationService.cs` y `OrganizationController.cs`) permite dar de alta múltiples sucursales para una misma empresa, asignarles bodegas independientes y usuarios autorizados.

### ✅ 2. Los parámetros generales funcionan como valores por defecto cuando no exista una configuración específica por sucursal.
* **Plantilla Institucional:** La entidad `CompanySetting` define los parámetros generales por defecto (Razón Social, NIT, Moneda, Impuestos IVA, Pie de recibo institucional, Métodos de pago globales y Credenciales FEL).
* **Excepciones de Sucursal:** La entidad `BranchSetting` modela exclusivamente las excepciones permitidas a nivel local (código y nombre de establecimiento SAT, pie de recibo específico y métodos de pago permitidos en esa sede).
* **Servicio Central de Resolución:** `EnterpriseSettingsResolver` ejecuta la jerarquía:
  $$\text{Valor Efectivo} = \text{Excepción Sucursal} \;\;??\;\; \text{Valor Empresa} \;\;??\;\; \text{Fallback Sistema}$$
* **Trazabilidad de Origen:** Cada valor retornado es un `EffectiveValue<T>(T Value, SettingOrigin Origin)`. Si la sucursal no tiene excepción, el sistema retorna el valor de la empresa con `Origin = SettingOrigin.Company`. Si la sucursal sobreescribe el parámetro, se retorna el valor local con `Origin = SettingOrigin.Branch`.
* **Evidencia de Pruebas:** Verificado y aprobado mediante pruebas unitarias en `EnterpriseSettingsResolverTests.cs` (`Resolve_BranchWithoutOverride_InheritsCompanySettings` y `Resolve_BranchWithOverride_ReturnsBranchValuesAndBranchOrigin`).

### ✅ 3. Los cambios de configuración sensibles deben quedar auditados.
* **Trazabilidad en Entidades:** Tanto `CompanySetting` como `BranchSetting` heredan de `AuditableEntity`, asegurando la captura obligatoria de:
  * `CreatedAtUtc` y `CreatedBy` (momento UTC y usuario creador).
  * `ModifiedAtUtc` y `ModifiedBy` (momento UTC y usuario que realizó la modificación).
* **Bitácora Transaccional:** `AuditSaveChangesInterceptor` intercepta automáticamente cualquier cambio en `CompanySettings` y `BranchSettings`, registrándolo en la tabla `AuditLogs` dentro de la **misma transacción atómica** de la base de datos (si la auditoría falla, el cambio de configuración no se aplica).
* **Protección de Secretos:** Las credenciales FEL (`InfileApiKeyProtected`, etc.) se almacenan cifradas mediante `IDataProtectionProvider`. En la unidad ORG-09D se asegura adicionalmente que `ChangesJson` redacte/enmascare cualquier clave o secreto para que no se almacenen en texto claro dentro de la bitácora de auditoría ni en logs del servidor.

