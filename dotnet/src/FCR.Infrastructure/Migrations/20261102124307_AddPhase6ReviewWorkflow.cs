using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FCR.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase6ReviewWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "cycle",
                table: "fcr_workflow_instances",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "cycle",
                table: "fcr_review_assignments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "fcr_notifications",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    user_id = table.Column<long>(type: "INTEGER", nullable: false),
                    notification_type = table.Column<string>(type: "TEXT", nullable: false),
                    message = table.Column<string>(type: "TEXT", nullable: false),
                    fcr_request_id = table.Column<string>(type: "TEXT", nullable: true),
                    is_read = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fcr_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_fcr_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fcr_reviews",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    fcr_request_id = table.Column<string>(type: "TEXT", nullable: false),
                    workflow_instance_id = table.Column<long>(type: "INTEGER", nullable: false),
                    review_assignment_id = table.Column<long>(type: "INTEGER", nullable: true),
                    reviewer_user_id = table.Column<long>(type: "INTEGER", nullable: false),
                    reviewer_name = table.Column<string>(type: "TEXT", nullable: false),
                    stage_name = table.Column<string>(type: "TEXT", nullable: false),
                    review_cycle = table.Column<int>(type: "INTEGER", nullable: false),
                    action_type = table.Column<string>(type: "TEXT", nullable: false),
                    comment = table.Column<string>(type: "TEXT", nullable: true),
                    previous_status = table.Column<string>(type: "TEXT", nullable: false),
                    resulting_status = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fcr_reviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_fcr_reviews_users_reviewer_user_id",
                        column: x => x.reviewer_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workflow_definitions",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    workflow_name = table.Column<string>(type: "TEXT", nullable: false),
                    review_mode = table.Column<string>(type: "TEXT", nullable: false),
                    configuration = table.Column<string>(type: "TEXT", nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_definitions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_notifications_user",
                table: "fcr_notifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_reviews_request",
                table: "fcr_reviews",
                column: "fcr_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_fcr_reviews_reviewer_user_id",
                table: "fcr_reviews",
                column: "reviewer_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fcr_notifications");

            migrationBuilder.DropTable(
                name: "fcr_reviews");

            migrationBuilder.DropTable(
                name: "workflow_definitions");

            migrationBuilder.DropColumn(
                name: "cycle",
                table: "fcr_workflow_instances");

            migrationBuilder.DropColumn(
                name: "cycle",
                table: "fcr_review_assignments");
        }
    }
}
