using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReusableProductVariantCatalog : Migration
    {
        /// <inheritdoc />
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

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = 'ProductInternalCodeSequence' AND schema_id = SCHEMA_ID('dbo'))
                BEGIN
                    DECLARE @next bigint = ISNULL((
                        SELECT MAX(TRY_CONVERT(bigint, SUBSTRING([InternalCode], 3, 50))) + 1
                        FROM [Products]
                        WHERE [InternalCode] LIKE 'P-%'
                    ), 1);
                    DECLARE @sequenceSql nvarchar(max);
                    SET @sequenceSql = N'CREATE SEQUENCE [dbo].[ProductInternalCodeSequence] AS bigint START WITH '
                        + CONVERT(nvarchar(30), @next)
                        + N' INCREMENT BY 1;';
                    EXEC sys.sp_executesql @sequenceSql;
                END
                """);

            migrationBuilder.AlterColumn<string>(
                name: "VariantValue",
                table: "Products",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ManagesVariants",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "VariantTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VariantTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VariantOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VariantOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VariantOptions_VariantTypes_VariantTypeId",
                        column: x => x.VariantTypeId,
                        principalTable: "VariantTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductVariantOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantOptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVariantOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductVariantOptions_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductVariantOptions_VariantOptions_VariantOptionId",
                        column: x => x.VariantOptionId,
                        principalTable: "VariantOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariantOptions_ProductId_VariantOptionId",
                table: "ProductVariantOptions",
                columns: new[] { "ProductId", "VariantOptionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariantOptions_VariantOptionId",
                table: "ProductVariantOptions",
                column: "VariantOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_VariantOptions_VariantTypeId_Value",
                table: "VariantOptions",
                columns: new[] { "VariantTypeId", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VariantTypes_Name",
                table: "VariantTypes",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.sequences WHERE name = 'ProductInternalCodeSequence' AND schema_id = SCHEMA_ID('dbo')) DROP SEQUENCE [dbo].[ProductInternalCodeSequence];");
            migrationBuilder.DropTable(
                name: "ProductVariantOptions");

            migrationBuilder.DropTable(
                name: "VariantOptions");

            migrationBuilder.DropTable(
                name: "VariantTypes");

            migrationBuilder.DropColumn(
                name: "ManagesVariants",
                table: "Products");

            migrationBuilder.AlterColumn<string>(
                name: "VariantValue",
                table: "Products",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldNullable: true);
        }
    }
}
