using FCR.Application;
using FCR.Domain;
using FCR.Domain.Entities;
using FCR.Infrastructure.Attachments;
using FCR.Infrastructure.Archive;
using Microsoft.EntityFrameworkCore;

namespace FCR.Infrastructure.Persistence;

/// <summary>
/// FCR request submission + FCR Admin processing (master plan §2.2/§2.3).
/// Requests stay logically separate from `fcr` records; the Request ID is a
/// UUID created at submission, the FCR Code is assigned later by the admin.
/// </summary>
public sealed class RequestService(FcrDbContext db, AttachmentStore store, IFcrRepository fcrRepo, FcrArchiveService archiveService)
    : IRequestService
{
    private static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    public async Task<RequestDetailDto> CreateAsync(
        long requesterUserId, CreateRequestInput input, CancellationToken ct = default)
    {
        var subject = (input.Subject ?? "").Trim();
        var justification = (input.Justification ?? "").Trim();
        var description = (input.Description ?? "").Trim();
        if (subject.Length == 0) throw new FcrValidationException("Subject required");
        if (justification.Length == 0) throw new FcrValidationException("Justification required");
        if (description.Length == 0) throw new FcrValidationException("Description required");

        var now = Now();
        var req = new FcrRequest
        {
            RequestNumber = await NextRequestNumberAsync(ct),
            RequesterUserId = requesterUserId,
            Subject = subject,
            Justification = justification,
            Description = description,
            SubmissionStatus = RequestStatuses.Submitted,
            SubmittedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.FcrRequests.Add(req);
        await db.SaveChangesAsync(ct);
        await NotifyAsync(await AdminUserIdsAsync(ct), "request_submitted",
            $"New FCR request {req.RequestNumber}: {subject}", req.Id, ct);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(req.Id, ct))!;
    }

    private async Task<List<long>> AdminUserIdsAsync(CancellationToken ct) =>
        await db.UserRoles
            .Where(ur => ur.Role!.Name == RoleNames.FcrAdmin
                || ur.Role!.Name == RoleNames.SystemAdministrator)
            .Where(ur => ur.User!.IsActive)
            .Select(ur => ur.UserId).Distinct().ToListAsync(ct);

    private void Notify(IEnumerable<long> userIds, string type, string message,
        string? requestId)
    {
        foreach (var uid in userIds.Distinct())
            db.FcrNotifications.Add(new FcrNotification
            {
                UserId = uid, NotificationType = type, Message = message,
                FcrRequestId = requestId, CreatedAt = Now(),
            });
    }

    private Task NotifyAsync(IEnumerable<long> userIds, string type, string message,
        string? requestId, CancellationToken ct)
    {
        Notify(userIds, type, message, requestId);
        return Task.CompletedTask;
    }

    private async Task<string> NextRequestNumberAsync(CancellationToken ct)
    {
        var prefix = $"REQ-{DateTime.Now.Year}-";
        var count = await db.FcrRequests
            .CountAsync(r => r.RequestNumber.StartsWith(prefix), ct);
        return $"{prefix}{count + 1:000}";
    }

    public Task<List<RequestListItemDto>> ListAsync(
        long? onlyUserId, string? status, CancellationToken ct = default)
    {
        var q = db.FcrRequests.AsNoTracking().AsQueryable();
        if (onlyUserId.HasValue) q = q.Where(r => r.RequesterUserId == onlyUserId.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.SubmissionStatus == status);
        return q.OrderByDescending(r => r.SubmittedAt)
            .Select(r => new RequestListItemDto(
                r.Id, r.RequestNumber, r.RequesterUserId,
                r.Requester!.Username, r.Subject, r.SubmissionStatus, r.SubmittedAt,
                r.CreatedAt, r.UpdatedAt, r.LinkedFcrId, r.FcrCode, r.Category,
                r.Contractor, r.Subcontractor, r.ArchivePath != null, r.ArchivedAt,
                r.Attachments.Count, r.Comments.Count))
            .ToListAsync(ct);
    }

    public async Task<RequestDetailDto?> GetAsync(string id, CancellationToken ct = default)
    {
        var r = await db.FcrRequests.AsNoTracking()
            .Include(x => x.Requester)
            .Include(x => x.Comments).ThenInclude(c => c.User)
            .Include(x => x.Attachments).ThenInclude(a => a.UploadedBy)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return null;

        var wf = await db.FcrWorkflowInstances.AsNoTracking()
            .Include(w => w.Stages).ThenInclude(s => s.Assignments).ThenInclude(a => a.AssignedUser)
            .FirstOrDefaultAsync(w => w.FcrRequestId == id, ct);
        return ToDto(r, wf);
    }

    public Task<long?> OwnerIdAsync(string id, CancellationToken ct = default) =>
        db.FcrRequests.Where(r => r.Id == id)
            .Select(r => (long?)r.RequesterUserId).FirstOrDefaultAsync(ct);

    public async Task<CommentDto?> AddCommentAsync(
        string id, long userId, string text, string commentType, CancellationToken ct = default)
    {
        var req = await db.FcrRequests.FindAsync([id], ct);
        if (req is null) return null;
        var body = (text ?? "").Trim();
        if (body.Length == 0) throw new FcrValidationException("Comment text required");
        var c = new FcrRequestComment
        {
            FcrRequestId = id,
            UserId = userId,
            CommentText = body,
            CommentType = commentType,
            CreatedAt = Now(),
        };
        db.FcrRequestComments.Add(c);
        req.UpdatedAt = Now();
        await db.SaveChangesAsync(ct);
        var username = await db.Users.Where(u => u.Id == userId)
            .Select(u => u.Username).FirstAsync(ct);
        return new CommentDto(c.Id, userId, username, c.CommentText, c.CommentType, c.CreatedAt);
    }

    // ── Attachments ──────────────────────────────────────────────────
    public async Task<AttachmentDto> AddAttachmentAsync(
        string id, long userId, string fileName, string contentType,
        Stream content, CancellationToken ct = default)
    {
        var req = await db.FcrRequests.FindAsync([id], ct);
        if (req is null) throw new FcrNotFoundException(0);
        if (!string.IsNullOrWhiteSpace(req.ArchivedAt)
            || req.SubmissionStatus is RequestStatuses.UnderReview
                or RequestStatuses.Approved or RequestStatuses.Rejected
                or RequestStatuses.Completed)
            throw new FcrValidationException(
                $"Attachments cannot be added while the request is '{req.SubmissionStatus}'");

        var stored = await store.SaveAsync(id, fileName, content, ct);
        var a = new FcrAttachment
        {
            FcrRequestId = id,
            UploadedByUserId = userId,
            OriginalFileName = AttachmentStore.SanitizeFileName(fileName),
            StoredFileName = Path.GetFileName(stored),
            StorageLocation = stored,
            ContentType = contentType ?? "application/octet-stream",
            FileSize = new FileInfo(store.Resolve(stored)).Length,
            UploadedAt = Now(),
        };
        db.FcrAttachments.Add(a);
        req.UpdatedAt = Now();
        await db.SaveChangesAsync(ct);
        return await AttachmentDtoAsync(a.Id, ct);
    }

    public async Task<FcrAttachmentInfo?> GetAttachmentAsync(
        string id, long attachmentId, CancellationToken ct = default)
    {
        var a = await db.FcrAttachments.AsNoTracking().Include(x => x.UploadedBy)
            .FirstOrDefaultAsync(x => x.Id == attachmentId && x.FcrRequestId == id, ct);
        if (a is null) return null;
        var path = store.Resolve(a.StorageLocation);
        if (!File.Exists(path)) return null;
        return new FcrAttachmentInfo(
            new AttachmentDto(a.Id, a.UploadedByUserId, a.UploadedBy?.Username ?? "",
                a.OriginalFileName, a.ContentType, a.FileSize, a.UploadedAt),
            path, a.OriginalFileName, a.ContentType);
    }

    public async Task<bool> DeleteAttachmentAsync(
        string id, long attachmentId, long userId, bool isAdmin, CancellationToken ct = default)
    {
        if (await db.FcrRequests.AnyAsync(r => r.Id == id && r.ArchivePath != null, ct))
            throw new FcrValidationException("Attachments cannot be changed after archiving");
        var a = await db.FcrAttachments
            .FirstOrDefaultAsync(x => x.Id == attachmentId && x.FcrRequestId == id, ct);
        if (a is null) return false;
        if (!isAdmin && a.UploadedByUserId != userId)
            throw new FcrValidationException("Only the uploader or an admin can delete attachments");
        db.FcrAttachments.Remove(a);
        await db.SaveChangesAsync(ct);
        store.DeleteFile(a.StorageLocation);
        return true;
    }

    private async Task<AttachmentDto> AttachmentDtoAsync(long id, CancellationToken ct)
    {
        var a = await db.FcrAttachments.AsNoTracking().Include(x => x.UploadedBy)
            .FirstAsync(x => x.Id == id, ct);
        return new AttachmentDto(a.Id, a.UploadedByUserId, a.UploadedBy?.Username ?? "",
            a.OriginalFileName, a.ContentType, a.FileSize, a.UploadedAt);
    }

    // ── FCR Admin processing ─────────────────────────────────────────
    public async Task<RequestDetailDto?> AdminUpdateAsync(
        string id, AdminUpdateInput input, CancellationToken ct = default)
    {
        var req = await db.FcrRequests.FindAsync([id], ct);
        if (req is null) return null;

        if (!string.IsNullOrWhiteSpace(req.ArchivePath)
            && ((input.FcrCode is not null && !string.Equals(
                    req.FcrCode, FcrCode.Validate(input.FcrCode), StringComparison.OrdinalIgnoreCase))
                || (input.Category is not null && input.Category.Trim() != req.Category)
                || (input.Contractor is not null && input.Contractor.Trim() != req.Contractor)
                || (input.Subcontractor is not null && input.Subcontractor.Trim() != req.Subcontractor)))
            throw new FcrValidationException(
                "FCR number/category/contractor fields cannot be changed after archiving");

        if (input.FcrCode is not null)
        {
            var code = FcrCode.Validate(input.FcrCode);
            if (code.Length > 0 && await db.Fcrs.AnyAsync(f => f.FcrCode == code, ct)
                && db.Fcrs.Any(f => f.FcrCode == code && f.Id != input.LinkedFcrId && f.Id != req.LinkedFcrId))
                throw new FcrValidationException($"FCR Code '{code}' already exists");
            if (!string.IsNullOrWhiteSpace(req.ArchivePath)
                && !string.Equals(req.FcrCode, code, StringComparison.OrdinalIgnoreCase))
                throw new FcrValidationException("FCR number cannot be changed after archiving");
            req.FcrCode = code;
        }
        if (input.Category is not null) req.Category = input.Category.Trim();
        if (input.Contractor is not null) req.Contractor = input.Contractor.Trim();
        if (input.Subcontractor is not null) req.Subcontractor = input.Subcontractor.Trim();
        if (input.LinkedFcrId.HasValue)
        {
            if (!await db.Fcrs.AnyAsync(f => f.Id == input.LinkedFcrId.Value, ct))
                throw new FcrValidationException($"FCR record {input.LinkedFcrId} not found");
            req.LinkedFcrId = input.LinkedFcrId;
        }
        if (req.SubmissionStatus == RequestStatuses.Submitted)
            req.SubmissionStatus = RequestStatuses.UnderAdminReview;
        req.UpdatedAt = Now();
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<RequestDetailDto?> CreateLinkedFcrAsync(string id, CancellationToken ct = default)
    {
        var req = await db.FcrRequests.Include(r => r.Requester)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (req is null) return null;
        if (req.LinkedFcrId.HasValue)
        {
            await archiveService.ArchiveAsync(id, ct);
            return await GetAsync(id, ct);
        }
        if (string.IsNullOrWhiteSpace(req.FcrCode))
            throw new FcrValidationException("FCR number must be assigned before creating the linked FCR");
        if (string.IsNullOrWhiteSpace(req.Category))
            throw new FcrValidationException("FCR category must be assigned before creating the linked FCR");

        var rec = await fcrRepo.CreateAsync(new FcrRecordInput
        {
            FcrSubject = req.Subject,
            FcrCode = req.FcrCode,
            Category = req.Category,
            Originator = req.Requester?.Username,
            Date = DateTime.Now.ToString("yyyy-MM-dd"),
        }, ct);
        req.LinkedFcrId = rec.Id;
        if (req.SubmissionStatus == RequestStatuses.Submitted)
            req.SubmissionStatus = RequestStatuses.UnderAdminReview;
        req.UpdatedAt = Now();
        await db.SaveChangesAsync(ct);
        await archiveService.ArchiveAsync(id, ct);
        return await GetAsync(id, ct);
    }

    public Task<ArchiveInfoDto?> ArchiveAsync(string id, CancellationToken ct = default) =>
        archiveService.ArchiveAsync(id, ct);

    public async Task<RequestDetailDto?> PrepareWorkflowAsync(
        string id, WorkflowPrepInput input, CancellationToken ct = default)
    {
        var req = await db.FcrRequests.FindAsync([id], ct);
        if (req is null) return null;
        if (!ReviewModes.All.Contains(input.ReviewMode))
            throw new FcrValidationException(
                $"review_mode must be one of: {string.Join(", ", ReviewModes.All)}");
        var reviewerIds = (input.ReviewerUserIds ?? []).Distinct().ToList();
        if (reviewerIds.Count == 0)
            throw new FcrValidationException("At least one reviewer must be assigned");
        var validReviewers = await db.UserRoles
            .Where(ur => reviewerIds.Contains(ur.UserId) && ur.Role!.Name == RoleNames.Reviewer)
            .Select(ur => ur.UserId).ToListAsync(ct);
        var invalid = reviewerIds.Except(validReviewers).ToList();
        if (invalid.Count > 0)
            throw new FcrValidationException(
                $"Users without the Reviewer role cannot be assigned: {string.Join(", ", invalid)}");

        var now = Now();
        var wf = await db.FcrWorkflowInstances.Include(w => w.Stages)
            .ThenInclude(s => s.Assignments)
            .FirstOrDefaultAsync(w => w.FcrRequestId == id, ct);
        if (wf is null)
        {
            wf = new FcrWorkflowInstance
            {
                FcrRequestId = id,
                WorkflowStatus = WorkflowStatuses.NotStarted,
                CreatedAt = now,
            };
            db.FcrWorkflowInstances.Add(wf);
        }
        if (wf.WorkflowStatus != WorkflowStatuses.NotStarted)
            throw new FcrValidationException(
                "Reviewer assignments cannot be changed after the review has started");
        var approverIds = (input.ApproverUserIds ?? []).Distinct().ToList();
        if (approverIds.Count > 0)
        {
            var validApprovers = await db.UserRoles
                .Where(ur => approverIds.Contains(ur.UserId) && ur.Role!.Name == RoleNames.Approver)
                .Select(ur => ur.UserId).ToListAsync(ct);
            var badApprovers = approverIds.Except(validApprovers).ToList();
            if (badApprovers.Count > 0)
                throw new FcrValidationException(
                    $"Users without the Approver role cannot be assigned: {string.Join(", ", badApprovers)}");
        }

        var decisionRule = input.DecisionRule ?? "all";
        if (!DecisionRules.IsValid(decisionRule))
            throw new FcrValidationException(
                "decision_rule must be 'all', 'any', or 'count:<n>'");

        db.FcrWorkflowStages.RemoveRange(wf.Stages);
        wf.ReviewMode = input.ReviewMode;
        wf.UpdatedAt = now;
        var stage = new FcrWorkflowStage
        {
            StageName = "Review",
            StageOrder = 0,
            DecisionRule = decisionRule,
            StageStatus = "Pending",
            Assignments = reviewerIds.Select((uid, i) => new FcrReviewAssignment
            {
                AssignedUserId = uid,
                AssignmentOrder = i,
                Cycle = wf.Cycle,
                AssignmentStatus = "Pending",
                AssignedAt = now,
            }).ToList(),
        };
        wf.Stages.Add(stage);
        if (approverIds.Count > 0)
            wf.Stages.Add(new FcrWorkflowStage
            {
                StageName = "Approval",
                StageOrder = 1,
                DecisionRule = decisionRule,
                StageStatus = "Pending",
                Assignments = approverIds.Select((uid, i) => new FcrReviewAssignment
                {
                    AssignedUserId = uid,
                    AssignmentOrder = i,
                    Cycle = wf.Cycle,
                    AssignmentStatus = "Pending",
                    AssignedAt = now,
                }).ToList(),
            });
        req.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<RequestDetailDto?> SubmitForReviewAsync(string id, CancellationToken ct = default)
    {
        var req = await db.FcrRequests.FindAsync([id], ct);
        if (req is null) return null;
        if (req.SubmissionStatus == RequestStatuses.UnderReview)
            throw new FcrValidationException("Request is already under review");
        if (req.FcrCode.Trim().Length == 0)
            throw new FcrValidationException("Assign an FCR Code before submitting for review");
        var wf = await db.FcrWorkflowInstances.Include(w => w.Stages).ThenInclude(s => s.Assignments)
            .FirstOrDefaultAsync(w => w.FcrRequestId == id, ct);
        if (wf is null || wf.Stages.SelectMany(s => s.Assignments).Count() == 0)
            throw new FcrValidationException("Configure the workflow and assign reviewers first");

        wf.WorkflowStatus = WorkflowStatuses.InProgress;
        wf.CurrentStage = 0;
        wf.StartedAt = Now();
        wf.UpdatedAt = Now();
        req.SubmissionStatus = RequestStatuses.UnderReview;
        req.UpdatedAt = Now();
        ActivateStage(wf.Stages.First(s => s.StageOrder == wf.CurrentStage), wf);
        Notify(wf.Stages.First(s => s.StageOrder == wf.CurrentStage)
                .Assignments.Where(a => a.Cycle == wf.Cycle && a.AssignmentStatus == "Active")
                .Select(a => a.AssignedUserId),
            "assignment",
            $"You are assigned to review request {req.RequestNumber}", id);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>sequential: only the first pending assignment goes Active;
    /// parallel: every pending assignment goes Active.</summary>
    private static void ActivateStage(FcrWorkflowStage stage, FcrWorkflowInstance wf)
    {
        stage.StageStatus = "Active";
        var pending = stage.Assignments
            .Where(a => a.Cycle == wf.Cycle && a.AssignmentStatus == "Pending")
            .OrderBy(a => a.AssignmentOrder).ToList();
        if (wf.ReviewMode == ReviewModes.Sequential)
            pending.Take(1).ToList().ForEach(a => a.AssignmentStatus = "Active");
        else
            pending.ForEach(a => a.AssignmentStatus = "Active");
    }

    // ── Phase 6: review/approval workflow execution ─────────────────
    public Task<List<ReviewTaskDto>> ListMyReviewsAsync(
        long userId, CancellationToken ct = default) =>
        db.FcrReviewAssignments.AsNoTracking()
            .Where(a => a.AssignedUserId == userId && a.AssignmentStatus == "Active")
            .Join(db.FcrWorkflowStages, a => a.WorkflowStageId, s => s.Id,
                (a, s) => new { a, s })
            .Join(db.FcrWorkflowInstances, x => x.s.WorkflowInstanceId, w => w.Id,
                (x, w) => new { x.a, x.s, w })
            .Where(x => x.a.Cycle == x.w.Cycle && x.s.StageOrder == x.w.CurrentStage)
            .Join(db.FcrRequests, x => x.w.FcrRequestId, r => r.Id,
                (x, r) => new ReviewTaskDto(
                    x.a.Id, r.Id, r.RequestNumber, r.Subject, x.s.StageName,
                    x.w.Cycle, r.SubmissionStatus, x.a.AssignedAt, null))
            .ToListAsync(ct);

    public async Task<RequestDetailDto> SubmitDecisionAsync(
        string requestId, long userId, ReviewDecisionInput input, CancellationToken ct = default)
    {
        var action = (input.Action ?? "").Trim().ToLowerInvariant();
        if (!ReviewActions.All.Contains(action))
            throw new FcrValidationException(
                $"action must be one of: {string.Join(", ", ReviewActions.All)}");

        var req = await db.FcrRequests.FindAsync([requestId], ct)
            ?? throw new FcrNotFoundException(0);
        var wf = await db.FcrWorkflowInstances
            .Include(w => w.Stages).ThenInclude(s => s.Assignments)
            .FirstOrDefaultAsync(w => w.FcrRequestId == requestId, ct)
            ?? throw new FcrValidationException("No workflow is configured for this request");
        if (wf.WorkflowStatus is not (WorkflowStatuses.InProgress
            or WorkflowStatuses.AwaitingApproval or WorkflowStatuses.AwaitingReview))
            throw new FcrValidationException(
                $"The workflow is '{wf.WorkflowStatus}' — no active review");

        var stage = wf.Stages.First(s => s.StageOrder == wf.CurrentStage);
        var my = stage.Assignments
            .Where(a => a.Cycle == wf.Cycle && a.AssignedUserId == userId
                && a.AssignmentStatus == "Active")
            .OrderBy(a => a.AssignmentOrder).FirstOrDefault();
        if (my is null)
            throw new FcrValidationException(
                "You have no active assignment for the current stage");

        var user = await db.Users.FindAsync([userId], ct);
        var previous = req.SubmissionStatus;
        var now = Now();

        if (action == ReviewActions.Comment)
        {
            var text = (input.Comment ?? "").Trim();
            if (text.Length == 0) throw new FcrValidationException("Comment text required");
            db.FcrReviews.Add(new FcrReview
            {
                FcrRequestId = requestId, WorkflowInstanceId = wf.Id,
                ReviewAssignmentId = my.Id, ReviewerUserId = userId,
                ReviewerName = user?.DisplayName ?? user?.Username ?? "",
                StageName = stage.StageName, ReviewCycle = wf.Cycle,
                ActionType = action, Comment = text,
                PreviousStatus = previous, ResultingStatus = previous,
                CreatedAt = now,
            });
            db.FcrRequestComments.Add(new FcrRequestComment
            {
                FcrRequestId = requestId, UserId = userId,
                CommentText = text, CommentType = "reviewer", CreatedAt = now,
            });
            Notify(await AdminUserIdsAsync(ct), "comment",
                $"Review comment on {req.RequestNumber}", requestId);
            req.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            return (await GetAsync(requestId, ct))!;
        }

        string resulting;
        if (action == ReviewActions.Reject)
        {
            stage.StageStatus = "Rejected";
            wf.WorkflowStatus = WorkflowStatuses.Rejected;
            req.SubmissionStatus = resulting = RequestStatuses.Rejected;
            foreach (var a in stage.Assignments.Where(x => x.Cycle == wf.Cycle && x.Id != my.Id
                && x.AssignmentStatus is "Pending" or "Active"))
                a.AssignmentStatus = "Skipped";
            Notify(await AdminUserIdsAsync(ct), "rejected",
                $"Request {req.RequestNumber} was rejected by {user?.DisplayName ?? user?.Username}",
                requestId);
        }
        else if (action == ReviewActions.Return)
        {
            stage.StageStatus = "Returned for Correction";
            wf.WorkflowStatus = WorkflowStatuses.ReturnedForCorrection;
            req.SubmissionStatus = resulting = RequestStatuses.ReturnedToAdmin;
            foreach (var a in stage.Assignments.Where(x => x.Cycle == wf.Cycle && x.Id != my.Id
                && x.AssignmentStatus is "Pending" or "Active"))
                a.AssignmentStatus = "Skipped";
            Notify(await AdminUserIdsAsync(ct), "returned",
                $"Request {req.RequestNumber} was returned for correction", requestId);
        }
        else // approve
        {
            my.AssignmentStatus = "Completed";
            my.CompletedAt = now;
            var cycleAssignments = stage.Assignments
                .Where(a => a.Cycle == wf.Cycle).ToList();
            var approvals = cycleAssignments.Count(a => a.AssignmentStatus == "Completed");

            if (wf.ReviewMode == ReviewModes.Sequential)
            {
                var next = cycleAssignments
                    .Where(a => a.AssignmentStatus == "Pending")
                    .OrderBy(a => a.AssignmentOrder).FirstOrDefault();
                if (next is not null)
                {
                    next.AssignmentStatus = "Active";
                    Notify([next.AssignedUserId], "assignment",
                        $"You are assigned to review request {req.RequestNumber}", requestId);
                }
            }

            var satisfied = wf.ReviewMode == ReviewModes.Sequential
                ? approvals >= cycleAssignments.Count
                : DecisionRules.IsSatisfied(
                    stage.DecisionRule, approvals, cycleAssignments.Count);

            if (!satisfied)
            {
                resulting = req.SubmissionStatus;
            }
            else
            {
                foreach (var a in cycleAssignments.Where(x =>
                    x.AssignmentStatus is "Pending" or "Active"))
                    a.AssignmentStatus = "Skipped";
                stage.StageStatus = "Completed";
                var nextStage = wf.Stages
                    .Where(s => s.StageOrder > stage.StageOrder)
                    .OrderBy(s => s.StageOrder).FirstOrDefault();
                if (nextStage is not null)
                {
                    wf.CurrentStage = nextStage.StageOrder;
                    wf.WorkflowStatus = nextStage.StageName == "Approval"
                        ? WorkflowStatuses.AwaitingApproval : WorkflowStatuses.AwaitingReview;
                    ActivateStage(nextStage, wf);
                    Notify(nextStage.Assignments
                            .Where(a => a.Cycle == wf.Cycle && a.AssignmentStatus == "Active")
                            .Select(a => a.AssignedUserId),
                        "assignment",
                        $"Request {req.RequestNumber} awaits your {nextStage.StageName}",
                        requestId);
                    resulting = req.SubmissionStatus;
                }
                else
                {
                    wf.WorkflowStatus = WorkflowStatuses.Approved;
                    wf.CompletedAt = now;
                    req.SubmissionStatus = resulting = RequestStatuses.Approved;
                    Notify(await AdminUserIdsAsync(ct), "approved",
                        $"Request {req.RequestNumber} was approved", requestId);
                }
            }
        }

        db.FcrReviews.Add(new FcrReview
        {
            FcrRequestId = requestId, WorkflowInstanceId = wf.Id,
            ReviewAssignmentId = my.Id, ReviewerUserId = userId,
            ReviewerName = user?.DisplayName ?? user?.Username ?? "",
            StageName = stage.StageName, ReviewCycle = wf.Cycle,
            ActionType = action, Comment = input.Comment,
            PreviousStatus = previous, ResultingStatus = resulting,
            CreatedAt = now,
        });
        wf.UpdatedAt = req.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return (await GetAsync(requestId, ct))!;
    }

    /// <summary>Admin resubmits a returned/rejected request — new review cycle,
    /// previous review history retained in fcr_reviews (§2.6).</summary>
    public async Task<RequestDetailDto?> ResubmitAsync(string requestId, CancellationToken ct = default)
    {
        var req = await db.FcrRequests.FindAsync([requestId], ct);
        if (req is null) return null;
        if (req.SubmissionStatus is not (RequestStatuses.ReturnedToAdmin
            or RequestStatuses.Rejected))
            throw new FcrValidationException(
                $"Only returned or rejected requests can be resubmitted (current: '{req.SubmissionStatus}')");
        var wf = await db.FcrWorkflowInstances
            .Include(w => w.Stages).ThenInclude(s => s.Assignments)
            .FirstOrDefaultAsync(w => w.FcrRequestId == requestId, ct)
            ?? throw new FcrValidationException("No workflow is configured for this request");

        var now = Now();
        wf.Cycle += 1;
        foreach (var stage in wf.Stages.OrderBy(s => s.StageOrder))
        {
            var prior = stage.Assignments.Where(a => a.Cycle < wf.Cycle).ToList();
            foreach (var a in prior) a.AssignmentStatus = "Superseded";
            foreach (var (a, i) in prior.OrderBy(a => a.AssignmentOrder).Select((a, i) => (a, i)))
                stage.Assignments.Add(new FcrReviewAssignment
                {
                    AssignedUserId = a.AssignedUserId,
                    AssignmentOrder = i, Cycle = wf.Cycle,
                    AssignmentStatus = "Pending", AssignedAt = now,
                });
            stage.StageStatus = "Pending";
        }
        wf.CurrentStage = wf.Stages.Min(s => s.StageOrder);
        wf.WorkflowStatus = WorkflowStatuses.InProgress;
        wf.CompletedAt = null;
        wf.UpdatedAt = now;
        req.SubmissionStatus = RequestStatuses.UnderReview;
        req.UpdatedAt = now;

        var firstStage = wf.Stages.First(s => s.StageOrder == wf.CurrentStage);
        firstStage.StageStatus = "Active";
        ActivateStage(firstStage, wf);
        Notify(firstStage.Assignments
                .Where(a => a.Cycle == wf.Cycle && a.AssignmentStatus == "Active")
                .Select(a => a.AssignedUserId),
            "assignment",
            $"Request {req.RequestNumber} was resubmitted for review (cycle {wf.Cycle})",
            requestId);
        await db.SaveChangesAsync(ct);
        return await GetAsync(requestId, ct);
    }

    public async Task<List<ReviewActionDto>> GetReviewHistoryAsync(
        string requestId, CancellationToken ct = default) =>
        await db.FcrReviews.AsNoTracking()
            .Where(r => r.FcrRequestId == requestId)
            .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
            .Select(r => new ReviewActionDto(
                r.Id, r.ReviewerUserId, r.ReviewerName, r.StageName, r.ReviewCycle,
                r.ActionType, r.Comment, r.PreviousStatus, r.ResultingStatus, r.CreatedAt))
            .ToListAsync(ct);

    public async Task<List<WorkflowDefinitionDto>> ListWorkflowDefinitionsAsync(
        CancellationToken ct = default) =>
        await db.WorkflowDefinitions.AsNoTracking()
            .OrderBy(d => d.Id)
            .Select(d => new WorkflowDefinitionDto(d.Id, d.WorkflowName, d.ReviewMode,
                d.Configuration, d.IsActive, d.CreatedAt))
            .ToListAsync(ct);

    public async Task<WorkflowDefinitionDto> CreateWorkflowDefinitionAsync(
        WorkflowDefinitionInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.WorkflowName))
            throw new FcrValidationException("workflow_name required");
        if (!ReviewModes.All.Contains(input.ReviewMode))
            throw new FcrValidationException(
                $"review_mode must be one of: {string.Join(", ", ReviewModes.All)}");
        var d = new WorkflowDefinition
        {
            WorkflowName = input.WorkflowName.Trim(),
            ReviewMode = input.ReviewMode,
            Configuration = string.IsNullOrWhiteSpace(input.Configuration)
                ? "{}" : input.Configuration.Trim(),
            IsActive = input.IsActive,
            CreatedAt = Now(),
        };
        db.WorkflowDefinitions.Add(d);
        await db.SaveChangesAsync(ct);
        return new WorkflowDefinitionDto(d.Id, d.WorkflowName, d.ReviewMode,
            d.Configuration, d.IsActive, d.CreatedAt);
    }

    public async Task<NotificationListDto> ListNotificationsAsync(
        long userId, CancellationToken ct = default)
    {
        var items = await db.FcrNotifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.Id).Take(50)
            .Select(n => new NotificationDto(n.Id, n.NotificationType, n.Message,
                n.FcrRequestId, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);
        var unread = await db.FcrNotifications
            .CountAsync(n => n.UserId == userId && !n.IsRead, ct);
        return new NotificationListDto(unread, items);
    }

    public async Task<bool> MarkNotificationReadAsync(
        long userId, long notificationId, CancellationToken ct = default)
    {
        var n = await db.FcrNotifications
            .FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId, ct);
        if (n is null) return false;
        n.IsRead = true;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static RequestDetailDto ToDto(FcrRequest r, FcrWorkflowInstance? wf)
    {
        WorkflowDto? wfDto = wf is null ? null : new WorkflowDto(
            wf.Id, wf.WorkflowStatus, wf.ReviewMode, wf.CurrentStage, wf.Cycle,
            wf.Stages.OrderBy(s => s.StageOrder)
                .Select(s => new StageDto(
                    s.StageName, s.StageOrder, s.DecisionRule, s.StageStatus,
                    s.Assignments.Where(a => a.Cycle == wf.Cycle)
                        .OrderBy(a => a.AssignmentOrder)
                        .Select(a => new ReviewerAssignmentDto(
                            a.AssignedUserId, a.AssignedUser?.Username ?? "",
                            a.AssignmentOrder, a.AssignmentStatus)).ToList()))
                .ToList(),
            wf.StartedAt);
        return new RequestDetailDto(
            r.Id, r.RequestNumber, r.RequesterUserId, r.Requester?.Username ?? "",
            r.Subject, r.Justification, r.Description, r.SubmissionStatus,
            r.SubmittedAt, r.CreatedAt, r.UpdatedAt, r.LinkedFcrId, r.FcrCode, r.Category,
            r.Contractor, r.Subcontractor, r.ArchivePath is not null, r.ArchivedAt,
            r.Comments.OrderBy(c => c.CreatedAt).Select(c => new CommentDto(
                c.Id, c.UserId, c.User?.Username ?? "", c.CommentText, c.CommentType, c.CreatedAt)).ToList(),
            r.Attachments.OrderBy(a => a.UploadedAt).Select(a => new AttachmentDto(
                a.Id, a.UploadedByUserId, a.UploadedBy?.Username ?? "",
                a.OriginalFileName, a.ContentType, a.FileSize, a.UploadedAt)).ToList(),
            wfDto);
    }
}
