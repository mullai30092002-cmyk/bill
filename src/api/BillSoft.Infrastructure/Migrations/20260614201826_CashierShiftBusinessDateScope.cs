using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillSoft.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CashierShiftBusinessDateScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_CashierShifts_RestaurantId_BranchId_Open",
                table: "CashierShifts");

            migrationBuilder.AddColumn<DateTime>(
                name: "BusinessDate",
                table: "CashierShifts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE CashierShifts
                SET BusinessDate = CONVERT(date, OpenedAt)
                WHERE BusinessDate IS NULL
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "BusinessDate",
                table: "CashierShifts",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashierShifts_RestaurantId_BranchId_BusinessDate",
                table: "CashierShifts",
                columns: new[] { "RestaurantId", "BranchId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "UX_CashierShifts_RestaurantId_BranchId_OpenedByUserId_Open",
                table: "CashierShifts",
                columns: new[] { "RestaurantId", "BranchId", "OpenedByUserId" },
                unique: true,
                filter: "[Status] = 'Open'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CashierShifts_RestaurantId_BranchId_BusinessDate",
                table: "CashierShifts");

            migrationBuilder.DropIndex(
                name: "UX_CashierShifts_RestaurantId_BranchId_OpenedByUserId_Open",
                table: "CashierShifts");

            migrationBuilder.DropColumn(
                name: "BusinessDate",
                table: "CashierShifts");

            migrationBuilder.CreateIndex(
                name: "UX_CashierShifts_RestaurantId_BranchId_Open",
                table: "CashierShifts",
                columns: new[] { "RestaurantId", "BranchId" },
                unique: true,
                filter: "[Status] = 'Open'");
        }
    }
}
