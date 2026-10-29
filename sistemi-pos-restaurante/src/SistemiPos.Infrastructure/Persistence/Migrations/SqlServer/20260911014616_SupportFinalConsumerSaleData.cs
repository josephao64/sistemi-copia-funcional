using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupportFinalConsumerSaleData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerAddressSnapshot",
                table: "Sales",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerNameSnapshot",
                table: "Sales",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerNitSnapshot",
                table: "Sales",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerPhoneSnapshot",
                table: "Sales",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerVisitOriginSnapshot",
                table: "Sales",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerAddressSnapshot",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CustomerNameSnapshot",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CustomerNitSnapshot",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CustomerPhoneSnapshot",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CustomerVisitOriginSnapshot",
                table: "Sales");

        }
    }
}
