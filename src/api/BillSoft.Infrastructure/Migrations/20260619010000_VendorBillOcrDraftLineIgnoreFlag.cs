using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillSoft.Infrastructure.Migrations;

[DbContext(typeof(BillSoftDbContext))]
[Migration("20260619010000_VendorBillOcrDraftLineIgnoreFlag")]
public partial class VendorBillOcrDraftLineIgnoreFlag : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsIgnored",
            table: "VendorBillOcrDraftLines",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsIgnored",
            table: "VendorBillOcrDraftLines");
    }
}
