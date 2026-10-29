using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddRecipeProductionAndHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "CostAtSale",
                table: "SaleItems",
                type: "numeric(22,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AddColumn<string>(
                name: "RecipeConsumptionJson",
                table: "SaleItems",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecipeVersionId",
                table: "SaleItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecipeVersionNumber",
                table: "SaleItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConsumptionMode",
                table: "Recipes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentStock",
                table: "InventoryStocks",
                type: "numeric(22,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                table: "InventoryMovements",
                type: "numeric(22,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "InventoryMovements",
                type: "numeric(22,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "PreviousStock",
                table: "InventoryMovements",
                type: "numeric(22,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "NewStock",
                table: "InventoryMovements",
                type: "numeric(22,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "InventoryMovementLots",
                type: "numeric(22,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                table: "InventoryExpiryLots",
                type: "numeric(22,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ReceivedQuantity",
                table: "InventoryExpiryLots",
                type: "numeric(22,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "AvailableQuantity",
                table: "InventoryExpiryLots",
                type: "numeric(22,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.CreateTable(
                name: "ProductCostBaselines",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCostBaselines", x => x.ProductId);
                    table.ForeignKey(
                        name: "FK_ProductCostBaselines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    EffectiveAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DefinitionJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeVersions_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductionOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LotNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PlannedQuantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ActualQuantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    WasteQuantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    VarianceReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ScheduledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResponsibleUser = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ConfirmedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ConsumptionJson = table.Column<string>(type: "jsonb", nullable: false),
                    TotalCost = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionOrders", x => x.Id);
                    table.CheckConstraint("CK_Production_Yield", "\"PlannedQuantity\" > 0 AND (\"ActualQuantity\" IS NULL OR \"ActualQuantity\" >= 0)");
                    table.ForeignKey(
                        name: "FK_ProductionOrders_RecipeVersions_RecipeVersionId",
                        column: x => x.RecipeVersionId,
                        principalTable: "RecipeVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionOrders_Warehouses_DestinationWarehouseId",
                        column: x => x.DestinationWarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionOrders_Warehouses_OriginWarehouseId",
                        column: x => x.OriginWarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_RecipeVersionId",
                table: "SaleItems",
                column: "RecipeVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_DestinationWarehouseId",
                table: "ProductionOrders",
                column: "DestinationWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_Number",
                table: "ProductionOrders",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_OperationId",
                table: "ProductionOrders",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_OriginWarehouseId",
                table: "ProductionOrders",
                column: "OriginWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_RecipeVersionId",
                table: "ProductionOrders",
                column: "RecipeVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeVersions_RecipeId_Number",
                table: "RecipeVersions",
                columns: new[] { "RecipeId", "Number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleItems_RecipeVersions_RecipeVersionId",
                table: "SaleItems",
                column: "RecipeVersionId",
                principalTable: "RecipeVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                UPDATE "Recipes" r SET "ConsumptionMode" = CASE WHEN p."ArticleType" IN ('Subreceta', '4') THEN 2 ELSE 1 END
                FROM "Products" p WHERE p."Id" = r."ProductId";
                INSERT INTO "ProductCostBaselines" ("ProductId", "EffectiveAtUtc", "UnitCost")
                SELECT "Id", CURRENT_TIMESTAMP, "LastCost" FROM "Products";
                INSERT INTO "RecipeVersions" ("Id", "RecipeId", "Number", "EffectiveAtUtc", "DefinitionJson")
                SELECT r."Id", r."Id", r."Revision", CURRENT_TIMESTAMP,
                    jsonb_build_object(
                        'ProductId', r."ProductId", 'ProductName', concat_ws(' ', p."Name", p."VariantValue"),
                        'YieldUnitId', r."YieldUnitId", 'YieldUnitCode', r."YieldUnitCode", 'YieldPrecision', u."Precision",
                        'YieldQuantity', r."YieldQuantity", 'Instructions', r."Instructions", 'ConsumptionMode', r."ConsumptionMode",
                        'Components', (SELECT jsonb_agg(jsonb_build_object(
                            'ProductId', i."ProductId", 'Code', c."InternalCode", 'Name', concat_ws(' ', c."Name", c."VariantValue"),
                            'UnitId', i."UnitId", 'UnitCode', i."UnitCode", 'Quantity', i."Quantity",
                            'BaseUnitId', c."BaseUnitId", 'BaseUnitCode', i."BaseUnitCode", 'Precision', cu."Precision",
                            'BaseQuantity', i."BaseQuantity", 'SubrecipeVersionId', i."SubrecipeId") ORDER BY i."Id")
                            FROM "RecipeIngredients" i JOIN "Products" c ON c."Id" = i."ProductId"
                            JOIN "UnitsOfMeasure" cu ON cu."Id" = c."BaseUnitId" WHERE i."RecipeId" = r."Id"))
                FROM "Recipes" r JOIN "Products" p ON p."Id" = r."ProductId"
                JOIN "UnitsOfMeasure" u ON u."Id" = r."YieldUnitId";
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SaleItems_RecipeVersions_RecipeVersionId",
                table: "SaleItems");

            migrationBuilder.DropTable(
                name: "ProductCostBaselines");

            migrationBuilder.DropTable(
                name: "ProductionOrders");

            migrationBuilder.DropTable(
                name: "RecipeVersions");

            migrationBuilder.DropIndex(
                name: "IX_SaleItems_RecipeVersionId",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "RecipeConsumptionJson",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "RecipeVersionId",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "RecipeVersionNumber",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "ConsumptionMode",
                table: "Recipes");

            migrationBuilder.AlterColumn<decimal>(
                name: "CostAtSale",
                table: "SaleItems",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentStock",
                table: "InventoryStocks",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                table: "InventoryMovements",
                type: "numeric(18,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "InventoryMovements",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "PreviousStock",
                table: "InventoryMovements",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "NewStock",
                table: "InventoryMovements",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "InventoryMovementLots",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                table: "InventoryExpiryLots",
                type: "numeric(18,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ReceivedQuantity",
                table: "InventoryExpiryLots",
                type: "numeric(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "AvailableQuantity",
                table: "InventoryExpiryLots",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,6)");
        }
    }
}
