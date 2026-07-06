using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillSoft.Infrastructure.Migrations;

[DbContext(typeof(BillSoftDbContext))]
[Migration("20260619000000_PilotCurrencyDefaults")]
public partial class PilotCurrencyDefaults : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE r
            SET [CountryCode] = 'IN',
                [CurrencyCode] = 'INR',
                [TimeZoneId] = 'Asia/Kolkata'
            FROM [Restaurants] AS r
            WHERE UPPER(LTRIM(RTRIM(r.[RestaurantCode]))) = 'DEMO';

            UPDATE b
            SET [CountryCode] = 'IN',
                [CurrencyCode] = 'INR',
                [TimeZoneId] = 'Asia/Kolkata'
            FROM [Branches] AS b
            INNER JOIN [Restaurants] AS r ON r.[RestaurantId] = b.[RestaurantId]
            WHERE UPPER(LTRIM(RTRIM(r.[RestaurantCode]))) = 'DEMO';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE r
            SET [CountryCode] = 'SG',
                [CurrencyCode] = 'SGD',
                [TimeZoneId] = 'Asia/Singapore'
            FROM [Restaurants] AS r
            WHERE UPPER(LTRIM(RTRIM(r.[RestaurantCode]))) = 'DEMO';

            UPDATE b
            SET [CountryCode] = 'SG',
                [CurrencyCode] = 'SGD',
                [TimeZoneId] = 'Asia/Singapore'
            FROM [Branches] AS b
            INNER JOIN [Restaurants] AS r ON r.[RestaurantId] = b.[RestaurantId]
            WHERE UPPER(LTRIM(RTRIM(r.[RestaurantCode]))) = 'DEMO';
            """);
    }
}
