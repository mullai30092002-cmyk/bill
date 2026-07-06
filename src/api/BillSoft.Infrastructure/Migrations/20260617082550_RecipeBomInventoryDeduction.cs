using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillSoft.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RecipeBomInventoryDeduction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KitchenTicketInventoryDeductions",
                columns: table => new
                {
                    KitchenTicketInventoryDeductionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KitchenTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuantityDeducted = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitchenTicketInventoryDeductions", x => x.KitchenTicketInventoryDeductionId);
                    table.ForeignKey(
                        name: "FK_KitchenTicketInventoryDeductions_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KitchenTicketInventoryDeductions_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "InventoryItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KitchenTicketInventoryDeductions_InventoryMovements_InventoryMovementId",
                        column: x => x.InventoryMovementId,
                        principalTable: "InventoryMovements",
                        principalColumn: "InventoryMovementId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KitchenTicketInventoryDeductions_KitchenTickets_KitchenTicketId",
                        column: x => x.KitchenTicketId,
                        principalTable: "KitchenTickets",
                        principalColumn: "KitchenTicketId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KitchenTicketInventoryDeductions_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "RestaurantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MenuItemRecipeIngredients",
                columns: table => new
                {
                    MenuItemRecipeIngredientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuantityRequired = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemRecipeIngredients", x => x.MenuItemRecipeIngredientId);
                    table.ForeignKey(
                        name: "FK_MenuItemRecipeIngredients_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuItemRecipeIngredients_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "InventoryItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuItemRecipeIngredients_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "MenuItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuItemRecipeIngredients_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "RestaurantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KitchenTicketInventoryDeductions_BranchId",
                table: "KitchenTicketInventoryDeductions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenTicketInventoryDeductions_InventoryItemId",
                table: "KitchenTicketInventoryDeductions",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenTicketInventoryDeductions_InventoryMovementId",
                table: "KitchenTicketInventoryDeductions",
                column: "InventoryMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenTicketInventoryDeductions_KitchenTicketId",
                table: "KitchenTicketInventoryDeductions",
                column: "KitchenTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenTicketInventoryDeductions_RestaurantId",
                table: "KitchenTicketInventoryDeductions",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenTicketInventoryDeductions_RestaurantId_BranchId",
                table: "KitchenTicketInventoryDeductions",
                columns: new[] { "RestaurantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "UX_KitchenTicketInventoryDeductions_RestaurantId_KitchenTicketId_InventoryItemId",
                table: "KitchenTicketInventoryDeductions",
                columns: new[] { "RestaurantId", "KitchenTicketId", "InventoryItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemRecipeIngredients_BranchId",
                table: "MenuItemRecipeIngredients",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemRecipeIngredients_InventoryItemId",
                table: "MenuItemRecipeIngredients",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemRecipeIngredients_MenuItemId",
                table: "MenuItemRecipeIngredients",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemRecipeIngredients_RestaurantId",
                table: "MenuItemRecipeIngredients",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemRecipeIngredients_RestaurantId_BranchId",
                table: "MenuItemRecipeIngredients",
                columns: new[] { "RestaurantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemRecipeIngredients_RestaurantId_BranchId_MenuItemId",
                table: "MenuItemRecipeIngredients",
                columns: new[] { "RestaurantId", "BranchId", "MenuItemId" });

            migrationBuilder.CreateIndex(
                name: "UX_MenuItemRecipeIngredients_RestaurantId_BranchId_MenuItemId_InventoryItemId",
                table: "MenuItemRecipeIngredients",
                columns: new[] { "RestaurantId", "BranchId", "MenuItemId", "InventoryItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KitchenTicketInventoryDeductions");

            migrationBuilder.DropTable(
                name: "MenuItemRecipeIngredients");
        }
    }
}
