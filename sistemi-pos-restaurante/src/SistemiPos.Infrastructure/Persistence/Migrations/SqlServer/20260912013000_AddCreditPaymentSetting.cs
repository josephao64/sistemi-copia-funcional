using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SistemiPos.Infrastructure.Persistence;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260912013000_AddCreditPaymentSetting")]
public partial class AddCreditPaymentSetting : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "CreditPaymentsEnabled",
            table: "SystemSettings",
            type: "bit",
            nullable: false,
            defaultValue: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CreditPaymentsEnabled",
            table: "SystemSettings");
    }
}
