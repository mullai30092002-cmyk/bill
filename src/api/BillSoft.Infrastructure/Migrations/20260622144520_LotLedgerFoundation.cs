using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillSoft.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LotLedgerFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryLots",
                columns: table => new
                {
                    InventoryLotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceBatchProductionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BatchReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    InitialQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryLots", x => x.InventoryLotId);
                    table.ForeignKey(
                        name: "FK_InventoryLots_BatchProductions_SourceBatchProductionId",
                        column: x => x.SourceBatchProductionId,
                        principalTable: "BatchProductions",
                        principalColumn: "BatchProductionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLots_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLots_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "InventoryItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLots_InventoryMovements_SourceMovementId",
                        column: x => x.SourceMovementId,
                        principalTable: "InventoryMovements",
                        principalColumn: "InventoryMovementId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLots_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "RestaurantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryLotAllocations",
                columns: table => new
                {
                    InventoryLotAllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryLotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuantityAllocated = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AllocationReason = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryLotAllocations", x => x.InventoryLotAllocationId);
                    table.ForeignKey(
                        name: "FK_InventoryLotAllocations_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLotAllocations_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "InventoryItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLotAllocations_InventoryLots_InventoryLotId",
                        column: x => x.InventoryLotId,
                        principalTable: "InventoryLots",
                        principalColumn: "InventoryLotId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLotAllocations_InventoryMovements_InventoryMovementId",
                        column: x => x.InventoryMovementId,
                        principalTable: "InventoryMovements",
                        principalColumn: "InventoryMovementId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLotAllocations_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "RestaurantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLotAllocations_BranchId",
                table: "InventoryLotAllocations",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLotAllocations_InventoryItemId",
                table: "InventoryLotAllocations",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLotAllocations_InventoryLotId",
                table: "InventoryLotAllocations",
                column: "InventoryLotId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLotAllocations_InventoryMovementId",
                table: "InventoryLotAllocations",
                column: "InventoryMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLotAllocations_RestaurantId_BranchId_InventoryItemId",
                table: "InventoryLotAllocations",
                columns: new[] { "RestaurantId", "BranchId", "InventoryItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_BranchId",
                table: "InventoryLots",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_InventoryItemId",
                table: "InventoryLots",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_RestaurantId_BranchId_InventoryItemId",
                table: "InventoryLots",
                columns: new[] { "RestaurantId", "BranchId", "InventoryItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_RestaurantId_BranchId_InventoryItemId_BatchReference",
                table: "InventoryLots",
                columns: new[] { "RestaurantId", "BranchId", "InventoryItemId", "BatchReference" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_RestaurantId_BranchId_InventoryItemId_ExpiresAtUtc",
                table: "InventoryLots",
                columns: new[] { "RestaurantId", "BranchId", "InventoryItemId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_SourceBatchProductionId",
                table: "InventoryLots",
                column: "SourceBatchProductionId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_SourceMovementId",
                table: "InventoryLots",
                column: "SourceMovementId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryLotAllocations");

            migrationBuilder.DropTable(
                name: "InventoryLots");
        }
    }
}
