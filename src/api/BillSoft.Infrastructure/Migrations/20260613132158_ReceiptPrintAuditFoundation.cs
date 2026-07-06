using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillSoft.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReceiptPrintAuditFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BillPrintEvents",
                columns: table => new
                {
                    BillPrintEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrintedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PrintSequence = table.Column<int>(type: "int", nullable: false),
                    PrintReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillPrintEvents", x => x.BillPrintEventId);
                    table.ForeignKey(
                        name: "FK_BillPrintEvents_Bills_BillId",
                        column: x => x.BillId,
                        principalTable: "Bills",
                        principalColumn: "BillId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BillPrintEvents_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BillPrintEvents_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "RestaurantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BillPrintEvents_Users_PrintedByUserId",
                        column: x => x.PrintedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillPrintEvents_BillId",
                table: "BillPrintEvents",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_BillPrintEvents_BranchId",
                table: "BillPrintEvents",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_BillPrintEvents_CreatedAt",
                table: "BillPrintEvents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BillPrintEvents_PrintedByUserId",
                table: "BillPrintEvents",
                column: "PrintedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BillPrintEvents_RestaurantId",
                table: "BillPrintEvents",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "UX_BillPrintEvents_BillId_PrintSequence",
                table: "BillPrintEvents",
                columns: new[] { "BillId", "PrintSequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillPrintEvents");
        }
    }
}
