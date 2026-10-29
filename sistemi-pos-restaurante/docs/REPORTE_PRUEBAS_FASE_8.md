# Reporte de pruebas de la fase 8

Fecha: 21 de octubre de 2026. Zona: America/Guatemala.

## Resultado

44 pruebas aprobadas, 0 fallidas y 0 omitidas: 21 unitarias y 23 de componentes/modelo. Compilación de los proyectos de prueba y dependencias correcta, sin errores ni advertencias.

Las pruebas de componentes usan el modelo PostgreSQL sin abrir conexiones. No certifican el funcionamiento de consultas completas sobre una base real, despliegue, interfaz, certificador ni recuperación del servidor.

## Distribución

| Grupo | Casos aprobados |
|---|---:|
| NitLookupServiceTests | 3 |
| HistoricalProfitTests | 4 |
| RestaurantOrderTests | 12 |
| FiscalSubmissionTests | 2 |
| PhaseEightControlTests | 21 |
| ExcelExportServiceTests | 1 |
| PhaseEightModelTests | 1 |

## Cobertura del plan

| Clave | Área | Resultado y límite |
|---|---|---|
| CTL-01 | FEL por empresa | Expediente y bloqueos fiscales probados. Emisión real pendiente de contrato y adaptador. |
| CTL-02 | Consulta NIT | Fallback local y captura manual probados. Servicio externo pendiente de contrato. |
| CTL-03 | Auditoría contextual | Contexto, ocultación de credenciales y bloqueo de borrado comprobados. |
| CTL-04 | Ventas y cobros | Permisos y alcance negativo probados. Consultas con ventas reales pendientes. |
| CTL-05 | Inventario y diferencias | Modelo y precisión histórica comprobados. Operación de Kardex, conteos y merma pendiente con datos reales. |
| CTL-06 | Compras y proveedores | Reportes implementados. Recepción parcial, costos y facturas requieren aceptación funcional. |
| CTL-07 | Rentabilidad histórica | Cuatro casos de cálculo aprobados; conserva costo ante reembolso sin retorno físico. |
| CTL-08 | Centro de control | Controles de acceso probados. Indicadores, navegación y resultados reales requieren revisión funcional. |
| CTL-09 | Excel y PDF | Excel tipado y PDF paginado comprobados. Exportación completa desde la consulta requiere aceptación funcional. |
| CTL-10 | Respaldo y recuperación | Rechazo de configuración insegura y límites probados. Respaldo real y simulacro aún pendientes. |

## Evidencia por caso

