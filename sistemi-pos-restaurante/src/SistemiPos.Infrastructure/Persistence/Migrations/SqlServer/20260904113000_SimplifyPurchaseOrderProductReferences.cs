using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SistemiPos.Infrastructure.Persistence;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260904113000_SimplifyPurchaseOrderProductReferences")]
public sealed class SimplifyPurchaseOrderProductReferences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SupplierDescription",
            table: "Products",
            type: "nvarchar(800)",
            maxLength: 800,
            nullable: true);

        migrationBuilder.DropColumn(
            name: "EstimatedUnitCost",
            table: "PurchaseOrderItems");

    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "EstimatedUnitCost",
            table: "PurchaseOrderItems",
            type: "decimal(18,2)",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.DropColumn(
            name: "SupplierDescription",
            table: "Products");
    }
}
