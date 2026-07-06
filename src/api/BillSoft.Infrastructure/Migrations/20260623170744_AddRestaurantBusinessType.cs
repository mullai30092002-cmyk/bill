using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillSoft.Infrastructure.Migrations;

[DbContext(typeof(BillSoftDbContext))]
[Migration("20260623170744_AddRestaurantBusinessType")]
public partial class AddRestaurantBusinessType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BusinessType",
            table: "Restaurants",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "Restaurant");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "BusinessType",
            table: "Restaurants");
    }
}
