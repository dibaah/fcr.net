using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FCR.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// DDL is verbatim from the existing fcr_database.db (schema v2) so that a
    /// freshly migrated database reproduces it byte-for-byte — all TEXT columns
    /// nullable, INTEGER PRIMARY KEY without NOT NULL, same index names.
    /// </remarks>
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE fcr (
                    id               INTEGER PRIMARY KEY AUTOINCREMENT,
                    row_num          TEXT,
                    fcr_subject      TEXT,
                    category         TEXT,
                    fcr_code         TEXT,
                    date             TEXT,
                    review_date      TEXT,
                    originator       TEXT,
                    action_party     TEXT,
                    level            TEXT,
                    session_code     TEXT,
                    pedec_status     TEXT,
                    fcr_status       TEXT,
                    execution_status TEXT,
                    closed_date      TEXT,
                    tr               TEXT,
                    next_action_by   TEXT,
                    arch_loc         TEXT,
                    last_update      TEXT,
                    remarks          TEXT,
                    created_at       TEXT DEFAULT (datetime('now')),
                    updated_at       TEXT DEFAULT (datetime('now'))
                );
                """);

            migrationBuilder.Sql("""
                CREATE TABLE import_log (
                    id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    filename    TEXT,
                    row_count   INTEGER,
                    imported_at TEXT DEFAULT (datetime('now'))
                );
                """);

            migrationBuilder.Sql("""
                CREATE TABLE meta (
                    key   TEXT PRIMARY KEY,
                    value TEXT
                );
                """);

            migrationBuilder.Sql("CREATE INDEX idx_fcr_category ON fcr(category);");
            migrationBuilder.Sql("CREATE INDEX idx_fcr_code ON fcr(fcr_code);");
            migrationBuilder.Sql("CREATE INDEX idx_fcr_date ON fcr(date);");
            migrationBuilder.Sql("CREATE INDEX idx_fcr_exec_status ON fcr(execution_status);");
            migrationBuilder.Sql("CREATE INDEX idx_fcr_fcr_status ON fcr(fcr_status);");
            migrationBuilder.Sql("CREATE INDEX idx_fcr_pedec_status ON fcr(pedec_status);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "fcr");
            migrationBuilder.DropTable(name: "import_log");
            migrationBuilder.DropTable(name: "meta");
        }
    }
}
