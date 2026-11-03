using System.Security.Claims;
using FCR.Application;
using FCR.Domain;
using FCR.Infrastructure.Persistence;

namespace FCR.Web;

/// <summary>
/// Requester module (§2.2) + FCR Admin module (§2.3) endpoints.
/// Ownership is enforced server-side: requesters see/act on their own requests,
/// the AdminConsole policy (FCR Admin / System Administrator) gates admin routes.
/// </summary>
public static class RequestEndpoints
{
    public static void MapRequestApi(this WebApplication app)
    {
        var req = app.MapGroup("/api/requests").RequireAuthorization(PolicyNames.FcrModuleAccess);

        req.MapPost("/", async Task<IResult> (
            CreateRequestInput input, IRequestService svc, HttpContext http) =>
        {
            try
            {
                var dto = await svc.CreateAsync(CurrentUserId(http), input);
                return Results.Json(dto, statusCode: 201);
            }
            catch (FcrValidationException ex) { return Detail(400, ex.Message); }
        }).RequireAuthorization(PolicyNames.RequesterAccess);

        req.MapGet("/", async Task<IResult> (
            IRequestService svc, HttpContext http, string? status) =>
        {
            var onlyUser = IsAdmin(http) ? (long?)null : CurrentUserId(http);
            return Results.Ok(await svc.ListAsync(onlyUser, status));
        });

        req.MapGet("/{id}", async Task<IResult> (
            string id, IRequestService svc, HttpContext http) =>
        {
            if (!await CanView(id, svc, http)) return Detail(404, "Request not found");
            var dto = await svc.GetAsync(id);
            return dto is null ? Detail(404, "Request not found") : Results.Ok(dto);
        });

        req.MapPost("/{id}/comments", async Task<IResult> (
            string id, CommentInput input, IRequestService svc, HttpContext http) =>
        {
            if (!await CanView(id, svc, http)) return Detail(404, "Request not found");
            var type = IsAdmin(http) ? "admin" : "requester";
            try
            {
                var c = await svc.AddCommentAsync(id, CurrentUserId(http), input.CommentText, type);
                return c is null ? Detail(404, "Request not found")
                    : Results.Json(c, statusCode: 201);
            }
            catch (FcrValidationException ex) { return Detail(400, ex.Message); }
        });

        req.MapPost("/{id}/attachments", async Task<IResult> (
            string id, IFormFile? file, IRequestService svc, HttpContext http) =>
        {
            if (file is null || file.Length == 0) return Detail(400, "No file uploaded");
            if (!await CanView(id, svc, http)) return Detail(404, "Request not found");
            try
            {
                using var stream = file.OpenReadStream();
                var a = await svc.AddAttachmentAsync(
                    id, CurrentUserId(http), file.FileName, file.ContentType, stream);
                return Results.Json(a, statusCode: 201);
            }
            catch (FcrNotFoundException) { return Detail(404, "Request not found"); }
            catch (FcrValidationException ex) { return Detail(400, ex.Message); }
        }).DisableAntiforgery();

        req.MapGet("/{id}/attachments/{attId:long}/download", async Task<IResult> (
            string id, long attId, IRequestService svc, HttpContext http) =>
        {
            if (!await CanView(id, svc, http)) return Detail(404, "Request not found");
            var a = await svc.GetAttachmentAsync(id, attId);
            return a is null ? Detail(404, "Attachment not found")
                : Results.File(a.PhysicalPath, a.ContentType, a.DownloadName);
        });

        req.MapDelete("/{id}/attachments/{attId:long}", async Task<IResult> (
            string id, long attId, IRequestService svc, HttpContext http) =>
        {
            if (!await CanView(id, svc, http)) return Detail(404, "Request not found");
            try
            {
                return await svc.DeleteAttachmentAsync(id, attId, CurrentUserId(http), IsAdmin(http))
                    ? Results.Ok(new { deleted = attId })
                    : Detail(404, "Attachment not found");
            }
            catch (FcrValidationException ex) { return Detail(400, ex.Message); }
        });

        // ── FCR Admin console (§2.3) ───────────────────────────────────
        var admin = app.MapGroup("/api/admin/requests")
            .RequireAuthorization(PolicyNames.AdminConsole);

        admin.MapGet("/", async Task<IResult> (
            IRequestService svc, string? status) =>
            Results.Ok(await svc.ListAsync(null, status)));

        admin.MapGet("/reviewers", async Task<IResult> (IUserService users) =>
            Results.Ok((await users.ListUsersAsync())
                .Where(u => u.IsActive && u.Roles.Contains(RoleNames.Reviewer))
                .Select(u => new { id = u.Id, username = u.Username, display_name = u.DisplayName })
                .ToList()));

        admin.MapPut("/{id}", async Task<IResult> (
            string id, AdminUpdateInput input, IRequestService svc) =>
        {
            try
            {
                var dto = await svc.AdminUpdateAsync(id, input);
                return dto is null ? Detail(404, "Request not found") : Results.Ok(dto);
            }
            catch (FcrValidationException ex) { return Detail(400, ex.Message); }
        });

        admin.MapPost("/{id}/archive", async Task<IResult> (
            string id, IRequestService svc) =>
        {
            try
            {
                var archive = await svc.ArchiveAsync(id);
                return archive is null ? Detail(404, "Request not found") : Results.Ok(archive);
            }
            catch (FcrValidationException ex) { return Detail(400, ex.Message); }
        });

        admin.MapPost("/{id}/create-fcr", async Task<IResult> (
            string id, IRequestService svc) =>
        {
            try
            {
                var dto = await svc.CreateLinkedFcrAsync(id);
                return dto is null ? Detail(404, "Request not found") : Results.Ok(dto);
            }
            catch (FcrValidationException ex) { return Detail(400, ex.Message); }
            catch (FcrDuplicateCodeException ex) { return Detail(409, ex.Message); }
        });

        admin.MapPut("/{id}/workflow", async Task<IResult> (
            string id, WorkflowPrepInput input, IRequestService svc) =>
        {
            try
            {
                var dto = await svc.PrepareWorkflowAsync(id, input);
                return dto is null ? Detail(404, "Request not found") : Results.Ok(dto);
            }
            catch (FcrValidationException ex) { return Detail(400, ex.Message); }
        });

        admin.MapPost("/{id}/submit-for-review", async Task<IResult> (
            string id, IRequestService svc) =>
        {
            try
            {
                var dto = await svc.SubmitForReviewAsync(id);
                return dto is null ? Detail(404, "Request not found") : Results.Ok(dto);
            }
            catch (FcrValidationException ex) { return Detail(400, ex.Message); }
        });

        admin.MapPost("/{id}/resubmit", async Task<IResult> (
            string id, IRequestService svc) =>
        {
            try
            {
                var dto = await svc.ResubmitAsync(id);
                return dto is null ? Detail(404, "Request not found") : Results.Ok(dto);
            }
            catch (FcrValidationException ex) { return Detail(400, ex.Message); }
        });

        var defs = app.MapGroup("/api/workflow-definitions")
            .RequireAuthorization(PolicyNames.AdminConsole);
        defs.MapGet("/", async Task<IResult> (IRequestService svc) =>
            Results.Ok(await svc.ListWorkflowDefinitionsAsync()));
        defs.MapPost("/", async Task<IResult> (
            WorkflowDefinitionInput input, IRequestService svc) =>
        {
            try { return Results.Json(await svc.CreateWorkflowDefinitionAsync(input), statusCode: 201); }
            catch (FcrValidationException ex) { return Detail(400, ex.Message); }
        });

        // ── Reviewer module (Phase 6) ──────────────────────────────────
        var rev = app.MapGroup("/api/reviews")
            .RequireAuthorization(PolicyNames.ReviewerAccess);
        rev.MapGet("/mine", async Task<IResult> (
            IRequestService svc, HttpContext http) =>
            Results.Ok(await svc.ListMyReviewsAsync(CurrentUserId(http))));
        rev.MapPost("/{requestId}/decision", async Task<IResult> (
            string requestId, ReviewDecisionInput input,
            IRequestService svc, HttpContext http) =>
        {
            try
            {
                return Results.Ok(await svc.SubmitDecisionAsync(
                    requestId, CurrentUserId(http), input));
            }
            catch (FcrNotFoundException) { return Detail(404, "Request not found"); }
            catch (FcrValidationException ex) { return Detail(400, ex.Message); }
        });

        // workflow history — any viewer of the request
        req.MapGet("/{id}/history", async Task<IResult> (
            string id, IRequestService svc, HttpContext http) =>
        {
            if (!await CanView(id, svc, http)) return Detail(404, "Request not found");
            return Results.Ok(await svc.GetReviewHistoryAsync(id));
        });

        // ── In-app notifications (Phase 6, §9) ────────────────────────
        var notif = app.MapGroup("/api/my/notifications").RequireAuthorization(PolicyNames.FcrModuleAccess);
        notif.MapGet("/", async Task<IResult> (IRequestService svc, HttpContext http) =>
            Results.Ok(await svc.ListNotificationsAsync(CurrentUserId(http))));
        notif.MapPost("/{id:long}/read", async Task<IResult> (
            long id, IRequestService svc, HttpContext http) =>
            await svc.MarkNotificationReadAsync(CurrentUserId(http), id)
                ? Results.Ok(new { read = id }) : Detail(404, "Notification not found"));
    }

    private static long CurrentUserId(HttpContext http) =>
        long.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    private static bool IsAdmin(HttpContext http) =>
        http.User.IsInRole(RoleNames.FcrAdmin) || http.User.IsInRole(RoleNames.SystemAdministrator);

    private static async Task<bool> CanView(string id, IRequestService svc, HttpContext http)
    {
        var owner = await svc.OwnerIdAsync(id);
        if (owner is null) return false;
        return IsAdmin(http) || owner == CurrentUserId(http);
    }

    private static IResult Detail(int status, string message) =>
        Results.Json(new Dictionary<string, object?> { ["detail"] = message }, statusCode: status);
}
