using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SistemiPos.Infrastructure.Persistence;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260912010000_RemoveOfflineSalesSupport")]
public partial class RemoveOfflineSalesSupport : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Sales_ClientOperationId",
            table: "Sales");

        migrationBuilder.DropColumn(
            name: "ClientOperationId",
            table: "Sales");

        migrationBuilder.DropColumn(
            name: "SynchronizedAtUtc",
            table: "Sales");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ClientOperationId",
            table: "Sales",
            type: "nvarchar(80)",
            maxLength: 80,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "SynchronizedAtUtc",
            table: "Sales",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Sales_ClientOperationId",
            table: "Sales",
            column: "ClientOperationId",
            unique: true,
            filter: "[ClientOperationId] IS NOT NULL");
    }
}
