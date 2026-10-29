using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260906190000_AddCustomerCreditTerms")]
public sealed class AddCustomerCreditTerms : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "CreditDays", table: "Customers", type: "int", nullable: false, defaultValue: 30);
        migrationBuilder.AddColumn<int>(name: "GraceDays", table: "Customers", type: "int", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>(name: "CreditDays", table: "ReceivableAccounts", type: "int", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>(name: "GraceDays", table: "ReceivableAccounts", type: "int", nullable: false, defaultValue: 0);
        migrationBuilder.Sql("UPDATE ReceivableAccounts SET CreditDays = CASE WHEN DATEDIFF(day, SaleDateUtc, DueDateUtc) < 0 THEN 0 ELSE DATEDIFF(day, SaleDateUtc, DueDateUtc) END;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CreditDays", table: "Customers");
        migrationBuilder.DropColumn(name: "GraceDays", table: "Customers");
        migrationBuilder.DropColumn(name: "CreditDays", table: "ReceivableAccounts");
        migrationBuilder.DropColumn(name: "GraceDays", table: "ReceivableAccounts");
    }
}
