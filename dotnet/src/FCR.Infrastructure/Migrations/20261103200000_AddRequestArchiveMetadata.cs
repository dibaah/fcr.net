using FCR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FCR.Infrastructure.Migrations;

[DbContext(typeof(FcrDbContext))]
[Migration("20261103200000_AddRequestArchiveMetadata")]
public partial class AddRequestArchiveMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "contractor",
            table: "fcr_requests",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "subcontractor",
            table: "fcr_requests",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "archive_path",
            table: "fcr_requests",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "archived_at",
            table: "fcr_requests",
            type: "TEXT",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "contractor", table: "fcr_requests");
        migrationBuilder.DropColumn(name: "subcontractor", table: "fcr_requests");
        migrationBuilder.DropColumn(name: "archive_path", table: "fcr_requests");
        migrationBuilder.DropColumn(name: "archived_at", table: "fcr_requests");
    }
}
