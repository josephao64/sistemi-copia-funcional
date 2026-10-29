using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260902003405_FixSaleCustomerRelationship")]
public partial class FixSaleCustomerRelationship : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('Sales', 'CustomerId1') IS NOT NULL
            BEGIN
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Sales_Customers_CustomerId1')
                    ALTER TABLE [Sales] DROP CONSTRAINT [FK_Sales_Customers_CustomerId1];
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Sales_CustomerId1' AND object_id = OBJECT_ID('Sales'))
                    DROP INDEX [IX_Sales_CustomerId1] ON [Sales];
                ALTER TABLE [Sales] DROP COLUMN [CustomerId1];
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('Sales', 'CustomerId1') IS NULL
            BEGIN
                ALTER TABLE [Sales] ADD [CustomerId1] uniqueidentifier NULL;
                CREATE INDEX [IX_Sales_CustomerId1] ON [Sales] ([CustomerId1]);
                ALTER TABLE [Sales] ADD CONSTRAINT [FK_Sales_Customers_CustomerId1]
                    FOREIGN KEY ([CustomerId1]) REFERENCES [Customers] ([Id]);
            END
            """);
    }
}
