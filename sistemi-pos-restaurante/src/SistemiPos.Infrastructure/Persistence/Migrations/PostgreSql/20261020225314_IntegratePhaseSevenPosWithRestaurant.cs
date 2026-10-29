using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class IntegratePhaseSevenPosWithRestaurant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderLine_Quantities",
                table: "PosOrderLines");

            migrationBuilder.AddColumn<string>(
                name: "TerminalCode",
                table: "Sales",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TerminalId",
                table: "Sales",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperationId",
                table: "SaleReturns",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprint",
                table: "SaleReturns",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperationId",
                table: "SaleRefunds",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ReferenceStock",
                table: "PhysicalInventoryCountItems",
                type: "numeric(22,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "OperationalStockBeforeAdjustment",
                table: "PhysicalInventoryCountItems",
                type: "numeric(22,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "OperationalStockAfterAdjustment",
                table: "PhysicalInventoryCountItems",
                type: "numeric(22,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Difference",
                table: "PhysicalInventoryCountItems",
                type: "numeric(22,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CountedStock",
                table: "PhysicalInventoryCountItems",
                type: "numeric(22,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "AppliedAdjustment",
                table: "PhysicalInventoryCountItems",
                type: "numeric(22,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "QuantitySent",
                table: "InventoryTransferItems",
                type: "numeric(22,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "QuantityReceived",
                table: "InventoryTransferItems",
                type: "numeric(22,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "CashSessions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrencySnapshotJson",
                table: "CashSessions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_TerminalId",
                table: "Sales",
                column: "TerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleReturns_SaleId_OperationId",
                table: "SaleReturns",
                columns: new[] { "SaleId", "OperationId" },
                unique: true,
                filter: "\"OperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SaleRefunds_SaleId_OperationId",
                table: "SaleRefunds",
                columns: new[] { "SaleId", "OperationId" },
                unique: true,
                filter: "\"OperationId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderLine_Quantities",
                table: "PosOrderLines",
                sql: "\"Quantity\" > 0 AND \"PreparingQuantity\" >= 0 AND \"PreparedQuantity\" >= 0 AND \"DispatchedQuantity\" >= 0 AND \"CancelledQuantity\" >= 0 AND \"StartedQuantity\" >= 0 AND \"CancelledAfterPreparationQuantity\" >= 0 AND \"Quantity\" >= \"StartedQuantity\" + \"CancelledQuantity\" - \"CancelledAfterPreparationQuantity\" AND \"SoldQuantity\" >= 0 AND \"SoldQuantity\" <= \"Quantity\" - \"CancelledQuantity\" AND \"AllocatedGrossAmount\" >= 0 AND \"AllocatedGrossAmount\" <= \"GrossAmount\"");

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_PosTerminals_TerminalId",
                table: "Sales",
                column: "TerminalId",
                principalTable: "PosTerminals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sales_PosTerminals_TerminalId",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_TerminalId",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_SaleReturns_SaleId_OperationId",
                table: "SaleReturns");

            migrationBuilder.DropIndex(
                name: "IX_SaleRefunds_SaleId_OperationId",
                table: "SaleRefunds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderLine_Quantities",
                table: "PosOrderLines");

            migrationBuilder.DropColumn(
                name: "TerminalCode",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "TerminalId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "OperationId",
                table: "SaleReturns");

            migrationBuilder.DropColumn(
                name: "RequestFingerprint",
                table: "SaleReturns");

            migrationBuilder.DropColumn(
                name: "OperationId",
                table: "SaleRefunds");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "CashSessions");

            migrationBuilder.DropColumn(
                name: "CurrencySnapshotJson",
                table: "CashSessions");

            migrationBuilder.AlterColumn<decimal>(
                name: "ReferenceStock",
                table: "PhysicalInventoryCountItems",
                type: "numeric(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "OperationalStockBeforeAdjustment",
                table: "PhysicalInventoryCountItems",
                type: "numeric(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "OperationalStockAfterAdjustment",
                table: "PhysicalInventoryCountItems",
                type: "numeric(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Difference",
                table: "PhysicalInventoryCountItems",
                type: "numeric(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CountedStock",
                table: "PhysicalInventoryCountItems",
                type: "numeric(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "AppliedAdjustment",
                table: "PhysicalInventoryCountItems",
                type: "numeric(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "QuantitySent",
                table: "InventoryTransferItems",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "QuantityReceived",
                table: "InventoryTransferItems",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,6)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderLine_Quantities",
                table: "PosOrderLines",
                sql: "\"Quantity\" > 0 AND \"PreparingQuantity\" >= 0 AND \"PreparedQuantity\" >= 0 AND \"DispatchedQuantity\" >= 0 AND \"CancelledQuantity\" >= 0 AND \"StartedQuantity\" >= 0 AND \"CancelledAfterPreparationQuantity\" >= 0 AND \"Quantity\" >= \"StartedQuantity\" + \"CancelledQuantity\" - \"CancelledAfterPreparationQuantity\"");
        }
    }
}
