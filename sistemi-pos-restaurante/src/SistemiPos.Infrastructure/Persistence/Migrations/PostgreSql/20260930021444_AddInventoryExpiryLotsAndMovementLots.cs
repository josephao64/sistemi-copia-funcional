using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations.PostgreSql;

/// <summary>
/// Adds the inventory lot ledger. The guards reconcile databases that received the
/// equivalent migration from the parallel development branch before this migration.
/// </summary>
public partial class AddInventoryExpiryLotsAndMovementLots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "PurchaseItems"
                ADD COLUMN IF NOT EXISTS "BaseQuantityPerPackage" numeric(18,4),
                ADD COLUMN IF NOT EXISTS "BaseUnitCode" character varying(100),
                ADD COLUMN IF NOT EXISTS "ProductPresentationId" uuid;

            ALTER TABLE "Products"
                ALTER COLUMN "PreviousCost" TYPE numeric(18,6),
                ALTER COLUMN "LastCost" TYPE numeric(18,6);

            ALTER TABLE "InventoryExpiryLots"
                ALTER COLUMN "ExpiryDate" DROP NOT NULL,
                ADD COLUMN IF NOT EXISTS "IsOpeningBalance" boolean NOT NULL DEFAULT FALSE,
                ADD COLUMN IF NOT EXISTS "LotNumber" character varying(100) NOT NULL DEFAULT '',
                ADD COLUMN IF NOT EXISTS "ReceivedAtUtc" timestamp with time zone,
                ADD COLUMN IF NOT EXISTS "ReceivedQuantity" numeric(18,2),
                ADD COLUMN IF NOT EXISTS "WarehouseId" uuid;

            CREATE TABLE IF NOT EXISTS "InventoryMovementLots" (
                "Id" uuid NOT NULL,
                "MovementId" uuid NOT NULL,
                "LotId" uuid NOT NULL,
                "Quantity" numeric(18,2) NOT NULL,
                "ReversesAllocationId" uuid,
                CONSTRAINT "PK_InventoryMovementLots" PRIMARY KEY ("Id")
            );

            DO $migration$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'FK_InventoryExpiryLots_Warehouses_WarehouseId'
                ) THEN
                    ALTER TABLE "InventoryExpiryLots"
                        ADD CONSTRAINT "FK_InventoryExpiryLots_Warehouses_WarehouseId"
                        FOREIGN KEY ("WarehouseId") REFERENCES "Warehouses" ("Id") ON DELETE RESTRICT;
                END IF;

                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'FK_InventoryMovementLots_InventoryExpiryLots_LotId'
                ) THEN
                    ALTER TABLE "InventoryMovementLots"
                        ADD CONSTRAINT "FK_InventoryMovementLots_InventoryExpiryLots_LotId"
                        FOREIGN KEY ("LotId") REFERENCES "InventoryExpiryLots" ("Id") ON DELETE RESTRICT;
                END IF;

                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conrelid = '"InventoryMovementLots"'::regclass
                      AND conname LIKE 'FK_InventoryMovementLots_InventoryMovementLots_Revers%'
                ) THEN
                    ALTER TABLE "InventoryMovementLots"
                        ADD CONSTRAINT "FK_InventoryMovementLots_InventoryMovementLots_ReversAlloc"
                        FOREIGN KEY ("ReversesAllocationId") REFERENCES "InventoryMovementLots" ("Id") ON DELETE RESTRICT;
                END IF;

                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'FK_InventoryMovementLots_InventoryMovements_MovementId'
                ) THEN
                    ALTER TABLE "InventoryMovementLots"
                        ADD CONSTRAINT "FK_InventoryMovementLots_InventoryMovements_MovementId"
                        FOREIGN KEY ("MovementId") REFERENCES "InventoryMovements" ("Id") ON DELETE RESTRICT;
                END IF;
            END $migration$;

            CREATE INDEX IF NOT EXISTS "IX_InventoryExpiryLots_WarehouseId"
                ON "InventoryExpiryLots" ("WarehouseId");
            CREATE INDEX IF NOT EXISTS "IX_InventoryMovementLots_LotId"
                ON "InventoryMovementLots" ("LotId");
            CREATE INDEX IF NOT EXISTS "IX_InventoryMovementLots_MovementId_LotId_ReversesAllocationId"
                ON "InventoryMovementLots" ("MovementId", "LotId", "ReversesAllocationId");
            CREATE INDEX IF NOT EXISTS "IX_InventoryMovementLots_ReversesAllocationId"
                ON "InventoryMovementLots" ("ReversesAllocationId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "InventoryMovementLots");

        migrationBuilder.DropForeignKey(
            name: "FK_InventoryExpiryLots_Warehouses_WarehouseId",
            table: "InventoryExpiryLots");

        migrationBuilder.DropIndex(
            name: "IX_InventoryExpiryLots_WarehouseId",
            table: "InventoryExpiryLots");

        migrationBuilder.DropColumn(name: "BaseQuantityPerPackage", table: "PurchaseItems");
        migrationBuilder.DropColumn(name: "BaseUnitCode", table: "PurchaseItems");
        migrationBuilder.DropColumn(name: "ProductPresentationId", table: "PurchaseItems");

        migrationBuilder.AlterColumn<decimal>(
            name: "PreviousCost",
            table: "Products",
            type: "numeric(18,2)",
            nullable: false,
            oldClrType: typeof(decimal),
            oldType: "numeric(18,6)");

        migrationBuilder.AlterColumn<decimal>(
            name: "LastCost",
            table: "Products",
            type: "numeric(18,2)",
            nullable: false,
            oldClrType: typeof(decimal),
            oldType: "numeric(18,6)");

        migrationBuilder.AlterColumn<DateOnly>(
            name: "ExpiryDate",
            table: "InventoryExpiryLots",
            type: "date",
            nullable: false,
            defaultValue: new DateOnly(1, 1, 1),
            oldClrType: typeof(DateOnly),
            oldType: "date",
            oldNullable: true);

        migrationBuilder.DropColumn(name: "IsOpeningBalance", table: "InventoryExpiryLots");
        migrationBuilder.DropColumn(name: "LotNumber", table: "InventoryExpiryLots");
        migrationBuilder.DropColumn(name: "ReceivedAtUtc", table: "InventoryExpiryLots");
        migrationBuilder.DropColumn(name: "ReceivedQuantity", table: "InventoryExpiryLots");
        migrationBuilder.DropColumn(name: "WarehouseId", table: "InventoryExpiryLots");
    }
}
