using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillSoft.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _20260621000000_VendorSettlementNotesAndSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "NewOutstandingAmount",
                table: "VendorSettlements",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "VendorSettlements",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PreviousOutstandingAmount",
                table: "VendorSettlements",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NewOutstandingAmount",
                table: "VendorSettlements");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "VendorSettlements");

            migrationBuilder.DropColumn(
                name: "PreviousOutstandingAmount",
                table: "VendorSettlements");
        }
    }
}
