using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations
{
    public partial class AddProductExpiryLots : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "ExpiryAlertDays", table: "Products", type: "int", nullable: true);
            migrationBuilder.AddColumn<bool>(name: "ManagesExpiry", table: "Products", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.CreateTable(
                name: "InventoryExpiryLots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AvailableQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SourceDocument = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryExpiryLots", x => x.Id);
                    table.ForeignKey(name: "FK_InventoryExpiryLots_Products_ProductId", column: x => x.ProductId,
                        principalTable: "Products", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateIndex(name: "IX_InventoryExpiryLots_ProductId_ExpiryDate", table: "InventoryExpiryLots",
                columns: new[] { "ProductId", "ExpiryDate" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "InventoryExpiryLots");
            migrationBuilder.DropColumn(name: "ExpiryAlertDays", table: "Products");
            migrationBuilder.DropColumn(name: "ManagesExpiry", table: "Products");
        }
    }
}
