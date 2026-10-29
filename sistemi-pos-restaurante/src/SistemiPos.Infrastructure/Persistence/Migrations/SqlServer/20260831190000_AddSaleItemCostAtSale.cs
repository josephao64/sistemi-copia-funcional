using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SistemiPos.Infrastructure.Persistence;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260831190000_AddSaleItemCostAtSale")]
public partial class AddSaleItemCostAtSale : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>("CostAtSale", "SaleItems", type: "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.Sql("UPDATE si SET CostAtSale = ISNULL(p.LastCost, 0) FROM SaleItems si INNER JOIN Products p ON p.Id = si.ProductId;");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn("CostAtSale", "SaleItems");
}
