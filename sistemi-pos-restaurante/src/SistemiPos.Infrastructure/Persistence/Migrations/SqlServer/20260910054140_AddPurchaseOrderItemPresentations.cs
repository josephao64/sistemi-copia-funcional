using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseOrderItemPresentations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PackageQuantity",
                table: "PurchaseOrderItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "PresentationId",
                table: "PurchaseOrderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PresentationName",
                table: "PurchaseOrderItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Unidad");

            migrationBuilder.AddColumn<int>(
                name: "UnitsPerPackage",
                table: "PurchaseOrderItems",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(
                """
                UPDATE [PurchaseOrderItems]
                SET [PackageQuantity] = [RequestedQuantity],
                    [PresentationId] = '10000000-0000-0000-0000-000000000001',
                    [PresentationName] = N'Unidad',
                    [UnitsPerPackage] = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItems_PresentationId",
                table: "PurchaseOrderItems",
                column: "PresentationId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderItems_PurchasePresentations_PresentationId",
                table: "PurchaseOrderItems",
                column: "PresentationId",
                principalTable: "PurchasePresentations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderItems_PurchasePresentations_PresentationId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderItems_PresentationId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "PackageQuantity",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "PresentationId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "PresentationName",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "UnitsPerPackage",
                table: "PurchaseOrderItems");
        }
    }
}
