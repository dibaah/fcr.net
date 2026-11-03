using FCR.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FCR.Infrastructure.Persistence;

/// <summary>
/// EF Core context mapping the existing schema v2 exactly —
/// same table names, snake_case columns, indexes and column nullability
/// as the live fcr_database.db (all TEXT columns are nullable there).
/// </summary>
public sealed class FcrDbContext(DbContextOptions<FcrDbContext> options) : DbContext(options)
{
    public DbSet<Fcr> Fcrs => Set<Fcr>();
    public DbSet<ImportLog> ImportLogs => Set<ImportLog>();
    public DbSet<Meta> Meta => Set<Meta>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<FcrRequest> FcrRequests => Set<FcrRequest>();
    public DbSet<FcrRequestComment> FcrRequestComments => Set<FcrRequestComment>();
    public DbSet<FcrAttachment> FcrAttachments => Set<FcrAttachment>();
    public DbSet<FcrWorkflowInstance> FcrWorkflowInstances => Set<FcrWorkflowInstance>();
    public DbSet<FcrWorkflowStage> FcrWorkflowStages => Set<FcrWorkflowStage>();
    public DbSet<FcrReviewAssignment> FcrReviewAssignments => Set<FcrReviewAssignment>();
    public DbSet<FcrReview> FcrReviews => Set<FcrReview>();
    public DbSet<FcrNotification> FcrNotifications => Set<FcrNotification>();
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<FcrAuditLog> FcrAuditLogs => Set<FcrAuditLog>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Fcr>(e =>
        {
            e.ToTable("fcr");
            e.HasKey(f => f.Id);
            e.Property(f => f.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(f => f.RowNum).HasColumnName("row_num").IsRequired(false);
            e.Property(f => f.FcrSubject).HasColumnName("fcr_subject").IsRequired(false);
            e.Property(f => f.Category).HasColumnName("category").IsRequired(false);
            e.Property(f => f.FcrCode).HasColumnName("fcr_code").IsRequired(false);
            e.Property(f => f.Date).HasColumnName("date").IsRequired(false);
            e.Property(f => f.ReviewDate).HasColumnName("review_date").IsRequired(false);
            e.Property(f => f.Originator).HasColumnName("originator").IsRequired(false);
            e.Property(f => f.ActionParty).HasColumnName("action_party").IsRequired(false);
            e.Property(f => f.Level).HasColumnName("level").IsRequired(false);
            e.Property(f => f.SessionCode).HasColumnName("session_code").IsRequired(false);
            e.Property(f => f.PedecStatus).HasColumnName("pedec_status").IsRequired(false);
            e.Property(f => f.FcrStatus).HasColumnName("fcr_status").IsRequired(false);
            e.Property(f => f.ExecutionStatus).HasColumnName("execution_status").IsRequired(false);
            e.Property(f => f.ClosedDate).HasColumnName("closed_date").IsRequired(false);
            e.Property(f => f.Tr).HasColumnName("tr").IsRequired(false);
            e.Property(f => f.NextActionBy).HasColumnName("next_action_by").IsRequired(false);
            e.Property(f => f.ArchLoc).HasColumnName("arch_loc").IsRequired(false);
            e.Property(f => f.LastUpdate).HasColumnName("last_update").IsRequired(false);
            e.Property(f => f.Remarks).HasColumnName("remarks").IsRequired(false);
            e.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired(false);
            e.Property(f => f.UpdatedAt).HasColumnName("updated_at").IsRequired(false);
            e.HasIndex(f => f.FcrCode).HasDatabaseName("idx_fcr_code");
            e.HasIndex(f => f.Category).HasDatabaseName("idx_fcr_category");
            e.HasIndex(f => f.PedecStatus).HasDatabaseName("idx_fcr_pedec_status");
            e.HasIndex(f => f.FcrStatus).HasDatabaseName("idx_fcr_fcr_status");
            e.HasIndex(f => f.ExecutionStatus).HasDatabaseName("idx_fcr_exec_status");
            e.HasIndex(f => f.Date).HasDatabaseName("idx_fcr_date");
        });

        mb.Entity<ImportLog>(e =>
        {
            e.ToTable("import_log");
            e.HasKey(i => i.Id);
            e.Property(i => i.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(i => i.Filename).HasColumnName("filename");
            e.Property(i => i.RowCount).HasColumnName("row_count");
            e.Property(i => i.ImportedAt).HasColumnName("imported_at").IsRequired(false);
        });

        mb.Entity<Meta>(e =>
        {
            e.ToTable("meta");
            e.HasKey(m => m.Key);
            e.Property(m => m.Key).HasColumnName("key");
            e.Property(m => m.Value).HasColumnName("value");
        });

        mb.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(u => u.Username).HasColumnName("username").IsRequired();
            e.Property(u => u.DisplayName).HasColumnName("display_name");
            e.Property(u => u.Email).HasColumnName("email");
            e.Property(u => u.ExternalId).HasColumnName("external_id").IsRequired(false);
            e.Property(u => u.PasswordHash).HasColumnName("password_hash");
            e.Property(u => u.IsActive).HasColumnName("is_active");
            e.Property(u => u.CreatedAt).HasColumnName("created_at");
            e.Property(u => u.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(u => u.Username).IsUnique().HasDatabaseName("idx_users_username");
            e.HasIndex(u => u.ExternalId).IsUnique().HasDatabaseName("idx_users_external_id")
                .HasFilter("external_id IS NOT NULL");
        });

        mb.Entity<Role>(e =>
        {
            e.ToTable("roles");
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(r => r.Name).HasColumnName("name").IsRequired();
            e.Property(r => r.Description).HasColumnName("description");
            e.HasIndex(r => r.Name).IsUnique().HasDatabaseName("idx_roles_name");
        });

        mb.Entity<UserRole>(e =>
        {
            e.ToTable("user_roles");
            e.HasKey(ur => new { ur.UserId, ur.RoleId });
            e.Property(ur => ur.UserId).HasColumnName("user_id");
            e.Property(ur => ur.RoleId).HasColumnName("role_id");
            e.HasOne(ur => ur.User).WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ur => ur.Role).WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<FcrRequest>(e =>
        {
            e.ToTable("fcr_requests");
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasColumnName("id");
            e.Property(r => r.RequestNumber).HasColumnName("request_number").IsRequired();
            e.Property(r => r.RequesterUserId).HasColumnName("requester_user_id");
            e.Property(r => r.Subject).HasColumnName("subject").IsRequired();
            e.Property(r => r.Justification).HasColumnName("justification");
            e.Property(r => r.Description).HasColumnName("description");
            e.Property(r => r.SubmissionStatus).HasColumnName("submission_status");
            e.Property(r => r.SubmittedAt).HasColumnName("submitted_at");
            e.Property(r => r.CreatedAt).HasColumnName("created_at");
            e.Property(r => r.UpdatedAt).HasColumnName("updated_at");
            e.Property(r => r.LinkedFcrId).HasColumnName("linked_fcr_id");
            e.Property(r => r.FcrCode).HasColumnName("fcr_code");
            e.Property(r => r.Category).HasColumnName("category");
            e.Property(r => r.Contractor).HasColumnName("contractor").IsRequired();
            e.Property(r => r.Subcontractor).HasColumnName("subcontractor").IsRequired();
            e.Property(r => r.ArchivePath).HasColumnName("archive_path");
            e.Property(r => r.ArchivedAt).HasColumnName("archived_at");
            e.HasIndex(r => r.RequestNumber).IsUnique().HasDatabaseName("idx_requests_number");
            e.HasIndex(r => r.SubmissionStatus).HasDatabaseName("idx_requests_status");
            e.HasIndex(r => r.RequesterUserId).HasDatabaseName("idx_requests_requester");
            e.HasOne(r => r.Requester).WithMany()
                .HasForeignKey(r => r.RequesterUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(r => r.Comments).WithOne()
                .HasForeignKey(c => c.FcrRequestId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(r => r.Attachments).WithOne()
                .HasForeignKey(a => a.FcrRequestId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<FcrRequestComment>(e =>
        {
            e.ToTable("fcr_comments");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(c => c.FcrRequestId).HasColumnName("fcr_request_id");
            e.Property(c => c.UserId).HasColumnName("user_id");
            e.Property(c => c.CommentText).HasColumnName("comment_text");
            e.Property(c => c.CommentType).HasColumnName("comment_type");
            e.Property(c => c.CreatedAt).HasColumnName("created_at");
            e.HasIndex(c => c.FcrRequestId).HasDatabaseName("idx_comments_request");
            e.HasOne(c => c.User).WithMany()
                .HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<FcrAttachment>(e =>
        {
            e.ToTable("fcr_attachments");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(a => a.FcrRequestId).HasColumnName("fcr_request_id");
            e.Property(a => a.UploadedByUserId).HasColumnName("uploaded_by_user_id");
            e.Property(a => a.OriginalFileName).HasColumnName("original_file_name");
            e.Property(a => a.StoredFileName).HasColumnName("stored_file_name");
            e.Property(a => a.StorageLocation).HasColumnName("storage_location");
            e.Property(a => a.ContentType).HasColumnName("content_type");
            e.Property(a => a.FileSize).HasColumnName("file_size");
            e.Property(a => a.UploadedAt).HasColumnName("uploaded_at");
            e.HasIndex(a => a.FcrRequestId).HasDatabaseName("idx_attachments_request");
            e.HasOne(a => a.UploadedBy).WithMany()
                .HasForeignKey(a => a.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<FcrWorkflowInstance>(e =>
        {
            e.ToTable("fcr_workflow_instances");
            e.HasKey(w => w.Id);
            e.Property(w => w.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(w => w.FcrRequestId).HasColumnName("fcr_request_id");
            e.Property(w => w.WorkflowStatus).HasColumnName("workflow_status");
            e.Property(w => w.ReviewMode).HasColumnName("review_mode");
            e.Property(w => w.CurrentStage).HasColumnName("current_stage");
            e.Property(w => w.Cycle).HasColumnName("cycle");
            e.Property(w => w.StartedAt).HasColumnName("started_at");
            e.Property(w => w.CompletedAt).HasColumnName("completed_at");
            e.Property(w => w.CreatedAt).HasColumnName("created_at");
            e.Property(w => w.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(w => w.FcrRequestId).IsUnique().HasDatabaseName("idx_workflow_request");
            e.HasMany(w => w.Stages).WithOne()
                .HasForeignKey(s => s.WorkflowInstanceId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<FcrWorkflowStage>(e =>
        {
            e.ToTable("fcr_workflow_stages");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(s => s.WorkflowInstanceId).HasColumnName("workflow_instance_id");
            e.Property(s => s.StageName).HasColumnName("stage_name");
            e.Property(s => s.StageOrder).HasColumnName("stage_order");
            e.Property(s => s.DecisionRule).HasColumnName("decision_rule");
            e.Property(s => s.StageStatus).HasColumnName("stage_status");
            e.HasMany(s => s.Assignments).WithOne()
                .HasForeignKey(a => a.WorkflowStageId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<FcrReviewAssignment>(e =>
        {
            e.ToTable("fcr_review_assignments");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(a => a.WorkflowStageId).HasColumnName("workflow_stage_id");
            e.Property(a => a.AssignedUserId).HasColumnName("assigned_user_id");
            e.Property(a => a.AssignmentOrder).HasColumnName("assignment_order");
            e.Property(a => a.Cycle).HasColumnName("cycle");
            e.Property(a => a.AssignmentStatus).HasColumnName("assignment_status");
            e.Property(a => a.AssignedAt).HasColumnName("assigned_at");
            e.Property(a => a.CompletedAt).HasColumnName("completed_at");
            e.HasIndex(a => a.AssignedUserId).HasDatabaseName("idx_assignments_user");
            e.HasOne(a => a.AssignedUser).WithMany()
                .HasForeignKey(a => a.AssignedUserId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<FcrReview>(e =>
        {
            e.ToTable("fcr_reviews");
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(r => r.FcrRequestId).HasColumnName("fcr_request_id");
            e.Property(r => r.WorkflowInstanceId).HasColumnName("workflow_instance_id");
            e.Property(r => r.ReviewAssignmentId).HasColumnName("review_assignment_id");
            e.Property(r => r.ReviewerUserId).HasColumnName("reviewer_user_id");
            e.Property(r => r.ReviewerName).HasColumnName("reviewer_name");
            e.Property(r => r.StageName).HasColumnName("stage_name");
            e.Property(r => r.ReviewCycle).HasColumnName("review_cycle");
            e.Property(r => r.ActionType).HasColumnName("action_type");
            e.Property(r => r.Comment).HasColumnName("comment");
            e.Property(r => r.PreviousStatus).HasColumnName("previous_status");
            e.Property(r => r.ResultingStatus).HasColumnName("resulting_status");
            e.Property(r => r.CreatedAt).HasColumnName("created_at");
            e.HasIndex(r => r.FcrRequestId).HasDatabaseName("idx_reviews_request");
            e.HasOne(r => r.Reviewer).WithMany()
                .HasForeignKey(r => r.ReviewerUserId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<FcrNotification>(e =>
        {
            e.ToTable("fcr_notifications");
            e.HasKey(n => n.Id);
            e.Property(n => n.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(n => n.UserId).HasColumnName("user_id");
            e.Property(n => n.NotificationType).HasColumnName("notification_type");
            e.Property(n => n.Message).HasColumnName("message");
            e.Property(n => n.FcrRequestId).HasColumnName("fcr_request_id");
            e.Property(n => n.IsRead).HasColumnName("is_read");
            e.Property(n => n.CreatedAt).HasColumnName("created_at");
            e.HasIndex(n => n.UserId).HasDatabaseName("idx_notifications_user");
            e.HasOne(n => n.User).WithMany()
                .HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<FcrAuditLog>(e =>
        {
            e.ToTable("fcr_audit_logs");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(a => a.UserId).HasColumnName("user_id");
            e.Property(a => a.Username).HasColumnName("username");
            e.Property(a => a.Action).HasColumnName("action").IsRequired();
            e.Property(a => a.HttpMethod).HasColumnName("http_method").IsRequired();
            e.Property(a => a.Path).HasColumnName("path").IsRequired();
            e.Property(a => a.StatusCode).HasColumnName("status_code");
            e.Property(a => a.RequestId).HasColumnName("request_id");
            e.Property(a => a.CreatedAtUtc).HasColumnName("created_at_utc");
            e.HasIndex(a => a.CreatedAtUtc).HasDatabaseName("idx_audit_created_at");
            e.HasIndex(a => new { a.UserId, a.CreatedAtUtc }).HasDatabaseName("idx_audit_user_created_at");
            e.HasIndex(a => a.RequestId).HasDatabaseName("idx_audit_request");
        });

        mb.Entity<WorkflowDefinition>(e =>
        {
            e.ToTable("workflow_definitions");
            e.HasKey(d => d.Id);
            e.Property(d => d.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(d => d.WorkflowName).HasColumnName("workflow_name");
            e.Property(d => d.ReviewMode).HasColumnName("review_mode");
            e.Property(d => d.Configuration).HasColumnName("configuration");
            e.Property(d => d.IsActive).HasColumnName("is_active");
            e.Property(d => d.CreatedAt).HasColumnName("created_at");
        });
    }
}
