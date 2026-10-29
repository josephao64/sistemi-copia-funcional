using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssignMultiplePosDevicesToCashSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashSessionPosDevices",
                columns: table => new
                {
                    CashSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PosDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PosDeviceName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashSessionPosDevices", x => new { x.CashSessionId, x.PosDeviceId });
                    table.ForeignKey(
                        name: "FK_CashSessionPosDevices_CashSessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalTable: "CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CashSessionPosDevices_PosDevices_PosDeviceId",
                        column: x => x.PosDeviceId,
                        principalTable: "PosDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashSessionPosDevices_PosDeviceId",
                table: "CashSessionPosDevices",
                column: "PosDeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashSessionPosDevices");
        }
    }
}
