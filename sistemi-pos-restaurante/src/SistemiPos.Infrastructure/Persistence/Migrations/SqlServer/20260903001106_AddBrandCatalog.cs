using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBrandCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Brands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands", x => x.Id);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO [Brands] ([Id], [Name], [IsActive], [CreatedAtUtc], [CreatedBy], [ModifiedAtUtc], [ModifiedBy])
                SELECT NEWID(), [Existing].[Name], CAST(1 AS bit), SYSDATETIMEOFFSET(), N'Migration', NULL, NULL
                FROM (
                    SELECT DISTINCT LTRIM(RTRIM([Brand])) AS [Name]
                    FROM [Products]
                    WHERE [Brand] IS NOT NULL AND LTRIM(RTRIM([Brand])) <> N''
                ) AS [Existing];
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Brands_Name",
                table: "Brands",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Brands");
        }
    }
}
