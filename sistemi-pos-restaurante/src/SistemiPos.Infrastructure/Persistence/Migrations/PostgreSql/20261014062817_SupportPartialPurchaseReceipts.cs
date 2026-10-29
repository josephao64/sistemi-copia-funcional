using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class SupportPartialPurchaseReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Purchases_PurchaseOrderId",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReceipts_PurchaseOrderId",
                table: "PurchaseReceipts");

            migrationBuilder.AddColumn<decimal>(
                name: "BaseQuantityPerPackage",
                table: "PurchaseOrderItems",
                type: "numeric(18,6)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "BaseUnitCode",
                table: "PurchaseOrderItems",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductPresentationId",
                table: "PurchaseOrderItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReceivedQuantity",
                table: "PurchaseOrderItems",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("""
                UPDATE "PurchaseOrderItems"
                SET "BaseQuantityPerPackage" = GREATEST("UnitsPerPackage", 1);

                UPDATE "PurchaseOrderItems" AS item
                SET "ReceivedQuantity" = COALESCE((
                    SELECT SUM(receipt_item."ReceivedQuantity")
                    FROM "PurchaseReceiptItems" AS receipt_item
                    WHERE receipt_item."PurchaseOrderItemId" = item."Id"
                ), 0);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_PurchaseOrderId",
                table: "Purchases",
                column: "PurchaseOrderId",
                filter: "\"PurchaseOrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipts_PurchaseOrderId",
                table: "PurchaseReceipts",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItems_ProductPresentationId",
                table: "PurchaseOrderItems",
                column: "ProductPresentationId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderItems_ProductPurchasePresentations_ProductPres~",
                table: "PurchaseOrderItems",
                column: "ProductPresentationId",
                principalTable: "ProductPurchasePresentations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderItems_ProductPurchasePresentations_ProductPres~",
                table: "PurchaseOrderItems");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_PurchaseOrderId",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReceipts_PurchaseOrderId",
                table: "PurchaseReceipts");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderItems_ProductPresentationId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "BaseQuantityPerPackage",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "BaseUnitCode",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "ProductPresentationId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "ReceivedQuantity",
                table: "PurchaseOrderItems");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_PurchaseOrderId",
                table: "Purchases",
                column: "PurchaseOrderId",
                unique: true,
                filter: "\"PurchaseOrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipts_PurchaseOrderId",
                table: "PurchaseReceipts",
                column: "PurchaseOrderId",
                unique: true);
        }
    }
}
