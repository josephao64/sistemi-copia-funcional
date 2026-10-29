using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandPosDeviceRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BankAccountId",
                table: "PosDevices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "PosDevices",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Observation",
                table: "PosDevices",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "PosDevices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TerminalId",
                table: "PosDevices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PosDevices_BankAccountId",
                table: "PosDevices",
                column: "BankAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_PosDevices_BankAccounts_BankAccountId",
                table: "PosDevices",
                column: "BankAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PosDevices_BankAccounts_BankAccountId",
                table: "PosDevices");

            migrationBuilder.DropIndex(
                name: "IX_PosDevices_BankAccountId",
                table: "PosDevices");

            migrationBuilder.DropColumn(
                name: "BankAccountId",
                table: "PosDevices");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "PosDevices");

            migrationBuilder.DropColumn(
                name: "Observation",
                table: "PosDevices");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "PosDevices");

            migrationBuilder.DropColumn(
                name: "TerminalId",
                table: "PosDevices");
        }
    }
}
