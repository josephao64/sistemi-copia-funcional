using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class CompletePosCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalePayments_SaleId",
                table: "SalePayments");

            migrationBuilder.AddColumn<int>(
                name: "FinancialRevision",
                table: "Sales",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprint",
                table: "Sales",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RestaurantOrderId",
                table: "Sales",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Sales",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentId",
                table: "SaleRefunds",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AccountPartId",
                table: "SalePayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrderLineId",
                table: "SaleItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipeJson",
                table: "SaleItems",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AllocatedGrossAmount",
                table: "PosOrderLines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SoldQuantity",
                table: "PosOrderLines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "OperationRevision",
                table: "CashSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "CashSessions",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "SaleAccountParts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleAccountParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleAccountParts_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_RestaurantOrderId",
                table: "Sales",
                column: "RestaurantOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleRefunds_PaymentId",
                table: "SaleRefunds",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_SalePayments_AccountPartId",
                table: "SalePayments",
                column: "AccountPartId");

            migrationBuilder.CreateIndex(
                name: "IX_SalePayments_SaleId",
                table: "SalePayments",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_OrderLineId",
                table: "SaleItems",
                column: "OrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_CashSessions_BranchId_RegisterCode",
                table: "CashSessions",
                columns: new[] { "BranchId", "RegisterCode" },
                unique: true,
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CashSessions_CashierUserName",
                table: "CashSessions",
                column: "CashierUserName",
                unique: true,
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SaleAccountParts_SaleId_Number",
                table: "SaleAccountParts",
                columns: new[] { "SaleId", "Number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleItems_PosOrderLines_OrderLineId",
                table: "SaleItems",
                column: "OrderLineId",
                principalTable: "PosOrderLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalePayments_SaleAccountParts_AccountPartId",
                table: "SalePayments",
                column: "AccountPartId",
                principalTable: "SaleAccountParts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SaleRefunds_SalePayments_PaymentId",
                table: "SaleRefunds",
                column: "PaymentId",
                principalTable: "SalePayments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_PosOrders_RestaurantOrderId",
                table: "Sales",
                column: "RestaurantOrderId",
                principalTable: "PosOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SaleItems_PosOrderLines_OrderLineId",
                table: "SaleItems");

            migrationBuilder.DropForeignKey(
                name: "FK_SalePayments_SaleAccountParts_AccountPartId",
                table: "SalePayments");

            migrationBuilder.DropForeignKey(
                name: "FK_SaleRefunds_SalePayments_PaymentId",
                table: "SaleRefunds");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_PosOrders_RestaurantOrderId",
                table: "Sales");

            migrationBuilder.DropTable(
                name: "SaleAccountParts");

            migrationBuilder.DropIndex(
                name: "IX_Sales_RestaurantOrderId",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_SaleRefunds_PaymentId",
                table: "SaleRefunds");

            migrationBuilder.DropIndex(
                name: "IX_SalePayments_AccountPartId",
                table: "SalePayments");

            migrationBuilder.DropIndex(
                name: "IX_SalePayments_SaleId",
                table: "SalePayments");

            migrationBuilder.DropIndex(
                name: "IX_SaleItems_OrderLineId",
                table: "SaleItems");

            migrationBuilder.DropIndex(
                name: "IX_CashSessions_BranchId_RegisterCode",
                table: "CashSessions");

            migrationBuilder.DropIndex(
                name: "IX_CashSessions_CashierUserName",
                table: "CashSessions");

            migrationBuilder.DropColumn(
                name: "FinancialRevision",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "RequestFingerprint",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "RestaurantOrderId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "PaymentId",
                table: "SaleRefunds");

            migrationBuilder.DropColumn(
                name: "AccountPartId",
                table: "SalePayments");

            migrationBuilder.DropColumn(
                name: "OrderLineId",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "RecipeJson",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "AllocatedGrossAmount",
                table: "PosOrderLines");

            migrationBuilder.DropColumn(
                name: "SoldQuantity",
                table: "PosOrderLines");

            migrationBuilder.DropColumn(
                name: "OperationRevision",
                table: "CashSessions");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "CashSessions");

            migrationBuilder.CreateIndex(
                name: "IX_SalePayments_SaleId",
                table: "SalePayments",
                column: "SaleId",
                unique: true);
        }
    }
}
