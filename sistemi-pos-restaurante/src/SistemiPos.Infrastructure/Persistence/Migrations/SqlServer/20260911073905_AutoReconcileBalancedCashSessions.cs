using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AutoReconcileBalancedCashSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [CashSessions]
                SET [ReconciledAtUtc] = [ClosedAtUtc],
                    [ReconciledByUserName] = [CashierUserName],
                    [ReconciliationObservation] = N'Conciliación automática: cierre sin diferencia.'
                WHERE [Status] = 2
                  AND [Difference] = 0
                  AND [ReconciledAtUtc] IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [CashSessions]
                SET [ReconciledAtUtc] = NULL,
                    [ReconciledByUserName] = NULL,
                    [ReconciliationObservation] = NULL
                WHERE [Status] = 2
                  AND [Difference] = 0
                  AND [ReconciliationObservation] = N'Conciliación automática: cierre sin diferencia.';
                """);
        }
    }
}
