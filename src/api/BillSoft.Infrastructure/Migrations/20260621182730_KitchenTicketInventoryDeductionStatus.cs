using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillSoft.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KitchenTicketInventoryDeductionStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InventoryDeductionStatus",
                table: "KitchenTickets",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "NotDeducted");

            migrationBuilder.Sql("""
UPDATE kt
SET InventoryDeductionStatus = CASE
    WHEN EXISTS (
        SELECT 1
        FROM KitchenTicketInventoryDeductions kd
        WHERE kd.KitchenTicketId = kt.KitchenTicketId
    ) THEN 'Deducted'
    WHEN kt.[Status] = 'Served' THEN 'DeductionWarning'
    ELSE 'NotDeducted'
END
FROM KitchenTickets kt
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InventoryDeductionStatus",
                table: "KitchenTickets");
        }
    }
}
