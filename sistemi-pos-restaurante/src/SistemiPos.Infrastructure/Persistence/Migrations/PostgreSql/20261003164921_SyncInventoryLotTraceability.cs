using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations.PostgreSql;

/// <summary>
/// Reconciles legacy balances after the lot schema is available. Schema ownership
/// remains in AddInventoryExpiryLotsAndMovementLots so both development histories
/// can converge without creating the same columns and tables twice.
/// </summary>
public partial class SyncInventoryLotTraceability : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "InventoryExpiryLots"
            SET "LotNumber" = CASE
                    WHEN trim("LotNumber") = '' THEN 'SALDO-INICIAL-' || replace("Id"::text, '-', '')
                    ELSE "LotNumber"
                END,
                "ReceivedQuantity" = COALESCE("ReceivedQuantity", "AvailableQuantity"),
                "IsOpeningBalance" = TRUE
            WHERE trim("LotNumber") = ''
               OR "ReceivedQuantity" IS NULL
               OR "ReceivedAtUtc" IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The legacy balance classification cannot be safely inferred in reverse.
    }
}
