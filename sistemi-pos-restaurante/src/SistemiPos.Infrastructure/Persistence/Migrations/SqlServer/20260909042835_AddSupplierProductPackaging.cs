using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierProductPackaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                table: "PurchaseItems",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<decimal>(
                name: "PackageCost",
                table: "PurchaseItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PackageQuantity",
                table: "PurchaseItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PackageType",
                table: "PurchaseItems",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Unit");

            migrationBuilder.AddColumn<string>(
                name: "SupplierProductCode",
                table: "PurchaseItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierProductName",
                table: "PurchaseItems",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnitsPerPackage",
                table: "PurchaseItems",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("UPDATE PurchaseItems SET PackageQuantity = Quantity, UnitsPerPackage = 1, PackageCost = UnitCost, PackageType = 'Unit';");

            migrationBuilder.CreateTable(
                name: "ProductSuppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierProductName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SupplierProductCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PackageType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UnitsPerPackage = table.Column<int>(type: "int", nullable: false),
                    LastPackageCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LastUnitCost = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LastPurchaseDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Terms = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CreditDays = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSuppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductSuppliers_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductSuppliers_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierProductPriceHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductSupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PackageType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PackageQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitsPerPackage = table.Column<int>(type: "int", nullable: false),
                    PackageCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierProductPriceHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierProductPriceHistory_ProductSuppliers_ProductSupplierId",
                        column: x => x.ProductSupplierId,
                        principalTable: "ProductSuppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierProductPriceHistory_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierProductPriceHistory_Purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "Purchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierProductPriceHistory_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductSuppliers_ProductId_SupplierId",
                table: "ProductSuppliers",
                columns: new[] { "ProductId", "SupplierId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductSuppliers_SupplierId",
                table: "ProductSuppliers",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductPriceHistory_ProductId_SupplierId_PurchaseDateUtc",
                table: "SupplierProductPriceHistory",
                columns: new[] { "ProductId", "SupplierId", "PurchaseDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductPriceHistory_ProductSupplierId",
                table: "SupplierProductPriceHistory",
                column: "ProductSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductPriceHistory_PurchaseId",
                table: "SupplierProductPriceHistory",
                column: "PurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductPriceHistory_SupplierId",
                table: "SupplierProductPriceHistory",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierProductPriceHistory");

            migrationBuilder.DropTable(
                name: "ProductSuppliers");

            migrationBuilder.DropColumn(
                name: "PackageCost",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "PackageQuantity",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "PackageType",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "SupplierProductCode",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "SupplierProductName",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "UnitsPerPackage",
                table: "PurchaseItems");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                table: "PurchaseItems",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");
        }
    }
}
