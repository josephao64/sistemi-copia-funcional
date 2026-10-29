using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SistemiPos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UnifyPurchaseCaptureAndDiscounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                table: "SupplierProductPriceHistory",
                type: "decimal(18,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");

            migrationBuilder.AddColumn<decimal>(
                name: "GlobalDiscountAmount",
                table: "SupplierProductPriceHistory",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LineDiscountAmount",
                table: "SupplierProductPriceHistory",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PackageName",
                table: "SupplierProductPriceHistory",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Unidad");

            migrationBuilder.AddColumn<decimal>(
                name: "GlobalDiscountAmount",
                table: "Purchases",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "GlobalDiscountType",
                table: "Purchases",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<decimal>(
                name: "GlobalDiscountValue",
                table: "Purchases",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossSubtotal",
                table: "Purchases",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LineDiscountTotal",
                table: "Purchases",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NetSubtotal",
                table: "Purchases",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "ActualUnitCost",
                table: "PurchaseReceiptItems",
                type: "decimal(18,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                table: "PurchaseItems",
                type: "decimal(18,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");

            migrationBuilder.AddColumn<decimal>(
                name: "GlobalDiscountAmount",
                table: "PurchaseItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossSubtotal",
                table: "PurchaseItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LineDiscountAmount",
                table: "PurchaseItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "LineDiscountType",
                table: "PurchaseItems",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<decimal>(
                name: "LineDiscountValue",
                table: "PurchaseItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PackageName",
                table: "PurchaseItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Unidad");

            migrationBuilder.AlterColumn<decimal>(
                name: "LastUnitCost",
                table: "ProductSuppliers",
                type: "decimal(18,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");

            migrationBuilder.AddColumn<string>(
                name: "PackageName",
                table: "ProductSuppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Unidad");

            migrationBuilder.CreateTable(
                name: "PurchasePresentations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Units = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchasePresentations", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "PurchasePresentations",
                columns: new[] { "Id", "CreatedAtUtc", "CreatedBy", "IsActive", "ModifiedAtUtc", "ModifiedBy", "Name", "Units" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, null, null, "Unidad", 1 },
                    { new Guid("10000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, null, null, "Docena", 12 },
                    { new Guid("10000000-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, null, null, "Caja 24", 24 },
                    { new Guid("10000000-0000-0000-0000-000000000004"), new DateTimeOffset(new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, null, null, "Caja 48", 48 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchasePresentations_Name",
                table: "PurchasePresentations",
                column: "Name",
                unique: true);

            migrationBuilder.Sql("""
                UPDATE PurchaseItems
                SET GrossSubtotal = Subtotal,
                    PackageName = CASE PackageType
                        WHEN 'Box' THEN N'Caja'
                        WHEN 'Package' THEN N'Paquete'
                        WHEN 'Bundle' THEN N'Fardo'
                        WHEN 'Dozen' THEN N'Docena'
                        WHEN 'Other' THEN N'Otro'
                        ELSE N'Unidad' END;

                UPDATE Purchases
                SET GrossSubtotal = Total,
                    NetSubtotal = Total;

                UPDATE ProductSuppliers
                SET PackageName = CASE PackageType
                    WHEN 'Box' THEN N'Caja'
                    WHEN 'Package' THEN N'Paquete'
                    WHEN 'Bundle' THEN N'Fardo'
                    WHEN 'Dozen' THEN N'Docena'
                    WHEN 'Other' THEN N'Otro'
                    ELSE N'Unidad' END;

                UPDATE SupplierProductPriceHistory
                SET PackageName = CASE PackageType
                    WHEN 'Box' THEN N'Caja'
                    WHEN 'Package' THEN N'Paquete'
                    WHEN 'Bundle' THEN N'Fardo'
                    WHEN 'Dozen' THEN N'Docena'
                    WHEN 'Other' THEN N'Otro'
                    ELSE N'Unidad' END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchasePresentations");

            migrationBuilder.DropColumn(
                name: "GlobalDiscountAmount",
                table: "SupplierProductPriceHistory");

            migrationBuilder.DropColumn(
                name: "LineDiscountAmount",
                table: "SupplierProductPriceHistory");

            migrationBuilder.DropColumn(
                name: "PackageName",
                table: "SupplierProductPriceHistory");

            migrationBuilder.DropColumn(
                name: "GlobalDiscountAmount",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "GlobalDiscountType",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "GlobalDiscountValue",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "GrossSubtotal",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "LineDiscountTotal",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "NetSubtotal",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "GlobalDiscountAmount",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "GrossSubtotal",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "LineDiscountAmount",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "LineDiscountType",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "LineDiscountValue",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "PackageName",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "PackageName",
                table: "ProductSuppliers");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                table: "SupplierProductPriceHistory",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "ActualUnitCost",
                table: "PurchaseReceiptItems",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                table: "PurchaseItems",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "LastUnitCost",
                table: "ProductSuppliers",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)");

        }
    }
}
