using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations;

public partial class AddPurchaseReceiptSupplierHistoryAndCreditTerms : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "DefaultCreditDays",
            table: "Suppliers",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SupplierName",
            table: "Purchases",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SupplierNit",
            table: "Purchases",
            type: "nvarchar(max)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DefaultCreditDays", table: "Suppliers");
        migrationBuilder.DropColumn(name: "SupplierName", table: "Purchases");
        migrationBuilder.DropColumn(name: "SupplierNit", table: "Purchases");
    }
}
