using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SistemiPos.Infrastructure.Persistence;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260904133942_RemoveLegacyPurchaseOrderReceivedQuantity")]
public sealed class RemoveLegacyPurchaseOrderReceivedQuantity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ReceivedQuantity",
            table: "PurchaseOrderItems");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "ReceivedQuantity",
            table: "PurchaseOrderItems",
            type: "decimal(18,2)",
            nullable: false,
            defaultValue: 0m);
    }
}
