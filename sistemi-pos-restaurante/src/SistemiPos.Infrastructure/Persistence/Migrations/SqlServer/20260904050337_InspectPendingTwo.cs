using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations;

/// <summary>
/// Conserva explícitamente el modelo final donde la recepción es un documento separado.
/// El cambio físico se crea de forma atómica en la migración inmediatamente anterior.
/// </summary>
public partial class FinalizeSeparatedPurchaseReceipt : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) { }
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
