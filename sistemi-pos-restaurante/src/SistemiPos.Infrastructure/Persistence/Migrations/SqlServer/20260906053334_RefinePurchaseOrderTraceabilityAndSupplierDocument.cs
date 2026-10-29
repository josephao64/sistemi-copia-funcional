using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations;

public partial class RefinePurchaseOrderTraceabilityAndSupplierDocument : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "InvoiceNumber", table: "Purchases", type: "nvarchar(100)", maxLength: 100,
            nullable: true, oldClrType: typeof(string), oldType: "nvarchar(100)", oldMaxLength: 100);

        migrationBuilder.Sql("UPDATE [Purchases] SET [InvoiceNumber] = NULL WHERE [InvoiceNumber] = [Number]");

        migrationBuilder.AddColumn<string>(
            name: "SubmittedBy", table: "PurchaseOrders", type: "nvarchar(256)", maxLength: 256, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "ReceivedBy", table: "PurchaseOrders", type: "nvarchar(256)", maxLength: 256, nullable: true);

        migrationBuilder.Sql("UPDATE [PurchaseOrders] SET [SubmittedBy] = [UserName] WHERE [SubmittedAtUtc] IS NOT NULL");
        migrationBuilder.Sql("UPDATE [PurchaseOrders] SET [ReceivedBy] = [UserName] WHERE [ReceivedAtUtc] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "SubmittedBy", table: "PurchaseOrders");
        migrationBuilder.DropColumn(name: "ReceivedBy", table: "PurchaseOrders");
        migrationBuilder.Sql("UPDATE [Purchases] SET [InvoiceNumber] = [Number] WHERE [InvoiceNumber] IS NULL");
        migrationBuilder.AlterColumn<string>(
            name: "InvoiceNumber", table: "Purchases", type: "nvarchar(100)", maxLength: 100,
            nullable: false, defaultValue: "", oldClrType: typeof(string), oldType: "nvarchar(100)",
            oldMaxLength: 100, oldNullable: true);
    }
}
