using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddRestaurantOrderWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentStock",
                table: "InventoryStocks",
                type: "numeric(22,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

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

            migrationBuilder.AddColumn<Guid>(
                name: "RestaurantOrderEventId",
                table: "InventoryMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "InventoryMovementLots",
                type: "numeric(22,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

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
                name: "PosTerminals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RegisterCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosTerminals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PosTerminals_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosTerminals_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PosOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TerminalId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PosOrders_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosOrders_CashSessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalTable: "CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosOrders_PosTerminals_TerminalId",
                        column: x => x.TerminalId,
                        principalTable: "PosTerminals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosOrders_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PosOrderLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxPercent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsTaxExempt = table.Column<bool>(type: "boolean", nullable: false),
                    RecipeJson = table.Column<string>(type: "jsonb", nullable: true),
                    RecipeRevision = table.Column<int>(type: "integer", nullable: true),
                    PreparingQuantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PreparedQuantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DispatchedQuantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CancelledQuantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CancelledAfterPreparationQuantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    StartedQuantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosOrderLines", x => x.Id);
                    table.CheckConstraint("CK_OrderLine_Quantities", "\"Quantity\" > 0 AND \"PreparingQuantity\" >= 0 AND \"PreparedQuantity\" >= 0 AND \"DispatchedQuantity\" >= 0 AND \"CancelledQuantity\" >= 0 AND \"StartedQuantity\" >= 0 AND \"CancelledAfterPreparationQuantity\" >= 0 AND \"Quantity\" >= \"StartedQuantity\" + \"CancelledQuantity\" - \"CancelledAfterPreparationQuantity\"");
                    table.ForeignKey(
                        name: "FK_PosOrderLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosOrderLines_PosOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "PosOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RestaurantOrderEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OperationId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantOrderEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestaurantOrderEvents_PosOrderLines_LineId",
                        column: x => x.LineId,
                        principalTable: "PosOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestaurantOrderEvents_PosOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "PosOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_RestaurantOrderEventId_ProductId",
                table: "InventoryMovements",
                columns: new[] { "RestaurantOrderEventId", "ProductId" },
                unique: true,
                filter: "\"RestaurantOrderEventId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PosTerminals_BranchId_Code",
                table: "PosTerminals",
                columns: new[] { "BranchId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PosTerminals_WarehouseId",
                table: "PosTerminals",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrderEvents_LineId",
                table: "RestaurantOrderEvents",
                column: "LineId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrderEvents_OrderId_OperationId",
                table: "RestaurantOrderEvents",
                columns: new[] { "OrderId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PosOrderLines_OrderId",
                table: "PosOrderLines",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PosOrderLines_ProductId",
                table: "PosOrderLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PosOrders_BranchId_CreatedAtUtc",
                table: "PosOrders",
                columns: new[] { "BranchId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PosOrders_CashSessionId",
                table: "PosOrders",
                column: "CashSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PosOrders_TerminalId",
                table: "PosOrders",
                column: "TerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_PosOrders_WarehouseId",
                table: "PosOrders",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_RestaurantOrderEvents_RestaurantOrderEve~",
                table: "InventoryMovements",
                column: "RestaurantOrderEventId",
                principalTable: "RestaurantOrderEvents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovements_RestaurantOrderEvents_RestaurantOrderEve~",
                table: "InventoryMovements");

            migrationBuilder.DropTable(
                name: "RestaurantOrderEvents");

            migrationBuilder.DropTable(
                name: "PosOrderLines");

            migrationBuilder.DropTable(
                name: "PosOrders");

            migrationBuilder.DropTable(
                name: "PosTerminals");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_RestaurantOrderEventId_ProductId",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "RestaurantOrderEventId",
                table: "InventoryMovements");

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentStock",
                table: "InventoryStocks",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,6)");

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
