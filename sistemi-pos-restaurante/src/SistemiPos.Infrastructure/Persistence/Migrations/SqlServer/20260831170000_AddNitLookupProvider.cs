using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SistemiPos.Infrastructure.Persistence;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260831170000_AddNitLookupProvider")]
public partial class AddNitLookupProvider : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "NitLookupProvider",
            table: "SystemSettings",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "NormalizedNit",
            table: "Customers",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.Sql(
            "UPDATE [Customers] SET [NormalizedNit] = UPPER(REPLACE(REPLACE([Nit], '-', ''), ' ', '')) WHERE [Nit] IS NOT NULL;");

        migrationBuilder.CreateIndex(
            name: "IX_Customers_NormalizedNit",
            table: "Customers",
            column: "NormalizedNit");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Customers_NormalizedNit",
            table: "Customers");

        migrationBuilder.DropColumn(
            name: "NitLookupProvider",
            table: "SystemSettings");

        migrationBuilder.DropColumn(
            name: "NormalizedNit",
            table: "Customers");
    }
}
