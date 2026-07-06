using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillSoft.Infrastructure.Migrations;

[Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(BillSoftDbContext))]
[Migration("20260615000000_DropLegacyBranchCurrencyTimezoneColumns")]
public partial class DropLegacyBranchCurrencyTimezoneColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Currency",
            table: "Branches");

        migrationBuilder.DropColumn(
            name: "Timezone",
            table: "Branches");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Currency",
            table: "Branches",
            type: "nvarchar(3)",
            maxLength: 3,
            nullable: false,
            defaultValue: "SGD");

        migrationBuilder.AddColumn<string>(
            name: "Timezone",
            table: "Branches",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "Asia/Singapore");
    }
}
