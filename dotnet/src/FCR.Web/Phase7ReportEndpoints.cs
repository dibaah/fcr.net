using FCR.Domain;
using FCR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FCR.Web;

/// <summary>Admin-only Phase 7 reporting endpoints.</summary>
public static class Phase7ReportEndpoints
{
    public static void MapPhase7Reports(this WebApplication app)
    {
        var reports = app.MapGroup("/api/admin/reports")
            .RequireAuthorization(PolicyNames.AdminConsole);

        reports.MapGet("/workflow-summary", async (FcrDbContext db, CancellationToken ct) =>
        {
            var byStatus = await db.FcrRequests.AsNoTracking()
                .GroupBy(r => r.SubmissionStatus)
                .Select(g => new { status = g.Key, count = g.Count() })
                .OrderBy(x => x.status).ToListAsync(ct);

            var reviews = await db.FcrReviews.AsNoTracking()
                .GroupBy(r => r.ActionType)
                .Select(g => new { action = g.Key, count = g.Count() })
                .OrderBy(x => x.action).ToListAsync(ct);

            var cycles = await db.FcrWorkflowInstances.AsNoTracking()
                .GroupBy(w => w.WorkflowStatus)
                .Select(g => new { workflow_status = g.Key, count = g.Count() })
                .OrderBy(x => x.workflow_status).ToListAsync(ct);

            return Results.Ok(new
            {
                generated_at_utc = DateTime.UtcNow,
                request_status_counts = byStatus,
                review_decision_counts = reviews,
                workflow_status_counts = cycles,
                total_requests = await db.FcrRequests.CountAsync(ct),
                total_review_actions = await db.FcrReviews.CountAsync(ct)
            });
        });

        reports.MapGet("/pending-reviews", async (FcrDbContext db, CancellationToken ct) =>
        {
            var rows = await db.FcrReviewAssignments.AsNoTracking()
                .Where(a => a.Status == "Active")
                .Select(a => new
                {
                    request_id = a.Stage!.Workflow!.FcrRequestId,
                    request_number = a.Stage.Workflow.FcrRequest!.RequestNumber,
                    subject = a.Stage.Workflow.FcrRequest.Subject,
                    stage = a.Stage.StageName,
                    cycle = a.Stage.Workflow.Cycle,
                    reviewer_user_id = a.AssignedUserId,
                    reviewer_username = a.AssignedUser!.Username,
                    assigned_at = a.AssignedAt,
                    workflow_status = a.Stage.Workflow.WorkflowStatus
                })
                .OrderBy(x => x.assigned_at).ToListAsync(ct);

            var now = DateTime.UtcNow;
            return Results.Ok(rows.Select(r => new
            {
                r.request_id, r.request_number, r.subject, r.stage, r.cycle,
                r.reviewer_user_id, r.reviewer_username, r.assigned_at, r.workflow_status,
                age_days = TryAgeDays(r.assigned_at, now)
            }));
        });

        reports.MapGet("/approval-statistics", async (FcrDbContext db, CancellationToken ct) =>
        {
            var decisions = await db.FcrReviews.AsNoTracking()
                .Where(r => r.ActionType == "approved" || r.ActionType == "rejected"
                    || r.ActionType == "returned")
                .GroupBy(r => r.ActionType)
                .Select(g => new { decision = g.Key, count = g.Count() })
                .ToListAsync(ct);

            var submitted = await db.FcrRequests.AsNoTracking()
                .Where(r => r.SubmittedAt != null).CountAsync(ct);
            var completed = await db.FcrWorkflowInstances.AsNoTracking()
                .Where(w => w.WorkflowStatus == "Approved").CountAsync(ct);
            var rejected = await db.FcrWorkflowInstances.AsNoTracking()
                .Where(w => w.WorkflowStatus == "Rejected").CountAsync(ct);

            return Results.Ok(new
            {
                generated_at_utc = DateTime.UtcNow,
                total_submitted_requests = submitted,
                approved_workflows = completed,
                rejected_workflows = rejected,
                decision_counts = decisions
            });
        });

        reports.MapGet("/review-aging", async (FcrDbContext db, int? older_than_days, CancellationToken ct) =>
        {
            var threshold = Math.Clamp(older_than_days ?? 7, 0, 3650);
            var rows = await db.FcrReviewAssignments.AsNoTracking()
                .Where(a => a.Status == "Active")
                .Select(a => new
                {
                    request_id = a.Stage!.Workflow!.FcrRequestId,
                    request_number = a.Stage.Workflow.FcrRequest!.RequestNumber,
                    subject = a.Stage.Workflow.FcrRequest.Subject,
                    reviewer = a.AssignedUser!.Username,
                    stage = a.Stage.StageName,
                    assigned_at = a.AssignedAt
                }).ToListAsync(ct);

            var cutoff = DateTime.UtcNow.AddDays(-threshold);
            return Results.Ok(rows.Where(r => ParseDate(r.assigned_at) <= cutoff)
                .Select(r => new { r.request_id, r.request_number, r.subject, r.reviewer, r.stage,
                    r.assigned_at, age_days = TryAgeDays(r.assigned_at, DateTime.UtcNow) }));
        });

        reports.MapGet("/user-activity", async (FcrDbContext db, int? limit, long? user_id, CancellationToken ct) =>
        {
            var take = Math.Clamp(limit ?? 100, 1, 500);
            var query = db.FcrAuditLogs.AsNoTracking().AsQueryable();
            if (user_id.HasValue) query = query.Where(x => x.UserId == user_id.Value);
            var items = await query.OrderByDescending(x => x.CreatedAtUtc).Take(take)
                .Select(x => new
                {
                    x.Id, x.UserId, x.Username, x.Action, x.HttpMethod, x.Path,
                    x.StatusCode, x.RequestId, x.CreatedAtUtc
                }).ToListAsync(ct);
            return Results.Ok(items);
        });
    }

    private static DateTime ParseDate(string? value) =>
        DateTime.TryParse(value, out var date) ? date : DateTime.MinValue;

    private static int TryAgeDays(string? value, DateTime now) =>
        Math.Max(0, (int)(now - ParseDate(value)).TotalDays);
}
