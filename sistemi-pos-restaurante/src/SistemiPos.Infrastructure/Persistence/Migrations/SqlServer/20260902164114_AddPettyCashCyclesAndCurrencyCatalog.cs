using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPettyCashCyclesAndCurrencyCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrencyId",
                table: "SystemSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "PettyCashMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CycleId",
                table: "PettyCashMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAtUtc",
                table: "PettyCashFunds",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentCycleId",
                table: "PettyCashFunds",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OpenedAtUtc",
                table: "PettyCashFunds",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResponsibleUserId",
                table: "PettyCashFunds",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "ResponsibleUserName",
                table: "PettyCashFunds",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Decimals = table.Column<int>(type: "int", nullable: false),
                    DecimalSeparator = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    ThousandsSeparator = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    SymbolPosition = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PettyCashCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PettyCashCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PettyCashCycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PettyCashFundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InitialBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpectedBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CountedBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Difference = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OpenedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OpenedByUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ClosedByUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Observation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PettyCashCycles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PettyCashCycles_PettyCashFunds_PettyCashFundId",
                        column: x => x.PettyCashFundId,
                        principalTable: "PettyCashFunds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                DECLARE @now datetimeoffset = SYSDATETIMEOFFSET();
                INSERT INTO Currencies (Id, Code, Name, Symbol, Decimals, DecimalSeparator, ThousandsSeparator, SymbolPosition, IsActive, CreatedAtUtc)
                VALUES
                  ('4F4105F3-9B4A-4E24-B7C0-8A2F96970801', 'GTQ', N'Quetzal', 'Q', 2, '.', ',', 0, 1, @now),
                  ('4F4105F3-9B4A-4E24-B7C0-8A2F96970802', 'USD', N'Dólar', '$', 2, '.', ',', 0, 1, @now),
                  ('4F4105F3-9B4A-4E24-B7C0-8A2F96970803', 'MXN', N'Peso mexicano', '$', 2, '.', ',', 0, 1, @now);

                UPDATE SystemSettings SET CurrencyId = CASE CurrencyCode WHEN 'USD' THEN '4F4105F3-9B4A-4E24-B7C0-8A2F96970802' WHEN 'MXN' THEN '4F4105F3-9B4A-4E24-B7C0-8A2F96970803' ELSE '4F4105F3-9B4A-4E24-B7C0-8A2F96970801' END;

                INSERT INTO PettyCashCategories (Id, Name, IsActive, CreatedAtUtc)
                SELECT NEWID(), LTRIM(RTRIM(Category)), 1, @now FROM PettyCashMovements WHERE NULLIF(LTRIM(RTRIM(Category)), '') IS NOT NULL GROUP BY LTRIM(RTRIM(Category));
                UPDATE m SET CategoryId = c.Id FROM PettyCashMovements m INNER JOIN PettyCashCategories c ON c.Name = LTRIM(RTRIM(m.Category));

                UPDATE f SET ResponsibleUserId = COALESCE(owner.Id, fallback.Id, '00000000-0000-0000-0000-000000000000'), ResponsibleUserName = COALESCE(owner.UserName, fallback.UserName, N'Sin asignar')
                FROM PettyCashFunds f
                OUTER APPLY (SELECT TOP 1 u.Id, u.UserName FROM PettyCashMovements m INNER JOIN AspNetUsers u ON u.UserName = m.UserName WHERE m.PettyCashFundId = f.Id ORDER BY m.OccurredAtUtc) owner
                OUTER APPLY (SELECT TOP 1 u.Id, u.UserName FROM AspNetUsers u WHERE u.IsActive = 1 ORDER BY u.Id) fallback;

                INSERT INTO PettyCashCycles (Id, PettyCashFundId, InitialBalance, Status, OpenedAtUtc, OpenedByUserName, CreatedAtUtc)
                SELECT NEWID(), f.Id, f.FundAmount, 1, COALESCE((SELECT MIN(m.OccurredAtUtc) FROM PettyCashMovements m WHERE m.PettyCashFundId = f.Id), f.CreatedAtUtc, @now), f.ResponsibleUserName, @now FROM PettyCashFunds f;
                UPDATE f SET CurrentCycleId = c.Id, OpenedAtUtc = c.OpenedAtUtc FROM PettyCashFunds f INNER JOIN PettyCashCycles c ON c.PettyCashFundId = f.Id;
                UPDATE m SET CycleId = c.Id FROM PettyCashMovements m INNER JOIN PettyCashCycles c ON c.PettyCashFundId = m.PettyCashFundId;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_CurrencyId",
                table: "SystemSettings",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_PettyCashMovements_CategoryId",
                table: "PettyCashMovements",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PettyCashMovements_CycleId",
                table: "PettyCashMovements",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_PettyCashFunds_ResponsibleUserId",
                table: "PettyCashFunds",
                column: "ResponsibleUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_Code",
                table: "Currencies",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PettyCashCategories_Name",
                table: "PettyCashCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PettyCashCycles_PettyCashFundId_Status",
                table: "PettyCashCycles",
                columns: new[] { "PettyCashFundId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_PettyCashMovements_PettyCashCategories_CategoryId",
                table: "PettyCashMovements",
                column: "CategoryId",
                principalTable: "PettyCashCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PettyCashMovements_PettyCashCycles_CycleId",
                table: "PettyCashMovements",
                column: "CycleId",
                principalTable: "PettyCashCycles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SystemSettings_Currencies_CurrencyId",
                table: "SystemSettings",
                column: "CurrencyId",
                principalTable: "Currencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PettyCashMovements_PettyCashCategories_CategoryId",
                table: "PettyCashMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_PettyCashMovements_PettyCashCycles_CycleId",
                table: "PettyCashMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_SystemSettings_Currencies_CurrencyId",
                table: "SystemSettings");

            migrationBuilder.DropTable(
                name: "Currencies");

            migrationBuilder.DropTable(
                name: "PettyCashCategories");

            migrationBuilder.DropTable(
                name: "PettyCashCycles");

            migrationBuilder.DropIndex(
                name: "IX_SystemSettings_CurrencyId",
                table: "SystemSettings");

            migrationBuilder.DropIndex(
                name: "IX_PettyCashMovements_CategoryId",
                table: "PettyCashMovements");

            migrationBuilder.DropIndex(
                name: "IX_PettyCashMovements_CycleId",
                table: "PettyCashMovements");

            migrationBuilder.DropIndex(
                name: "IX_PettyCashFunds_ResponsibleUserId",
                table: "PettyCashFunds");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "PettyCashMovements");

            migrationBuilder.DropColumn(
                name: "CycleId",
                table: "PettyCashMovements");

            migrationBuilder.DropColumn(
                name: "ClosedAtUtc",
                table: "PettyCashFunds");

            migrationBuilder.DropColumn(
                name: "CurrentCycleId",
                table: "PettyCashFunds");

            migrationBuilder.DropColumn(
                name: "OpenedAtUtc",
                table: "PettyCashFunds");

            migrationBuilder.DropColumn(
                name: "ResponsibleUserId",
                table: "PettyCashFunds");

            migrationBuilder.DropColumn(
                name: "ResponsibleUserName",
                table: "PettyCashFunds");
        }
    }
}
