using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddCompanySettingsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanySettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BusinessLegalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BusinessNit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BusinessAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BusinessPhone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BusinessEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BusinessLogoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CurrencyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrencyName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CurrencySymbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CurrencyDecimals = table.Column<int>(type: "integer", nullable: false),
                    DecimalSeparator = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    ThousandsSeparator = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    CurrencySymbolPosition = table.Column<int>(type: "integer", nullable: false),
                    TaxPercent = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ShowTaxOnReceipt = table.Column<bool>(type: "boolean", nullable: false),
                    ReceiptFooter = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CashPaymentsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    TransferPaymentsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    PosPaymentsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreditPaymentsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DiscountsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DefaultMaxDiscountPercent = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    NitLookupProvider = table.Column<int>(type: "integer", nullable: false),
                    FelEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    FelEnvironment = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    FelProvider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "INFILE"),
                    DefaultFelDocumentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "FACT"),
                    FelTaxPhrases = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    InfileApiUserProtected = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    InfileApiKeyProtected = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    InfileSignatureUserProtected = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    InfileSignatureKeyProtected = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanySettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanySettings_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanySettings_Currencies_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanySettings_CompanyId",
                table: "CompanySettings",
                column: "CompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanySettings_CurrencyId",
                table: "CompanySettings",
                column: "CurrencyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanySettings");
        }
    }
}
