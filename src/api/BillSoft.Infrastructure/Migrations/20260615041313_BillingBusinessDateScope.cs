using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillSoft.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BillingBusinessDateScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BusinessDate",
                table: "Bills",
                type: "date",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE Bills
                SET BusinessDate = CONVERT(date, CreatedAt)
                WHERE BusinessDate IS NULL
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "BusinessDate",
                table: "Bills",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bills_RestaurantId_BranchId_BusinessDate",
                table: "Bills",
                columns: new[] { "RestaurantId", "BranchId", "BusinessDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bills_RestaurantId_BranchId_BusinessDate",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "BusinessDate",
                table: "Bills");
        }
    }
}
