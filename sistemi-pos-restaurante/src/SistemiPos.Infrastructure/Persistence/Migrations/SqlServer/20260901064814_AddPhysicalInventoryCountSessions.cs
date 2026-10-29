using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhysicalInventoryCountSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "InventoryStocks",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "PhysicalInventoryCountId",
                table: "InventoryMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PhysicalInventoryCountItemId",
                table: "InventoryMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PhysicalInventoryCounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FinalizedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FinalizedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalInventoryCounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PhysicalInventoryCountCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhysicalInventoryCountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalInventoryCountCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhysicalInventoryCountCategories_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalInventoryCountCategories_PhysicalInventoryCounts_PhysicalInventoryCountId",
                        column: x => x.PhysicalInventoryCountId,
                        principalTable: "PhysicalInventoryCounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhysicalInventoryCountItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhysicalInventoryCountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsActiveParticipation = table.Column<bool>(type: "bit", nullable: false),
                    ReferenceStock = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ReferenceStockVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    CountedStock = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Difference = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CountingStartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CountingStartedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CountedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CountedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OperationalStockBeforeAdjustment = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AppliedAdjustment = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OperationalStockAfterAdjustment = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalInventoryCountItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhysicalInventoryCountItems_PhysicalInventoryCounts_PhysicalInventoryCountId",
                        column: x => x.PhysicalInventoryCountId,
                        principalTable: "PhysicalInventoryCounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PhysicalInventoryCountItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_PhysicalInventoryCountId",
                table: "InventoryMovements",
                column: "PhysicalInventoryCountId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_PhysicalInventoryCountItemId",
                table: "InventoryMovements",
                column: "PhysicalInventoryCountItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalInventoryCountCategories_CategoryId",
                table: "PhysicalInventoryCountCategories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalInventoryCountCategories_PhysicalInventoryCountId_CategoryId",
                table: "PhysicalInventoryCountCategories",
                columns: new[] { "PhysicalInventoryCountId", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalInventoryCountItems_PhysicalInventoryCountId_ProductId",
                table: "PhysicalInventoryCountItems",
                columns: new[] { "PhysicalInventoryCountId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalInventoryCountItems_ProductId",
                table: "PhysicalInventoryCountItems",
                column: "ProductId",
                unique: true,
                filter: "[IsActiveParticipation] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_PhysicalInventoryCountItems_PhysicalInventoryCountItemId",
                table: "InventoryMovements",
                column: "PhysicalInventoryCountItemId",
                principalTable: "PhysicalInventoryCountItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_PhysicalInventoryCounts_PhysicalInventoryCountId",
                table: "InventoryMovements",
                column: "PhysicalInventoryCountId",
                principalTable: "PhysicalInventoryCounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovements_PhysicalInventoryCountItems_PhysicalInventoryCountItemId",
                table: "InventoryMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovements_PhysicalInventoryCounts_PhysicalInventoryCountId",
                table: "InventoryMovements");

            migrationBuilder.DropTable(
                name: "PhysicalInventoryCountCategories");

            migrationBuilder.DropTable(
                name: "PhysicalInventoryCountItems");

            migrationBuilder.DropTable(
                name: "PhysicalInventoryCounts");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_PhysicalInventoryCountId",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_PhysicalInventoryCountItemId",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "InventoryStocks");

            migrationBuilder.DropColumn(
                name: "PhysicalInventoryCountId",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "PhysicalInventoryCountItemId",
                table: "InventoryMovements");
        }
    }
}
