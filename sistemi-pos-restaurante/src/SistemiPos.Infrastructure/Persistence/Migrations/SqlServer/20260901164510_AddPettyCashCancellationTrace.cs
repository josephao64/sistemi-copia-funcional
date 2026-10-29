using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPettyCashCancellationTrace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationAuthorizedByUserName",
                table: "PettyCashMovements",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "PettyCashMovements",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAtUtc",
                table: "PettyCashMovements",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelledByUserName",
                table: "PettyCashMovements",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "PettyCashMovements",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationAuthorizedByUserName",
                table: "PettyCashMovements");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "PettyCashMovements");

            migrationBuilder.DropColumn(
                name: "CancelledAtUtc",
                table: "PettyCashMovements");

            migrationBuilder.DropColumn(
                name: "CancelledByUserName",
                table: "PettyCashMovements");

            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "PettyCashMovements");
        }
    }
}
