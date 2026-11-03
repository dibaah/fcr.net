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
            var workflowStatuses = await db.FcrWorkflowInstances.AsNoTracking()
                .GroupBy(w => w.WorkflowStatus)
                .Select(g => new { workflow_status = g.Key, count = g.Count() })
                .OrderBy(x => x.workflow_status).ToListAsync(ct);
            return Results.Ok(new
            {
                generated_at_utc = DateTime.UtcNow,
                request_status_counts = byStatus,
                review_action_counts = reviews,
                workflow_status_counts = workflowStatuses,
                total_requests = await db.FcrRequests.CountAsync(ct),
                total_review_actions = await db.FcrReviews.CountAsync(ct)
            });
        });

        reports.MapGet("/pending-reviews", async (FcrDbContext db, CancellationToken ct) =>
        {
            var rows = await (from assignment in db.FcrReviewAssignments.AsNoTracking()
                join stage in db.FcrWorkflowStages on assignment.WorkflowStageId equals stage.Id
                join workflow in db.FcrWorkflowInstances on stage.WorkflowInstanceId equals workflow.Id
                join request in db.FcrRequests on workflow.FcrRequestId equals request.Id
                join reviewer in db.Users on assignment.AssignedUserId equals reviewer.Id
                where assignment.AssignmentStatus == "Active"
                orderby assignment.AssignedAt
                select new
                {
                    request_id = request.Id, request_number = request.RequestNumber,
                    subject = request.Subject, stage = stage.StageName,
                    cycle = workflow.Cycle, reviewer_user_id = reviewer.Id,
                    reviewer_username = reviewer.Username, assigned_at = assignment.AssignedAt,
                    workflow_status = workflow.WorkflowStatus
                }).ToListAsync(ct);
            return Results.Ok(rows.Select(r => new
            {
                r.request_id, r.request_number, r.subject, r.stage, r.cycle,
                r.reviewer_user_id, r.reviewer_username, r.assigned_at, r.workflow_status,
                age_days = AgeDays(r.assigned_at)
            }));
        });

        reports.MapGet("/approval-statistics", async (FcrDbContext db, CancellationToken ct) =>
        {
            var decisions = await db.FcrReviews.AsNoTracking()
                .Where(r => r.ActionType == ReviewActions.Approve || r.ActionType == ReviewActions.Reject
                    || r.ActionType == ReviewActions.Return)
                .GroupBy(r => r.ActionType)
                .Select(g => new { decision = g.Key, count = g.Count() }).ToListAsync(ct);
            return Results.Ok(new
            {
                generated_at_utc = DateTime.UtcNow,
                total_submitted_requests = await db.FcrRequests.Where(r => r.SubmittedAt != null).CountAsync(ct),
                approved_workflows = await db.FcrWorkflowInstances.Where(w => w.WorkflowStatus == WorkflowStatuses.Approved).CountAsync(ct),
                rejected_workflows = await db.FcrWorkflowInstances.Where(w => w.WorkflowStatus == WorkflowStatuses.Rejected).CountAsync(ct),
                returned_workflows = await db.FcrWorkflowInstances.Where(w => w.WorkflowStatus == WorkflowStatuses.ReturnedForCorrection).CountAsync(ct),
                decision_counts = decisions
            });
        });

        reports.MapGet("/review-aging", async (FcrDbContext db, int? older_than_days, CancellationToken ct) =>
        {
            var threshold = Math.Clamp(older_than_days ?? 7, 0, 3650);
            var rows = await (from assignment in db.FcrReviewAssignments.AsNoTracking()
                join stage in db.FcrWorkflowStages on assignment.WorkflowStageId equals stage.Id
                join workflow in db.FcrWorkflowInstances on stage.WorkflowInstanceId equals workflow.Id
                join request in db.FcrRequests on workflow.FcrRequestId equals request.Id
                join reviewer in db.Users on assignment.AssignedUserId equals reviewer.Id
                where assignment.AssignmentStatus == "Active"
                select new { request_id = request.Id, request_number = request.RequestNumber,
                    subject = request.Subject, reviewer = reviewer.Username,
                    stage = stage.StageName, assigned_at = assignment.AssignedAt }).ToListAsync(ct);
            var cutoff = DateTime.UtcNow.AddDays(-threshold);
            return Results.Ok(rows.Where(r => ParseDate(r.assigned_at) <= cutoff)
                .Select(r => new { r.request_id, r.request_number, r.subject, r.reviewer,
                    r.stage, r.assigned_at, age_days = AgeDays(r.assigned_at) }));
        });

        reports.MapGet("/user-activity", async (FcrDbContext db, int? limit, long? user_id, CancellationToken ct) =>
        {
            var take = Math.Clamp(limit ?? 100, 1, 500);
            var query = db.FcrAuditLogs.AsNoTracking().AsQueryable();
            if (user_id.HasValue) query = query.Where(x => x.UserId == user_id.Value);
            var items = await query.OrderByDescending(x => x.CreatedAtUtc).Take(take)
                .Select(x => new { x.Id, x.UserId, x.Username, x.Action, x.HttpMethod,
                    x.Path, x.StatusCode, x.RequestId, x.CreatedAtUtc }).ToListAsync(ct);
            return Results.Ok(items);
        });
    }

    private static DateTime ParseDate(string? value) =>
        DateTime.TryParse(value, out var date) ? date : DateTime.MinValue;
    private static int AgeDays(string? value) =>
        Math.Max(0, (int)(DateTime.UtcNow - ParseDate(value)).TotalDays);
}
