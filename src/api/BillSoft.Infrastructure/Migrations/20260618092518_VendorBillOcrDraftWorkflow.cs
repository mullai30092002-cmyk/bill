using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillSoft.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VendorBillOcrDraftWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VendorBillOcrDrafts",
                columns: table => new
                {
                    VendorBillOcrDraftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredFilePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExtractedVendorName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    ExtractedBillNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ExtractedBillDate = table.Column<DateTime>(type: "date", nullable: true),
                    ExtractedTotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ExtractedConfidenceScore = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    ReviewedVendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedBillNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ReviewedBillDate = table.Column<DateTime>(type: "date", nullable: true),
                    ReviewedTotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SafeErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ConfirmedVendorBillId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfirmedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorBillOcrDrafts", x => x.VendorBillOcrDraftId);
                    table.ForeignKey(
                        name: "FK_VendorBillOcrDrafts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorBillOcrDrafts_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "RestaurantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorBillOcrDrafts_Users_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorBillOcrDrafts_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VendorBillOcrDraftLines",
                columns: table => new
                {
                    VendorBillOcrDraftLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorBillOcrDraftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    ExtractedDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ExtractedQuantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ExtractedUnitCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ExtractedLineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ConfidenceScore = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    SelectedInventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ReviewedQuantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ReviewedUnitCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ReviewedLineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorBillOcrDraftLines", x => x.VendorBillOcrDraftLineId);
                    table.ForeignKey(
                        name: "FK_VendorBillOcrDraftLines_InventoryItems_SelectedInventoryItemId",
                        column: x => x.SelectedInventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "InventoryItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorBillOcrDraftLines_VendorBillOcrDrafts_VendorBillOcrDraftId",
                        column: x => x.VendorBillOcrDraftId,
                        principalTable: "VendorBillOcrDrafts",
                        principalColumn: "VendorBillOcrDraftId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VendorBillOcrDraftLines_RestaurantId",
                table: "VendorBillOcrDraftLines",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorBillOcrDraftLines_RestaurantId_BranchId_DraftId",
                table: "VendorBillOcrDraftLines",
                columns: new[] { "RestaurantId", "BranchId", "VendorBillOcrDraftId" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorBillOcrDraftLines_SelectedInventoryItemId",
                table: "VendorBillOcrDraftLines",
                column: "SelectedInventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorBillOcrDraftLines_VendorBillOcrDraftId",
                table: "VendorBillOcrDraftLines",
                column: "VendorBillOcrDraftId");

            migrationBuilder.CreateIndex(
                name: "UX_VendorBillOcrDraftLines_RestaurantId_DraftId_LineNumber",
                table: "VendorBillOcrDraftLines",
                columns: new[] { "RestaurantId", "VendorBillOcrDraftId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorBillOcrDrafts_BranchId",
                table: "VendorBillOcrDrafts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorBillOcrDrafts_ConfirmedByUserId",
                table: "VendorBillOcrDrafts",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorBillOcrDrafts_RestaurantId",
                table: "VendorBillOcrDrafts",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorBillOcrDrafts_RestaurantId_BranchId_Status",
                table: "VendorBillOcrDrafts",
                columns: new[] { "RestaurantId", "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorBillOcrDrafts_RestaurantId_CreatedAtUtc",
                table: "VendorBillOcrDrafts",
                columns: new[] { "RestaurantId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorBillOcrDrafts_UploadedByUserId",
                table: "VendorBillOcrDrafts",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_VendorBillOcrDrafts_ConfirmedVendorBillId",
                table: "VendorBillOcrDrafts",
                column: "ConfirmedVendorBillId",
                unique: true,
                filter: "[ConfirmedVendorBillId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VendorBillOcrDraftLines");

            migrationBuilder.DropTable(
                name: "VendorBillOcrDrafts");
        }
    }
}
