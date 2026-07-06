using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillSoft.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CashierShiftFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CashierShiftId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CashierShifts",
                columns: table => new
                {
                    CashierShiftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpenedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OpeningCashAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpectedCashAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CountedCashAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CashVarianceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    OpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OpeningNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ClosingNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashierShifts", x => x.CashierShiftId);
                    table.ForeignKey(
                        name: "FK_CashierShifts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashierShifts_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "RestaurantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashierShifts_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashierShifts_Users_OpenedByUserId",
                        column: x => x.OpenedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashDrawerMovements",
                columns: table => new
                {
                    CashDrawerMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CashierShiftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MovementType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashDrawerMovements", x => x.CashDrawerMovementId);
                    table.ForeignKey(
                        name: "FK_CashDrawerMovements_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashDrawerMovements_CashierShifts_CashierShiftId",
                        column: x => x.CashierShiftId,
                        principalTable: "CashierShifts",
                        principalColumn: "CashierShiftId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CashDrawerMovements_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "RestaurantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashDrawerMovements_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CashierShiftId",
                table: "Payments",
                column: "CashierShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawerMovements_BranchId",
                table: "CashDrawerMovements",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawerMovements_CashierShiftId",
                table: "CashDrawerMovements",
                column: "CashierShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawerMovements_CreatedAt",
                table: "CashDrawerMovements",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawerMovements_CreatedByUserId",
                table: "CashDrawerMovements",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawerMovements_MovementType",
                table: "CashDrawerMovements",
                column: "MovementType");

            migrationBuilder.CreateIndex(
                name: "IX_CashDrawerMovements_RestaurantId_BranchId",
                table: "CashDrawerMovements",
                columns: new[] { "RestaurantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_CashierShifts_BranchId",
                table: "CashierShifts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierShifts_ClosedAt",
                table: "CashierShifts",
                column: "ClosedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CashierShifts_ClosedByUserId",
                table: "CashierShifts",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierShifts_OpenedAt",
                table: "CashierShifts",
                column: "OpenedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CashierShifts_OpenedByUserId",
                table: "CashierShifts",
                column: "OpenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierShifts_RestaurantId",
                table: "CashierShifts",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierShifts_RestaurantId_BranchId_Status",
                table: "CashierShifts",
                columns: new[] { "RestaurantId", "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_CashierShifts_RestaurantId_BranchId_Open",
                table: "CashierShifts",
                columns: new[] { "RestaurantId", "BranchId" },
                unique: true,
                filter: "[Status] = 'Open'");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_CashierShifts_CashierShiftId",
                table: "Payments",
                column: "CashierShiftId",
                principalTable: "CashierShifts",
                principalColumn: "CashierShiftId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_CashierShifts_CashierShiftId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "CashDrawerMovements");

            migrationBuilder.DropTable(
                name: "CashierShifts");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CashierShiftId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CashierShiftId",
                table: "Payments");
        }
    }
}
