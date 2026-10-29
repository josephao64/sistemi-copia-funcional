using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscountApprovalConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DiscountApprovalRequests",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DiscountApprovalRequests");
        }
    }
}
