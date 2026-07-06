using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillSoft.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BatchProductionPreparedStockDeduction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InventoryDeductionMode",
                table: "MenuItems",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "RecipeOnServe");

            migrationBuilder.CreateTable(
                name: "BatchProductions",
                columns: table => new
                {
                    BatchProductionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreparedInventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuantityProduced = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    BusinessDate = table.Column<DateTime>(type: "date", nullable: false),
                    ProducedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProducedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PreparedInventoryMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatchProductions", x => x.BatchProductionId);
                    table.ForeignKey(
                        name: "FK_BatchProductions_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BatchProductions_InventoryItems_PreparedInventoryItemId",
                        column: x => x.PreparedInventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "InventoryItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BatchProductions_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "MenuItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BatchProductions_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "RestaurantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BatchProductions_Users_ProducedByUserId",
                        column: x => x.ProducedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MenuItemStockItems",
                columns: table => new
                {
                    MenuItemStockItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemStockItems", x => x.MenuItemStockItemId);
                    table.ForeignKey(
                        name: "FK_MenuItemStockItems_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuItemStockItems_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "InventoryItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuItemStockItems_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "MenuItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuItemStockItems_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "RestaurantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BatchProductionIngredientConsumptions",
                columns: table => new
                {
                    BatchProductionIngredientConsumptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchProductionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    QuantityConsumed = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatchProductionIngredientConsumptions", x => x.BatchProductionIngredientConsumptionId);
                    table.ForeignKey(
                        name: "FK_BatchProductionIngredientConsumptions_BatchProductions_BatchProductionId",
                        column: x => x.BatchProductionId,
                        principalTable: "BatchProductions",
                        principalColumn: "BatchProductionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BatchProductionIngredientConsumptions_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BatchProductionIngredientConsumptions_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "InventoryItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BatchProductionIngredientConsumptions_InventoryMovements_InventoryMovementId",
                        column: x => x.InventoryMovementId,
                        principalTable: "InventoryMovements",
                        principalColumn: "InventoryMovementId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BatchProductionIngredientConsumptions_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "RestaurantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BatchProductionIngredientConsumptions_BatchProductionId",
                table: "BatchProductionIngredientConsumptions",
                column: "BatchProductionId");

            migrationBuilder.CreateIndex(
                name: "IX_BatchProductionIngredientConsumptions_BranchId",
                table: "BatchProductionIngredientConsumptions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_BatchProductionIngredientConsumptions_InventoryItemId",
                table: "BatchProductionIngredientConsumptions",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_BatchProductionIngredientConsumptions_InventoryMovementId",
                table: "BatchProductionIngredientConsumptions",
                column: "InventoryMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_BatchProductionIngredientConsumptions_RestaurantId",
                table: "BatchProductionIngredientConsumptions",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "IX_BatchProductionIngredientConsumptions_RestaurantId_BranchId",
                table: "BatchProductionIngredientConsumptions",
                columns: new[] { "RestaurantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "UX_BatchProductionIngredientConsumptions_RestaurantId_BatchProductionId_InventoryItemId",
                table: "BatchProductionIngredientConsumptions",
                columns: new[] { "RestaurantId", "BatchProductionId", "InventoryItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BatchProductions_BranchId",
                table: "BatchProductions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_BatchProductions_MenuItemId",
                table: "BatchProductions",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_BatchProductions_PreparedInventoryItemId",
                table: "BatchProductions",
                column: "PreparedInventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_BatchProductions_ProducedByUserId",
                table: "BatchProductions",
                column: "ProducedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BatchProductions_RestaurantId",
                table: "BatchProductions",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "IX_BatchProductions_RestaurantId_BranchId_BusinessDate",
                table: "BatchProductions",
                columns: new[] { "RestaurantId", "BranchId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BatchProductions_RestaurantId_BranchId_MenuItemId_BusinessDate",
                table: "BatchProductions",
                columns: new[] { "RestaurantId", "BranchId", "MenuItemId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemStockItems_BranchId",
                table: "MenuItemStockItems",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemStockItems_InventoryItemId",
                table: "MenuItemStockItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemStockItems_MenuItemId",
                table: "MenuItemStockItems",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemStockItems_RestaurantId",
                table: "MenuItemStockItems",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemStockItems_RestaurantId_BranchId",
                table: "MenuItemStockItems",
                columns: new[] { "RestaurantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "UX_MenuItemStockItems_RestaurantId_BranchId_MenuItemId",
                table: "MenuItemStockItems",
                columns: new[] { "RestaurantId", "BranchId", "MenuItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BatchProductionIngredientConsumptions");

            migrationBuilder.DropTable(
                name: "MenuItemStockItems");

            migrationBuilder.DropTable(
                name: "BatchProductions");

            migrationBuilder.DropColumn(
                name: "InventoryDeductionMode",
                table: "MenuItems");
        }
    }
}
