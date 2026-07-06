using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillSoft.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpiryShelfLifeTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BatchReference",
                table: "InventoryMovements",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAtUtc",
                table: "InventoryMovements",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatchReference",
                table: "BatchProductions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAtUtc",
                table: "BatchProductions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShelfLifeHours",
                table: "BatchProductions",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageNote",
                table: "BatchProductions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BatchReference",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "BatchReference",
                table: "BatchProductions");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "BatchProductions");

            migrationBuilder.DropColumn(
                name: "ShelfLifeHours",
                table: "BatchProductions");

            migrationBuilder.DropColumn(
                name: "StorageNote",
                table: "BatchProductions");
        }
    }
}
