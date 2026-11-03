using FCR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FCR.Infrastructure.Migrations;

[DbContext(typeof(FcrDbContext))]
[Migration("20261103200000_AddFcrAdminMetadata")]
public sealed class AddFcrAdminMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "contractor", table: "fcr_requests", type: "TEXT",
            nullable: false, defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "subcontractor", table: "fcr_requests", type: "TEXT",
            nullable: false, defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "contractor", table: "fcr_requests");
        migrationBuilder.DropColumn(name: "subcontractor", table: "fcr_requests");
    }
}
