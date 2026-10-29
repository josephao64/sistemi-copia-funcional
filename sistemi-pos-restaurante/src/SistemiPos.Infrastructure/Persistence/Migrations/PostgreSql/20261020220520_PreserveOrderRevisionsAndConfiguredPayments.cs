using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class PreserveOrderRevisionsAndConfiguredPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PosOrderLines_OrderId",
                table: "PosOrderLines");

            migrationBuilder.AddColumn<Guid>(
                name: "DefinitionId",
                table: "SalePayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefinitionName",
                table: "SalePayments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OtherReference",
                table: "SalePayments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ProductName",
                table: "SaleItems",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<string>(
                name: "ModifiersJson",
                table: "SaleItems",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "PosOrders",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiersJson",
                table: "PosOrderLines",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplacesLineId",
                table: "PosOrderLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevisionOperationId",
                table: "PosOrderLines",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtraPackagingJson",
                table: "RestaurantOrderEvents",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalOtherRefunds",
                table: "CashSessions",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalOtherSales",
                table: "CashSessions",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalePayments_DefinitionId",
                table: "SalePayments",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_PosOrderLines_OrderId_RevisionOperationId",
                table: "PosOrderLines",
                columns: new[] { "OrderId", "RevisionOperationId" },
                unique: true,
                filter: "\"RevisionOperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PosOrderLines_ReplacesLineId",
                table: "PosOrderLines",
                column: "ReplacesLineId");

            migrationBuilder.AddForeignKey(
                name: "FK_PosOrderLines_PosOrderLines_ReplacesLineId",
                table: "PosOrderLines",
                column: "ReplacesLineId",
                principalTable: "PosOrderLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalePayments_PaymentMethodDefinitions_DefinitionId",
                table: "SalePayments",
                column: "DefinitionId",
                principalTable: "PaymentMethodDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PosOrderLines_PosOrderLines_ReplacesLineId",
                table: "PosOrderLines");

            migrationBuilder.DropForeignKey(
                name: "FK_SalePayments_PaymentMethodDefinitions_DefinitionId",
                table: "SalePayments");

            migrationBuilder.DropIndex(
                name: "IX_SalePayments_DefinitionId",
                table: "SalePayments");

            migrationBuilder.DropIndex(
                name: "IX_PosOrderLines_OrderId_RevisionOperationId",
                table: "PosOrderLines");

            migrationBuilder.DropIndex(
                name: "IX_PosOrderLines_ReplacesLineId",
                table: "PosOrderLines");

            migrationBuilder.DropColumn(
                name: "DefinitionId",
                table: "SalePayments");

            migrationBuilder.DropColumn(
                name: "DefinitionName",
                table: "SalePayments");

            migrationBuilder.DropColumn(
                name: "OtherReference",
                table: "SalePayments");

            migrationBuilder.DropColumn(
                name: "ModifiersJson",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "PosOrders");

            migrationBuilder.DropColumn(
                name: "ModifiersJson",
                table: "PosOrderLines");

            migrationBuilder.DropColumn(
                name: "ReplacesLineId",
                table: "PosOrderLines");

            migrationBuilder.DropColumn(
                name: "RevisionOperationId",
                table: "PosOrderLines");

            migrationBuilder.DropColumn(
                name: "ExtraPackagingJson",
                table: "RestaurantOrderEvents");

            migrationBuilder.DropColumn(
                name: "TotalOtherRefunds",
                table: "CashSessions");

            migrationBuilder.DropColumn(
                name: "TotalOtherSales",
                table: "CashSessions");

            migrationBuilder.AlterColumn<string>(
                name: "ProductName",
                table: "SaleItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512);

            migrationBuilder.CreateIndex(
                name: "IX_PosOrderLines_OrderId",
                table: "PosOrderLines",
                column: "OrderId");
        }
    }
}
