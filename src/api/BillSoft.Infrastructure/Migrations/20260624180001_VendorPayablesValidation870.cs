using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillSoft.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VendorPayablesValidation870 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedMobileNumber",
                table: "Vendors",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedBillNumber",
                table: "VendorBills",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedPhone",
                table: "Branches",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.Sql("""
UPDATE [Vendors]
SET [NormalizedMobileNumber] =
    CASE
        WHEN NULLIF(LTRIM(RTRIM([MobileNumber])), '') IS NULL THEN NULL
        ELSE UPPER(LTRIM(RTRIM([MobileNumber])))
    END;

UPDATE [VendorBills]
SET [NormalizedBillNumber] =
    CASE
        WHEN NULLIF(LTRIM(RTRIM([BillNumber])), '') IS NULL THEN NULL
        ELSE UPPER(LTRIM(RTRIM([BillNumber])))
    END;

UPDATE [Branches]
SET [NormalizedPhone] =
    CASE
        WHEN NULLIF(LTRIM(RTRIM([Phone])), '') IS NULL THEN NULL
        ELSE UPPER(LTRIM(RTRIM([Phone])))
    END;

IF EXISTS (
    SELECT 1
    FROM (
        SELECT [RestaurantId], UPPER(LTRIM(RTRIM([MobileNumber]))) AS [NormalizedMobileNumber]
        FROM [Vendors]
        WHERE NULLIF(LTRIM(RTRIM([MobileNumber])), '') IS NOT NULL
        GROUP BY [RestaurantId], UPPER(LTRIM(RTRIM([MobileNumber])))
        HAVING COUNT(*) > 1
    ) AS [DuplicateVendors]
)
BEGIN
    DECLARE @DuplicateVendorMobileRestaurantId uniqueidentifier;
    DECLARE @DuplicateVendorMobileNumber nvarchar(32);
    DECLARE @DuplicateVendorMobileMessage nvarchar(2048);

    SELECT TOP (1)
        @DuplicateVendorMobileRestaurantId = [RestaurantId],
        @DuplicateVendorMobileNumber = [NormalizedMobileNumber]
    FROM (
        SELECT [RestaurantId], UPPER(LTRIM(RTRIM([MobileNumber]))) AS [NormalizedMobileNumber]
        FROM [Vendors]
        WHERE NULLIF(LTRIM(RTRIM([MobileNumber])), '') IS NOT NULL
        GROUP BY [RestaurantId], UPPER(LTRIM(RTRIM([MobileNumber])))
        HAVING COUNT(*) > 1
    ) AS [DuplicateVendors]
    ORDER BY [RestaurantId], [NormalizedMobileNumber];

    SET @DuplicateVendorMobileMessage = CONCAT(
        'Cannot apply vendor payables backfill because duplicate vendor mobile numbers already exist after normalization in restaurant ',
        CONVERT(nvarchar(36), @DuplicateVendorMobileRestaurantId),
        ' for mobile ',
        @DuplicateVendorMobileNumber,
        '.'
    );

    THROW 50000, @DuplicateVendorMobileMessage, 1;
END;

IF EXISTS (
    SELECT 1
    FROM (
        SELECT [RestaurantId], [VendorId], UPPER(LTRIM(RTRIM([BillNumber]))) AS [NormalizedBillNumber]
        FROM [VendorBills]
        WHERE NULLIF(LTRIM(RTRIM([BillNumber])), '') IS NOT NULL
        GROUP BY [RestaurantId], [VendorId], UPPER(LTRIM(RTRIM([BillNumber])))
        HAVING COUNT(*) > 1
    ) AS [DuplicateVendorBills]
)
BEGIN
    DECLARE @DuplicateVendorBillRestaurantId uniqueidentifier;
    DECLARE @DuplicateVendorBillVendorId uniqueidentifier;
    DECLARE @DuplicateVendorBillNumber nvarchar(40);
    DECLARE @DuplicateVendorBillMessage nvarchar(2048);

    SELECT TOP (1)
        @DuplicateVendorBillRestaurantId = [RestaurantId],
        @DuplicateVendorBillVendorId = [VendorId],
        @DuplicateVendorBillNumber = [NormalizedBillNumber]
    FROM (
        SELECT [RestaurantId], [VendorId], UPPER(LTRIM(RTRIM([BillNumber]))) AS [NormalizedBillNumber]
        FROM [VendorBills]
        WHERE NULLIF(LTRIM(RTRIM([BillNumber])), '') IS NOT NULL
        GROUP BY [RestaurantId], [VendorId], UPPER(LTRIM(RTRIM([BillNumber])))
        HAVING COUNT(*) > 1
    ) AS [DuplicateVendorBills]
    ORDER BY [RestaurantId], [VendorId], [NormalizedBillNumber];

    SET @DuplicateVendorBillMessage = CONCAT(
        'Cannot apply vendor payables backfill because duplicate vendor bill numbers already exist after normalization in restaurant ',
        CONVERT(nvarchar(36), @DuplicateVendorBillRestaurantId),
        ' for vendor ',
        CONVERT(nvarchar(36), @DuplicateVendorBillVendorId),
        ' and bill number ',
        @DuplicateVendorBillNumber,
        '.'
    );

    THROW 50000, @DuplicateVendorBillMessage, 1;
END;

IF EXISTS (
    SELECT 1
    FROM (
        SELECT [RestaurantId], UPPER(LTRIM(RTRIM([Phone]))) AS [NormalizedPhone]
        FROM [Branches]
        WHERE NULLIF(LTRIM(RTRIM([Phone])), '') IS NOT NULL
        GROUP BY [RestaurantId], UPPER(LTRIM(RTRIM([Phone])))
        HAVING COUNT(*) > 1
    ) AS [DuplicateBranches]
)
BEGIN
    DECLARE @DuplicateBranchRestaurantId uniqueidentifier;
    DECLARE @DuplicateBranchPhone nvarchar(32);
    DECLARE @DuplicateBranchMessage nvarchar(2048);

    SELECT TOP (1)
        @DuplicateBranchRestaurantId = [RestaurantId],
        @DuplicateBranchPhone = [NormalizedPhone]
    FROM (
        SELECT [RestaurantId], UPPER(LTRIM(RTRIM([Phone]))) AS [NormalizedPhone]
        FROM [Branches]
        WHERE NULLIF(LTRIM(RTRIM([Phone])), '') IS NOT NULL
        GROUP BY [RestaurantId], UPPER(LTRIM(RTRIM([Phone])))
        HAVING COUNT(*) > 1
    ) AS [DuplicateBranches]
    ORDER BY [RestaurantId], [NormalizedPhone];

    SET @DuplicateBranchMessage = CONCAT(
        'Cannot apply vendor payables backfill because duplicate branch mobile numbers already exist after normalization in restaurant ',
        CONVERT(nvarchar(36), @DuplicateBranchRestaurantId),
        ' for mobile ',
        @DuplicateBranchPhone,
        '.'
    );

    THROW 50000, @DuplicateBranchMessage, 1;
END;
""");

            migrationBuilder.CreateIndex(
                name: "UX_Vendors_RestaurantId_NormalizedMobileNumber",
                table: "Vendors",
                columns: new[] { "RestaurantId", "NormalizedMobileNumber" },
                unique: true,
                filter: "[NormalizedMobileNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_VendorBills_RestaurantId_VendorId_NormalizedBillNumber",
                table: "VendorBills",
                columns: new[] { "RestaurantId", "VendorId", "NormalizedBillNumber" },
                unique: true,
                filter: "[NormalizedBillNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Branches_RestaurantId_NormalizedPhone",
                table: "Branches",
                columns: new[] { "RestaurantId", "NormalizedPhone" },
                unique: true,
                filter: "[NormalizedPhone] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Vendors_RestaurantId_NormalizedMobileNumber",
                table: "Vendors");

            migrationBuilder.DropIndex(
                name: "UX_VendorBills_RestaurantId_VendorId_NormalizedBillNumber",
                table: "VendorBills");

            migrationBuilder.DropIndex(
                name: "UX_Branches_RestaurantId_NormalizedPhone",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "NormalizedMobileNumber",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "NormalizedBillNumber",
                table: "VendorBills");

            migrationBuilder.DropColumn(
                name: "NormalizedPhone",
                table: "Branches");
        }
    }
}
