using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillSoft.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KitchenTicketFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KitchenTicketNumberSequences",
                columns: table => new
                {
                    KitchenTicketNumberSequenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketDate = table.Column<DateTime>(type: "date", nullable: false),
                    LastSequence = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitchenTicketNumberSequences", x => x.KitchenTicketNumberSequenceId);
                    table.ForeignKey(
                        name: "FK_KitchenTicketNumberSequences_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KitchenTicketNumberSequences_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "RestaurantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KitchenTickets",
                columns: table => new
                {
                    KitchenTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PosOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OrderNumberSnapshot = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OrderTypeSnapshot = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastStatusChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PreparingAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReadyAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ServedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitchenTickets", x => x.KitchenTicketId);
                    table.ForeignKey(
                        name: "FK_KitchenTickets_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KitchenTickets_PosOrders_PosOrderId",
                        column: x => x.PosOrderId,
                        principalTable: "PosOrders",
                        principalColumn: "PosOrderId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KitchenTickets_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "RestaurantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KitchenTickets_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KitchenTickets_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KitchenTickets_Users_LastStatusChangedByUserId",
                        column: x => x.LastStatusChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KitchenTicketLines",
                columns: table => new
                {
                    KitchenTicketLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KitchenTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PosOrderLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuItemNameSnapshot = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    MenuCategoryNameSnapshot = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SkuSnapshot = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitchenTicketLines", x => x.KitchenTicketLineId);
                    table.ForeignKey(
                        name: "FK_KitchenTicketLines_KitchenTickets_KitchenTicketId",
                        column: x => x.KitchenTicketId,
                        principalTable: "KitchenTickets",
                        principalColumn: "KitchenTicketId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KitchenTicketLines_MenuCategories_MenuCategoryId",
                        column: x => x.MenuCategoryId,
                        principalTable: "MenuCategories",
                        principalColumn: "MenuCategoryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KitchenTicketLines_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "MenuItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KitchenTicketLines_PosOrderLines_PosOrderLineId",
                        column: x => x.PosOrderLineId,
                        principalTable: "PosOrderLines",
                        principalColumn: "PosOrderLineId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KitchenTicketLines_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "RestaurantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KitchenTicketLines_KitchenTicketId",
                table: "KitchenTicketLines",
                column: "KitchenTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenTicketLines_MenuCategoryId",
                table: "KitchenTicketLines",
                column: "MenuCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenTicketLines_MenuItemId",
                table: "KitchenTicketLines",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenTicketLines_PosOrderLineId",
                table: "KitchenTicketLines",
                column: "PosOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenTicketLines_RestaurantId",
                table: "KitchenTicketLines",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenTicketNumberSequences_BranchId",
                table: "KitchenTicketNumberSequences",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "UX_KitchenTicketNumberSequences_RestaurantId_BranchId_TicketDate",
                table: "KitchenTicketNumberSequences",
                columns: new[] { "RestaurantId", "BranchId", "TicketDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KitchenTickets_BranchId",
                table: "KitchenTickets",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenTickets_CancelledByUserId",
                table: "KitchenTickets",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenTickets_CreatedAt",
                table: "KitchenTickets",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenTickets_CreatedByUserId",
                table: "KitchenTickets",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenTickets_LastStatusChangedByUserId",
                table: "KitchenTickets",
                column: "LastStatusChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenTickets_RestaurantId",
                table: "KitchenTickets",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenTickets_RestaurantId_BranchId",
                table: "KitchenTickets",
                columns: new[] { "RestaurantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_KitchenTickets_RestaurantId_Status",
                table: "KitchenTickets",
                columns: new[] { "RestaurantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_KitchenTickets_PosOrderId_Active",
                table: "KitchenTickets",
                column: "PosOrderId",
                unique: true,
                filter: "[Status] <> 'Cancelled'");

            migrationBuilder.CreateIndex(
                name: "UX_KitchenTickets_RestaurantId_BranchId_TicketNumber",
                table: "KitchenTickets",
                columns: new[] { "RestaurantId", "BranchId", "TicketNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KitchenTicketLines");

            migrationBuilder.DropTable(
                name: "KitchenTicketNumberSequences");

            migrationBuilder.DropTable(
                name: "KitchenTickets");
        }
    }
}
