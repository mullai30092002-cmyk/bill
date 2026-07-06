using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillSoft.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CountryCurrencyMobileFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Users_RestaurantId_MobileNumber",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "Restaurants",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "SG");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "Restaurants",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "SGD");

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "Restaurants",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "Asia/Singapore");

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "Branches",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "SG");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "Branches",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "SGD");

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "Branches",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "Asia/Singapore");

            migrationBuilder.AddColumn<string>(
                name: "MobileCountryCode",
                table: "Users",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "SG");

            migrationBuilder.AddColumn<string>(
                name: "MobileDialCode",
                table: "Users",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: false,
                defaultValue: "+65");

            migrationBuilder.AddColumn<string>(
                name: "MobileE164",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MobileNationalNumber",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE [Restaurants]
                SET [CountryCode] = CASE WHEN [CountryCode] IS NULL OR LTRIM(RTRIM([CountryCode])) = '' THEN 'SG' ELSE UPPER(LTRIM(RTRIM([CountryCode]))) END,
                    [CurrencyCode] = CASE WHEN [CurrencyCode] IS NULL OR LTRIM(RTRIM([CurrencyCode])) = '' THEN 'SGD' ELSE UPPER(LTRIM(RTRIM([CurrencyCode]))) END,
                    [TimeZoneId] = CASE WHEN [TimeZoneId] IS NULL OR LTRIM(RTRIM([TimeZoneId])) = '' THEN 'Asia/Singapore' ELSE LTRIM(RTRIM([TimeZoneId])) END;

                UPDATE [Branches]
                SET [CountryCode] = CASE WHEN [CountryCode] IS NULL OR LTRIM(RTRIM([CountryCode])) = '' THEN 'SG' ELSE UPPER(LTRIM(RTRIM([CountryCode]))) END,
                    [CurrencyCode] = CASE
                        WHEN [CurrencyCode] IS NULL OR LTRIM(RTRIM([CurrencyCode])) = '' THEN
                            CASE WHEN [Currency] IS NULL OR LTRIM(RTRIM([Currency])) = '' THEN 'SGD' ELSE UPPER(LTRIM(RTRIM([Currency]))) END
                        ELSE UPPER(LTRIM(RTRIM([CurrencyCode])))
                    END,
                    [TimeZoneId] = CASE
                        WHEN [TimeZoneId] IS NULL OR LTRIM(RTRIM([TimeZoneId])) = '' THEN
                            CASE WHEN [Timezone] IS NULL OR LTRIM(RTRIM([Timezone])) = '' THEN 'Asia/Singapore' ELSE LTRIM(RTRIM([Timezone])) END
                        ELSE LTRIM(RTRIM([TimeZoneId]))
                    END;

                UPDATE [Users]
                SET [MobileCountryCode] = CASE
                        WHEN REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', '') LIKE '91%' AND LEN(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', '')) = 12 THEN 'IN'
                        WHEN REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', '') LIKE '0%' AND LEN(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', '')) = 11 THEN 'IN'
                        ELSE 'SG'
                    END,
                    [MobileDialCode] = CASE
                        WHEN REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', '') LIKE '91%' AND LEN(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', '')) = 12 THEN '+91'
                        WHEN REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', '') LIKE '0%' AND LEN(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', '')) = 11 THEN '+91'
                        ELSE '+65'
                    END,
                    [MobileNationalNumber] = CASE
                        WHEN REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', '') LIKE '65%' AND LEN(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', '')) = 10 THEN RIGHT(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', ''), 8)
                        WHEN REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', '') LIKE '91%' AND LEN(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', '')) = 12 THEN RIGHT(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', ''), 10)
                        WHEN REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', '') LIKE '0%' AND LEN(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', '')) = 11 THEN RIGHT(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', ''), 10)
                        ELSE REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', '')
                    END,
                    [MobileE164] = CASE
                        WHEN REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', '') LIKE '91%' AND LEN(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', '')) = 12 THEN '+91' + RIGHT(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', ''), 10)
                        WHEN REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', '') LIKE '0%' AND LEN(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', '')) = 11 THEN '+91' + RIGHT(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', ''), 10)
                        ELSE '+65' + CASE
                            WHEN REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', '') LIKE '65%' AND LEN(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', '')) = 10 THEN RIGHT(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', ''), 8)
                            ELSE REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([MobileNumber])), '+', ''), ' ', ''), '-', ''), '(', ''), ')', '')
                        END
                    END;
                """);

            migrationBuilder.CreateIndex(
                name: "UX_Users_RestaurantId_MobileE164",
                table: "Users",
                columns: new[] { "RestaurantId", "MobileE164" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Users_RestaurantId_MobileE164",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MobileCountryCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MobileDialCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MobileE164",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MobileNationalNumber",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "Branches");

            migrationBuilder.CreateIndex(
                name: "UX_Users_RestaurantId_MobileNumber",
                table: "Users",
                columns: new[] { "RestaurantId", "MobileNumber" },
                unique: true);
        }
    }
}