| Grupo | Caso | Resultado |
|---|---|---|
| ExcelExportServiceTests | SistemiPos.IntegrationTests.Infrastructure.ExcelExportServiceTests.CreateWorkbook_Generates_Valid_OpenXml_With_Typed_Values | Aprobada |
| FiscalSubmissionTests | SistemiPos.UnitTests.Sales.FiscalSubmissionTests.An_uncertain_external_result_blocks_a_blind_resubmission | Aprobada |
| FiscalSubmissionTests | SistemiPos.UnitTests.Sales.FiscalSubmissionTests.Stable_key_is_scoped_to_company_and_sale | Aprobada |
| HistoricalProfitTests | SistemiPos.UnitTests.Reports.HistoricalProfitTests.Currency_is_never_used_to_recalculate_a_historical_cost | Aprobada |
| HistoricalProfitTests | SistemiPos.UnitTests.Reports.HistoricalProfitTests.Duplicate_products_share_restoration_without_restoring_more_than_their_cost | Aprobada |
| HistoricalProfitTests | SistemiPos.UnitTests.Reports.HistoricalProfitTests.Physical_retail_return_reduces_historical_cost_proportionally | Aprobada |
| HistoricalProfitTests | SistemiPos.UnitTests.Reports.HistoricalProfitTests.Prepared_food_refund_preserves_the_cost_of_consumed_ingredients | Aprobada |
| NitLookupServiceTests | SistemiPos.UnitTests.Customers.NitLookupServiceTests.LookupAsync_Allows_Manual_Entry_When_Provider_Has_No_Credentials | Aprobada |
| NitLookupServiceTests | SistemiPos.UnitTests.Customers.NitLookupServiceTests.LookupAsync_Returns_Local_Customer_Before_Configured_Provider | Aprobada |
| NitLookupServiceTests | SistemiPos.UnitTests.Customers.NitLookupServiceTests.LookupAsync_Uses_Configured_Provider_When_Customer_Does_Not_Exist | Aprobada |
| PhaseEightControlTests | SistemiPos.IntegrationTests.Infrastructure.PhaseEightControlTests.A_missing_active_branch_does_not_grant_global_access | Aprobada |
| PhaseEightControlTests | SistemiPos.IntegrationTests.Infrastructure.PhaseEightControlTests.An_assigned_other_branch_still_requires_consolidation_permission | Aprobada |
| PhaseEightControlTests | SistemiPos.IntegrationTests.Infrastructure.PhaseEightControlTests.An_unassigned_branch_is_rejected_even_with_consolidation_permission | Aprobada |
| PhaseEightControlTests | SistemiPos.IntegrationTests.Infrastructure.PhaseEightControlTests.An_unassigned_company_is_rejected | Aprobada |
| PhaseEightControlTests | SistemiPos.IntegrationTests.Infrastructure.PhaseEightControlTests.An_unconfigured_provider_cannot_invent_a_certification | Aprobada |
| PhaseEightControlTests | SistemiPos.IntegrationTests.Infrastructure.PhaseEightControlTests.Audit_history_cannot_be_deleted | Aprobada |
| PhaseEightControlTests | SistemiPos.IntegrationTests.Infrastructure.PhaseEightControlTests.Audit_records_context_and_redacts_new_credentials | Aprobada |
| PhaseEightControlTests | SistemiPos.IntegrationTests.Infrastructure.PhaseEightControlTests.Audit_requires_its_own_permission | Aprobada |
| PhaseEightControlTests | SistemiPos.IntegrationTests.Infrastructure.PhaseEightControlTests.Backup_intervals_and_retention_are_bounded_without_running_tools | Aprobada |
| PhaseEightControlTests | SistemiPos.IntegrationTests.Infrastructure.PhaseEightControlTests.Empty_pdf_has_an_explicit_no_records_message | Aprobada |
| PhaseEightControlTests | SistemiPos.IntegrationTests.Infrastructure.PhaseEightControlTests.Exports_require_export_permission_even_when_viewing_is_allowed(format: "excel") | Aprobada |
| PhaseEightControlTests | SistemiPos.IntegrationTests.Infrastructure.PhaseEightControlTests.Exports_require_export_permission_even_when_viewing_is_allowed(format: "pdf") | Aprobada |
| PhaseEightControlTests | SistemiPos.IntegrationTests.Infrastructure.PhaseEightControlTests.Fiscal_management_requires_permission_before_loading_sales | Aprobada |
| PhaseEightControlTests | SistemiPos.IntegrationTests.Infrastructure.PhaseEightControlTests.Fiscal_payload_cannot_be_rewritten | Aprobada |
| PhaseEightControlTests | SistemiPos.IntegrationTests.Infrastructure.PhaseEightControlTests.Invalid_date_ranges_are_rejected_before_querying(from: "2024-01-01", to: "2026-10-21") | Aprobada |
| PhaseEightControlTests | SistemiPos.IntegrationTests.Infrastructure.PhaseEightControlTests.Invalid_date_ranges_are_rejected_before_querying(from: "2026-10-21", to: "2026-10-20") | Aprobada |
| PhaseEightControlTests | SistemiPos.IntegrationTests.Infrastructure.PhaseEightControlTests.Missing_or_public_backup_configuration_cannot_start_a_backup(publicFolder: False) | Aprobada |
| PhaseEightControlTests | SistemiPos.IntegrationTests.Infrastructure.PhaseEightControlTests.Missing_or_public_backup_configuration_cannot_start_a_backup(publicFolder: True) | Aprobada |
| PhaseEightControlTests | SistemiPos.IntegrationTests.Infrastructure.PhaseEightControlTests.Pdf_has_valid_offsets_escaped_text_and_repeated_headers | Aprobada |
| PhaseEightControlTests | SistemiPos.IntegrationTests.Infrastructure.PhaseEightControlTests.Reports_require_view_permission_before_querying_data | Aprobada |
| PhaseEightControlTests | SistemiPos.IntegrationTests.Infrastructure.PhaseEightControlTests.Schema_matches_migration_and_preserves_six_decimal_costs | Aprobada |
| PhaseEightModelTests | SistemiPos.IntegrationTests.Infrastructure.PhaseEightModelTests.Fiscal_submission_is_unique_per_sale_and_contextual_audit_is_indexed | Aprobada |
| RestaurantOrderTests | SistemiPos.UnitTests.Orders.RestaurantOrderTests.Cancellation_Preserves_Consumed_Quantities_And_Requires_Reason | Aprobada |
| RestaurantOrderTests | SistemiPos.UnitTests.Orders.RestaurantOrderTests.Confirmed_Order_Rejects_Replacement_Of_Historical_Lines | Aprobada |
| RestaurantOrderTests | SistemiPos.UnitTests.Orders.RestaurantOrderTests.Inventory_Stock_Movement_And_Lot_Preserve_Six_Decimals | Aprobada |
| RestaurantOrderTests | SistemiPos.UnitTests.Orders.RestaurantOrderTests.Only_Prepared_Quantities_Can_Be_Sold_And_Cannot_Be_Sold_Or_Cancelled_Again | Aprobada |
| RestaurantOrderTests | SistemiPos.UnitTests.Orders.RestaurantOrderTests.Partial_Checkouts_Keep_The_Exact_Original_Gross_Amount | Aprobada |
| RestaurantOrderTests | SistemiPos.UnitTests.Orders.RestaurantOrderTests.Preparation_Requires_Confirmation_And_Supports_Partial_Quantities | Aprobada |
| RestaurantOrderTests | SistemiPos.UnitTests.Orders.RestaurantOrderTests.Published_Recipe_Uses_Its_Pinned_Subrecipe_Version | Aprobada |
| RestaurantOrderTests | SistemiPos.UnitTests.Orders.RestaurantOrderTests.Recipe_Consumption_Expands_Yields_And_Aggregates_Shared_Ingredients | Aprobada |
| RestaurantOrderTests | SistemiPos.UnitTests.Orders.RestaurantOrderTests.Replayed_Event_Does_Not_Advance_Or_Duplicate_And_Different_Payload_Is_Rejected | Aprobada |
| RestaurantOrderTests | SistemiPos.UnitTests.Orders.RestaurantOrderTests.Reventa_Dispatches_Without_Preparation_And_Cannot_Dispatch_Twice | Aprobada |
| RestaurantOrderTests | SistemiPos.UnitTests.Orders.RestaurantOrderTests.Revision_Preserves_The_Original_Line_And_Is_Forbidden_After_Preparation_Starts | Aprobada |
| RestaurantOrderTests | SistemiPos.UnitTests.Orders.RestaurantOrderTests.Terminal_Rejects_Warehouse_From_Another_Branch | Aprobada |

