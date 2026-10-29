using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseConfirmationTraceability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConfirmedAtUtc",
                table: "Purchases",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmedBy",
                table: "Purchases",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.Sql("UPDATE [Purchases] SET [ConfirmedAtUtc] = CASE WHEN [CreatedAtUtc] = '0001-01-01T00:00:00.0000000+00:00' THEN [PurchaseDateUtc] ELSE [CreatedAtUtc] END, [ConfirmedBy] = COALESCE([CreatedBy], [UserName]) WHERE [Status] = 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfirmedAtUtc",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "ConfirmedBy",
                table: "Purchases");
        }
    }
}
