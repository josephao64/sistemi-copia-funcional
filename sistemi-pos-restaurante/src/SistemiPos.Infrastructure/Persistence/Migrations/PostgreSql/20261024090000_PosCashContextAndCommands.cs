using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SistemiPos.Infrastructure.Persistence.Migrations.PostgreSql;

[DbContext(typeof(AppDbContext))]
[Migration("20261024090000_PosCashContextAndCommands")]
public sealed class PosCashContextAndCommands : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<Guid>("TerminalId", "CashSessions", type: "uuid", nullable: true);
        m.AddColumn<string>("TimeZoneSnapshot", "CashSessions", type: "character varying(64)", maxLength: 64, nullable: true);
        m.AddColumn<string>("ClosedByUserName", "CashSessions", type: "character varying(256)", maxLength: 256, nullable: true);
        m.CreateIndex("IX_CashSessions_TerminalId", "CashSessions", "TerminalId");
        m.CreateIndex("IX_CashSessions_BranchId_TerminalId", "CashSessions", new[] { "BranchId", "TerminalId" }, unique: true,
            filter: "\"Status\" = 1 AND \"TerminalId\" IS NOT NULL");
        m.AddForeignKey("FK_CashSessions_PosTerminals_TerminalId", "CashSessions", "TerminalId", "PosTerminals", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        m.CreateTable("CashCommandReceipts", columns: t => new
        {
            ScopeKey = t.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
            Fingerprint = t.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
            SessionId = t.Column<Guid>(type: "uuid", nullable: false),
            CompletedAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_CashCommandReceipts", x => x.ScopeKey);
            t.ForeignKey("FK_CashCommandReceipts_CashSessions_SessionId", x => x.SessionId, "CashSessions", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_CashCommandReceipts_SessionId", "CashCommandReceipts", "SessionId");
        m.CreateIndex("IX_CashMovements_ManualReversal", "CashMovements", "SourceId", unique: true,
            filter: "\"SourceType\" = 'CashMovementReversal' AND \"SourceId\" IS NOT NULL");
        m.CreateIndex("IX_CashMovements_AutomaticPayment", "CashMovements", new[] { "SourceType", "SourceId" }, unique: true,
            filter: "\"SourceType\" IN ('SalePayment', 'SaleRefund') AND \"SourceId\" IS NOT NULL");
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropTable("CashCommandReceipts");
        m.DropIndex("IX_CashMovements_ManualReversal", "CashMovements");
        m.DropIndex("IX_CashMovements_AutomaticPayment", "CashMovements");
        m.DropForeignKey("FK_CashSessions_PosTerminals_TerminalId", "CashSessions");
        m.DropIndex("IX_CashSessions_BranchId_TerminalId", "CashSessions");
        m.DropIndex("IX_CashSessions_TerminalId", "CashSessions");
        m.DropColumn("TerminalId", "CashSessions");
        m.DropColumn("TimeZoneSnapshot", "CashSessions");
        m.DropColumn("ClosedByUserName", "CashSessions");
    }
}
