using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillSoft.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KitchenTicketOrderSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerNameSnapshot",
                table: "KitchenTickets",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderNotesSnapshot",
                table: "KitchenTickets",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TableNameSnapshot",
                table: "KitchenTickets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerNameSnapshot",
                table: "KitchenTickets");

            migrationBuilder.DropColumn(
                name: "OrderNotesSnapshot",
                table: "KitchenTickets");

            migrationBuilder.DropColumn(
                name: "TableNameSnapshot",
                table: "KitchenTickets");
        }
    }
}
