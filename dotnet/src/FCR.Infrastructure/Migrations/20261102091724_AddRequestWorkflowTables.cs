using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FCR.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestWorkflowTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fcr_requests",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    request_number = table.Column<string>(type: "TEXT", nullable: false),
                    requester_user_id = table.Column<long>(type: "INTEGER", nullable: false),
                    subject = table.Column<string>(type: "TEXT", nullable: false),
                    justification = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: false),
                    submission_status = table.Column<string>(type: "TEXT", nullable: false),
                    submitted_at = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: false),
                    linked_fcr_id = table.Column<long>(type: "INTEGER", nullable: true),
                    fcr_code = table.Column<string>(type: "TEXT", nullable: false),
                    category = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fcr_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_fcr_requests_users_requester_user_id",
                        column: x => x.requester_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fcr_workflow_instances",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    fcr_request_id = table.Column<string>(type: "TEXT", nullable: false),
                    workflow_status = table.Column<string>(type: "TEXT", nullable: false),
                    review_mode = table.Column<string>(type: "TEXT", nullable: false),
                    current_stage = table.Column<int>(type: "INTEGER", nullable: false),
                    started_at = table.Column<string>(type: "TEXT", nullable: true),
                    completed_at = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fcr_workflow_instances", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fcr_attachments",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    fcr_request_id = table.Column<string>(type: "TEXT", nullable: false),
                    uploaded_by_user_id = table.Column<long>(type: "INTEGER", nullable: false),
                    original_file_name = table.Column<string>(type: "TEXT", nullable: false),
                    stored_file_name = table.Column<string>(type: "TEXT", nullable: false),
                    storage_location = table.Column<string>(type: "TEXT", nullable: false),
                    content_type = table.Column<string>(type: "TEXT", nullable: false),
                    file_size = table.Column<long>(type: "INTEGER", nullable: false),
                    uploaded_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fcr_attachments", x => x.id);
                    table.ForeignKey(
                        name: "FK_fcr_attachments_fcr_requests_fcr_request_id",
                        column: x => x.fcr_request_id,
                        principalTable: "fcr_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fcr_attachments_users_uploaded_by_user_id",
                        column: x => x.uploaded_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fcr_comments",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    fcr_request_id = table.Column<string>(type: "TEXT", nullable: false),
                    user_id = table.Column<long>(type: "INTEGER", nullable: false),
                    comment_text = table.Column<string>(type: "TEXT", nullable: false),
                    comment_type = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fcr_comments", x => x.id);
                    table.ForeignKey(
                        name: "FK_fcr_comments_fcr_requests_fcr_request_id",
                        column: x => x.fcr_request_id,
                        principalTable: "fcr_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fcr_comments_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fcr_workflow_stages",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    workflow_instance_id = table.Column<long>(type: "INTEGER", nullable: false),
                    stage_name = table.Column<string>(type: "TEXT", nullable: false),
                    stage_order = table.Column<int>(type: "INTEGER", nullable: false),
                    decision_rule = table.Column<string>(type: "TEXT", nullable: false),
                    stage_status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fcr_workflow_stages", x => x.id);
                    table.ForeignKey(
                        name: "FK_fcr_workflow_stages_fcr_workflow_instances_workflow_instance_id",
                        column: x => x.workflow_instance_id,
                        principalTable: "fcr_workflow_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fcr_review_assignments",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    workflow_stage_id = table.Column<long>(type: "INTEGER", nullable: false),
                    assigned_user_id = table.Column<long>(type: "INTEGER", nullable: false),
                    assignment_order = table.Column<int>(type: "INTEGER", nullable: false),
                    assignment_status = table.Column<string>(type: "TEXT", nullable: false),
                    assigned_at = table.Column<string>(type: "TEXT", nullable: false),
                    completed_at = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fcr_review_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_fcr_review_assignments_fcr_workflow_stages_workflow_stage_id",
                        column: x => x.workflow_stage_id,
                        principalTable: "fcr_workflow_stages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fcr_review_assignments_users_assigned_user_id",
                        column: x => x.assigned_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_attachments_request",
                table: "fcr_attachments",
                column: "fcr_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_fcr_attachments_uploaded_by_user_id",
                table: "fcr_attachments",
                column: "uploaded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_comments_request",
                table: "fcr_comments",
                column: "fcr_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_fcr_comments_user_id",
                table: "fcr_comments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_requests_number",
                table: "fcr_requests",
                column: "request_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_requests_requester",
                table: "fcr_requests",
                column: "requester_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_requests_status",
                table: "fcr_requests",
                column: "submission_status");

            migrationBuilder.CreateIndex(
                name: "idx_assignments_user",
                table: "fcr_review_assignments",
                column: "assigned_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_fcr_review_assignments_workflow_stage_id",
                table: "fcr_review_assignments",
                column: "workflow_stage_id");

            migrationBuilder.CreateIndex(
                name: "idx_workflow_request",
                table: "fcr_workflow_instances",
                column: "fcr_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fcr_workflow_stages_workflow_instance_id",
                table: "fcr_workflow_stages",
                column: "workflow_instance_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fcr_attachments");

            migrationBuilder.DropTable(
                name: "fcr_comments");

            migrationBuilder.DropTable(
                name: "fcr_review_assignments");

            migrationBuilder.DropTable(
                name: "fcr_requests");

            migrationBuilder.DropTable(
                name: "fcr_workflow_stages");

            migrationBuilder.DropTable(
                name: "fcr_workflow_instances");
        }
    }
}
