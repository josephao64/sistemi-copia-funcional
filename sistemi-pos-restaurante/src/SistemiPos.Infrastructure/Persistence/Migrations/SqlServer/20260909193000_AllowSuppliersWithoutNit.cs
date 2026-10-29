using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260909193000_AllowSuppliersWithoutNit")]
public sealed class AllowSuppliersWithoutNit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Suppliers_Nit",
            table: "Suppliers");

        migrationBuilder.CreateIndex(
            name: "IX_Suppliers_Nit",
            table: "Suppliers",
            column: "Nit",
            unique: true,
            filter: "[Nit] <> ''");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Suppliers_Nit",
            table: "Suppliers");

        migrationBuilder.CreateIndex(
            name: "IX_Suppliers_Nit",
            table: "Suppliers",
            column: "Nit",
            unique: true);
    }
}
