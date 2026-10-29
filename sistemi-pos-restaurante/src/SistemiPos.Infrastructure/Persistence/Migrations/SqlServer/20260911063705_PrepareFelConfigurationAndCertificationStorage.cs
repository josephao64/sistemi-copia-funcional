using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrepareFelConfigurationAndCertificationStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultFelDocumentType",
                table: "SystemSettings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "FACT");

            migrationBuilder.AddColumn<string>(
                name: "EstablishmentCode",
                table: "SystemSettings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstablishmentName",
                table: "SystemSettings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FelEnabled",
                table: "SystemSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "FelEnvironment",
                table: "SystemSettings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "FelProvider",
                table: "SystemSettings",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "INFILE");

            migrationBuilder.AddColumn<string>(
                name: "FelTaxPhrases",
                table: "SystemSettings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InfileApiKeyProtected",
                table: "SystemSettings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InfileApiUserProtected",
                table: "SystemSettings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InfileSignatureKeyProtected",
                table: "SystemSettings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InfileSignatureUserProtected",
                table: "SystemSettings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FelCertifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AuthorizationNumber = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Series = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CertifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CertifierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CertifierNit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    QrCode = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AccessNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CertifiedDocumentXml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CertificationResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FelCertifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FelCertifications_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FelCertifications_SaleId",
                table: "FelCertifications",
                column: "SaleId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FelCertifications");

            migrationBuilder.DropColumn(
                name: "DefaultFelDocumentType",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "EstablishmentCode",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "EstablishmentName",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "FelEnabled",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "FelEnvironment",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "FelProvider",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "FelTaxPhrases",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "InfileApiKeyProtected",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "InfileApiUserProtected",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "InfileSignatureKeyProtected",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "InfileSignatureUserProtected",
                table: "SystemSettings");
        }
    }
}
