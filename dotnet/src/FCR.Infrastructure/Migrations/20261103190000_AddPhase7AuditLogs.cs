using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using FCR.Infrastructure.Persistence;

namespace FCR.Infrastructure.Migrations;

[DbContext(typeof(FcrDbContext))]
[Migration("20261103190000_AddPhase7AuditLogs")]
public sealed class AddPhase7AuditLogs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "fcr_audit_logs",
            columns: table => new
            {
                id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                user_id = table.Column<long>(type: "INTEGER", nullable: true),
                username = table.Column<string>(type: "TEXT", nullable: true),
                action = table.Column<string>(type: "TEXT", nullable: false),
                http_method = table.Column<string>(type: "TEXT", nullable: false),
                path = table.Column<string>(type: "TEXT", nullable: false),
                status_code = table.Column<int>(type: "INTEGER", nullable: false),
                request_id = table.Column<string>(type: "TEXT", nullable: true),
                created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_fcr_audit_logs", x => x.id));

        migrationBuilder.CreateIndex(
            name: "idx_audit_created_at",
            table: "fcr_audit_logs",
            column: "created_at_utc");
        migrationBuilder.CreateIndex(
            name: "idx_audit_user_created_at",
            table: "fcr_audit_logs",
            columns: new[] { "user_id", "created_at_utc" });
        migrationBuilder.CreateIndex(
            name: "idx_audit_request",
            table: "fcr_audit_logs",
            column: "request_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "fcr_audit_logs");
    }
}
