using FCR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FCR.Infrastructure.Migrations;

[DbContext(typeof(FcrDbContext))]
[Migration("20261103201000_AddRequestArchiveState")]
public sealed class AddRequestArchiveState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "archive_path", table: "fcr_requests", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "archived_at", table: "fcr_requests", type: "TEXT", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "archive_path", table: "fcr_requests");
        migrationBuilder.DropColumn(name: "archived_at", table: "fcr_requests");
    }
}