## Ejecución reproducible

```powershell
dotnet build tests/SistemiPos.IntegrationTests/SistemiPos.IntegrationTests.csproj --no-restore -c Release -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test tests/SistemiPos.UnitTests/SistemiPos.UnitTests.csproj --no-build --no-restore -c Release --filter "FullyQualifiedName~HistoricalProfitTests|FullyQualifiedName~FiscalSubmissionTests|FullyQualifiedName~NitLookupServiceTests|FullyQualifiedName~Orders.RestaurantOrderTests" --logger "trx;LogFileName=phase8-unit.trx" --results-directory artifacts/phase8-tests
dotnet test tests/SistemiPos.IntegrationTests/SistemiPos.IntegrationTests.csproj --no-build --no-restore -c Release --filter "FullyQualifiedName~PhaseEightControlTests|FullyQualifiedName~PhaseEightModelTests|FullyQualifiedName~ExcelExportServiceTests" --logger "trx;LogFileName=phase8-components.trx" --results-directory artifacts/phase8-tests
```

El entorno restringido impedía la comunicación local de testhost (socket 10013). Las dos ejecuciones exitosas se realizaron fuera de esa restricción. No hubo fallos de casos en las ejecuciones finales. No se cambió código productivo en esta ronda de pruebas.

## Pendientes para cierre operativo

- Aplicar la migración `20261021054815_PhaseEightControl` en un entorno preparado y con respaldo. No se aplicó durante esta validación.
- Validar consultas y exportaciones con datos reales de ventas, pagos mixtos, monedas, devoluciones, compras e inventario.
- Revisar interfaz y navegación manualmente; el proyecto reserva esas pruebas al usuario.
- Contratar e implementar el adaptador FEL/NIT, homologar y resolver consulta de estado, rechazos y conciliación antes de producción.
- Configurar pg_dump y carpeta privada; realizar respaldo, copia externa y simulacro aislado con objetivos RPO/RTO acordados.
- Los históricos sin costo o ubicación documentados no se reconstruyen artificialmente.

## Archivos de esta ronda

- Nuevo: `tests/SistemiPos.IntegrationTests/Infrastructure/PhaseEightControlTests.cs`.
- Evidencia: `artifacts/phase8-tests/phase8-unit.trx`, `phase8-components.trx` y `phase8-results.json`.
- Reporte: `output/pdf/Reporte_Fase_8_SISTEMI_POS.pdf`.
- Manual de implementación: `docs/FASE_8_CONTROL_REPORTES_Y_RESPALDOS.md`.

## Conclusión

Los 44 casos focalizados aprobaron y la compilación técnica es correcta. La fase dispone de implementación y evidencia de controles/cálculos; el cierre integral requiere aceptación funcional con base real, contratación fiscal y recuperación demostrada. No corresponde declararla certificada para producción únicamente con estas pruebas.
