using FCR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FCR.Infrastructure.Migrations;

[DbContext(typeof(FcrDbContext))]
[Migration("20261103193000_AddExternalIdentityMapping")]
public sealed partial class AddExternalIdentityMapping : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "external_id",
            table: "users",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "idx_users_external_id",
            table: "users",
            column: "external_id",
            unique: true,
            filter: "external_id IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "idx_users_external_id", table: "users");
        migrationBuilder.DropColumn(name: "external_id", table: "users");
    }
}
