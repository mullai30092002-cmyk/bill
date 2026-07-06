using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillSoft.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IdentityFoundationCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedEmail",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedRestaurantCode",
                table: "Restaurants",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestaurantCode",
                table: "Restaurants",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BranchNameSnapshot",
                table: "AuditLogs",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestaurantNameSnapshot",
                table: "AuditLogs",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserMobileSnapshot",
                table: "AuditLogs",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserNameSnapshot",
                table: "AuditLogs",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [Restaurants]
                SET [RestaurantCode] = UPPER(REPLACE(CONVERT(nvarchar(36), [RestaurantId]), '-', '')),
                    [NormalizedRestaurantCode] = UPPER(REPLACE(CONVERT(nvarchar(36), [RestaurantId]), '-', ''))
                WHERE [RestaurantCode] IS NULL
                   OR [RestaurantCode] = ''
                   OR [NormalizedRestaurantCode] IS NULL
                   OR [NormalizedRestaurantCode] = '';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedRestaurantCode",
                table: "Restaurants",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RestaurantCode",
                table: "Restaurants",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Users_RestaurantId_NormalizedEmail",
                table: "Users",
                columns: new[] { "RestaurantId", "NormalizedEmail" },
                unique: true,
                filter: "[NormalizedEmail] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Restaurants_NormalizedRestaurantCode",
                table: "Restaurants",
                column: "NormalizedRestaurantCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Users_RestaurantId_NormalizedEmail",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "UX_Restaurants_NormalizedRestaurantCode",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "NormalizedEmail",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NormalizedRestaurantCode",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "RestaurantCode",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "BranchNameSnapshot",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "RestaurantNameSnapshot",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "UserMobileSnapshot",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "UserNameSnapshot",
                table: "AuditLogs");
        }
    }
}
