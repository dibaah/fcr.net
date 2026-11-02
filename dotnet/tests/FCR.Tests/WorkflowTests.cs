using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FCR.Tests;

/// <summary>
/// Phase 6 tests: sequential/parallel review execution, decision rules,
/// rejection + return-for-correction routing, resubmission cycles,
/// final approval, review history and in-app notifications.
/// </summary>
public class WorkflowTests(ApiParityTests.Fixture fixture)
    : IClassFixture<ApiParityTests.Fixture>
{
    private static int _codeSeq = 500;

    private static async Task<JsonElement> Body(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private async Task<HttpClient?> Login(string username, string password)
    {
        var c = fixture.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login",
            new Dictionary<string, object?> { ["username"] = username, ["password"] = password });
        return r.IsSuccessStatusCode ? c : null;
    }

    private async Task<HttpClient> MakeUser(HttpClient admin, string username, params string[] roles)
    {
        var create = await admin.PostAsJsonAsync("/api/admin/users",
            new Dictionary<string, object?>
            {
                ["username"] = username, ["display_name"] = username,
                ["password"] = "pass1234", ["roles"] = roles,
            });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var c = await Login(username, "pass1234");
        Assert.NotNull(c);
        return c;
    }

    private static async Task<long> MyId(HttpClient c) =>
        long.Parse((await Body(await c.GetAsync("/api/auth/me")))
            .GetProperty("id").GetString()!);

    private static async Task<string> Submit(HttpClient c, string subject)
    {
        var r = await c.PostAsJsonAsync("/api/requests",
            new { subject, justification = "j", description = "d" });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return (await Body(r)).GetProperty("id").GetString()!;
    }

    private static async Task Prep(HttpClient admin, string id, string mode,
        string rule, params long[] reviewerIds)
    {
        var r = await admin.PutAsJsonAsync($"/api/admin/requests/{id}/workflow",
            new { review_mode = mode, decision_rule = rule, reviewer_user_ids = reviewerIds });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    private static async Task Send(HttpClient admin, string id)
    {
        var r = await admin.PutAsJsonAsync($"/api/admin/requests/{id}",
            new { fcr_code = $"FCR-CT-PI-{Interlocked.Increment(ref _codeSeq):000}",
                  category = "Permanent" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        r = await admin.PostAsync($"/api/admin/requests/{id}/submit-for-review", null);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    private static async Task<HttpResponseMessage> Decide(
        HttpClient c, string requestId, string action, string? comment = null) =>
        await c.PostAsJsonAsync($"/api/reviews/{requestId}/decision",
            new { action, comment });

    [Fact]
    public async Task Sequential_Review_Order_Enforced()
    {
        var admin = await Login("admin", "admin"); Assert.NotNull(admin);
        var requester = await MakeUser(admin, "wfreq1", "Requester");
        var r1 = await MakeUser(admin, "wfrev1", "Reviewer");
        var r2 = await MakeUser(admin, "wfrev2", "Reviewer");
        var (id1, id2) = (await MyId(r1), await MyId(r2));

        var id = await Submit(requester, "Sequential test");
        await Prep(admin, id, "sequential", "all", id1, id2);
        await Send(admin, id);

        // r2 cannot act before r1
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Decide(r2, id, "approve")).StatusCode);
        // r1 approves -> r2's turn
        Assert.Equal(HttpStatusCode.OK, (await Decide(r1, id, "approve", "ok")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Decide(r2, id, "approve")).StatusCode);

        var dto = await Body(await admin.GetAsync($"/api/requests/{id}"));
        Assert.Equal("Approved", dto.GetProperty("submission_status").GetString());
        Assert.Equal("Approved",
            dto.GetProperty("workflow").GetProperty("workflow_status").GetString());

        // history records both approvals, immutable order
        var hist = (await Body(await admin.GetAsync($"/api/requests/{id}/history")))
            .EnumerateArray().ToList();
        Assert.Equal(2, hist.Count(h => h.GetProperty("action_type").GetString() == "approve"));
        Assert.All(hist, h => Assert.Equal(1, h.GetProperty("review_cycle").GetInt32()));
    }

    [Fact]
    public async Task Parallel_DecisionRules_Any_And_Count()
    {
        var admin = await Login("admin", "admin"); Assert.NotNull(admin);
        var requester = await MakeUser(admin, "wfreq2", "Requester");
        var r1 = await MakeUser(admin, "wfrev3", "Reviewer");
        var r2 = await MakeUser(admin, "wfrev4", "Reviewer");
        var (id1, id2) = (await MyId(r1), await MyId(r2));

        // parallel + any: one approval completes the stage
        var id = await Submit(requester, "Parallel any");
        await Prep(admin, id, "parallel", "any", id1, id2);
        await Send(admin, id);
        Assert.Equal(HttpStatusCode.OK, (await Decide(r1, id, "approve")).StatusCode);
        var dto = await Body(await admin.GetAsync($"/api/requests/{id}"));
        Assert.Equal("Approved", dto.GetProperty("submission_status").GetString());
        // r2's assignment was skipped by the any-rule
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Decide(r2, id, "approve")).StatusCode);

        // parallel + all: both must approve
        var id2r = await Submit(requester, "Parallel all");
        await Prep(admin, id2r, "parallel", "all", id1, id2);
        await Send(admin, id2r);
        Assert.Equal(HttpStatusCode.OK, (await Decide(r1, id2r, "approve")).StatusCode);
        dto = await Body(await admin.GetAsync($"/api/requests/{id2r}"));
        Assert.Equal("Under Review", dto.GetProperty("submission_status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await Decide(r2, id2r, "approve")).StatusCode);
        dto = await Body(await admin.GetAsync($"/api/requests/{id2r}"));
        Assert.Equal("Approved", dto.GetProperty("submission_status").GetString());
    }

    [Fact]
    public async Task Reject_And_Return_Route_To_Admin_And_Resubmit()
    {
        var admin = await Login("admin", "admin"); Assert.NotNull(admin);
        var requester = await MakeUser(admin, "wfreq3", "Requester");
        var rev = await MakeUser(admin, "wfrev5", "Reviewer");
        var revId = await MyId(rev);

        // return-for-correction -> Returned to Admin
        var id = await Submit(requester, "Return test");
        await Prep(admin, id, "parallel", "all", revId);
        await Send(admin, id);
        Assert.Equal(HttpStatusCode.OK,
            (await Decide(rev, id, "return", "needs more detail")).StatusCode);
        var dto = await Body(await admin.GetAsync($"/api/requests/{id}"));
        Assert.Equal("Returned to Admin", dto.GetProperty("submission_status").GetString());
        Assert.Equal("Returned for Correction",
            dto.GetProperty("workflow").GetProperty("workflow_status").GetString());
        // appears in the admin queue filter
        var returned = (await Body(await admin.GetAsync(
                "/api/admin/requests?status=Returned%20to%20Admin"))).EnumerateArray();
        Assert.Contains(returned, q => q.GetProperty("id").GetString() == id);

        // admin receives a 'returned' notification
        var notifs = await Body(await admin.GetAsync("/api/my/notifications"));
        Assert.Contains(notifs.GetProperty("items").EnumerateArray(),
            n => n.GetProperty("notification_type").GetString() == "returned");

        // resubmit -> new cycle, same reviewer, history retained
        var resub = await admin.PostAsync($"/api/admin/requests/{id}/resubmit", null);
        Assert.Equal(HttpStatusCode.OK, resub.StatusCode);
        dto = await Body(resub);
        Assert.Equal("Under Review", dto.GetProperty("submission_status").GetString());
        Assert.Equal(2, dto.GetProperty("workflow").GetProperty("cycle").GetInt32());

        var hist = (await Body(await admin.GetAsync($"/api/requests/{id}/history")))
            .EnumerateArray().ToList();
        Assert.Contains(hist, h => h.GetProperty("action_type").GetString() == "return");
        Assert.Equal(HttpStatusCode.OK, (await Decide(rev, id, "approve")).StatusCode);
        dto = await Body(await admin.GetAsync($"/api/requests/{id}"));
        Assert.Equal("Approved", dto.GetProperty("submission_status").GetString());

        // reject path -> Rejected, distinguishable from return
        var id2 = await Submit(requester, "Reject test");
        await Prep(admin, id2, "parallel", "all", revId);
        await Send(admin, id2);
        Assert.Equal(HttpStatusCode.OK, (await Decide(rev, id2, "reject", "no")).StatusCode);
        dto = await Body(await admin.GetAsync($"/api/requests/{id2}"));
        Assert.Equal("Rejected", dto.GetProperty("submission_status").GetString());
        // rejected requests can also be resubmitted
        Assert.Equal(HttpStatusCode.OK,
            (await admin.PostAsync($"/api/admin/requests/{id2}/resubmit", null)).StatusCode);
    }

    [Fact]
    public async Task Approver_Stage_And_Final_Approval()
    {
        var admin = await Login("admin", "admin"); Assert.NotNull(admin);
        var requester = await MakeUser(admin, "wfreq4", "Requester");
        var rev = await MakeUser(admin, "wfrev6", "Reviewer");
        var app = await MakeUser(admin, "wfapp1", "Approver");
        var (revId, appId) = (await MyId(rev), await MyId(app));

        var id = await Submit(requester, "Approval stage test");
        var prep = await admin.PutAsJsonAsync($"/api/admin/requests/{id}/workflow",
            new { review_mode = "parallel", decision_rule = "all",
                  reviewer_user_ids = new[] { revId },
                  approver_user_ids = new[] { appId } });
        Assert.Equal(HttpStatusCode.OK, prep.StatusCode);
        await Send(admin, id);

        // approver cannot act before the review stage completes
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Decide(app, id, "approve")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Decide(rev, id, "approve")).StatusCode);

        var dto = await Body(await admin.GetAsync($"/api/requests/{id}"));
        Assert.Equal("Awaiting Approval",
            dto.GetProperty("workflow").GetProperty("workflow_status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await Decide(app, id, "approve")).StatusCode);
        dto = await Body(await admin.GetAsync($"/api/requests/{id}"));
        Assert.Equal("Approved", dto.GetProperty("submission_status").GetString());
        Assert.Equal("Approved",
            dto.GetProperty("workflow").GetProperty("workflow_status").GetString());
    }

    [Fact]
    public async Task ReviewComment_Notifications_And_Definitions()
    {
        var admin = await Login("admin", "admin"); Assert.NotNull(admin);
        var requester = await MakeUser(admin, "wfreq5", "Requester");
        var rev = await MakeUser(admin, "wfrev7", "Reviewer");
        var nonRev = await MakeUser(admin, "wfrev8", "Requester");
        var revId = await MyId(rev);

        // reviewer endpoints gated to Reviewer role
        Assert.Equal(HttpStatusCode.Forbidden,
            (await nonRev.GetAsync("/api/reviews/mine")).StatusCode);

        var id = await Submit(requester, "Comment test");
        await Prep(admin, id, "parallel", "all", revId);
        await Send(admin, id);

        // assignment notification for the reviewer
        var mine = (await Body(await rev.GetAsync("/api/my/notifications")));
        Assert.Contains(mine.GetProperty("items").EnumerateArray(),
            n => n.GetProperty("notification_type").GetString() == "assignment");
        Assert.True(mine.GetProperty("unread").GetInt32() > 0);

        // /api/reviews/mine lists the active assignment
        var tasks = (await Body(await rev.GetAsync("/api/reviews/mine")))
            .EnumerateArray().ToList();
        Assert.Contains(tasks, t => t.GetProperty("request_id").GetString() == id);

        // comment without a decision — review stays open
        Assert.Equal(HttpStatusCode.OK,
            (await Decide(rev, id, "comment", "question about scope")).StatusCode);
        var dto = await Body(await admin.GetAsync($"/api/requests/{id}"));
        Assert.Equal("Under Review", dto.GetProperty("submission_status").GetString());
        Assert.Contains(dto.GetProperty("comments").EnumerateArray(),
            c => c.GetProperty("comment_type").GetString() == "reviewer");

        // mark notification read
        var nid = mine.GetProperty("items")[0].GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.OK,
            (await rev.PostAsync($"/api/my/notifications/{nid}/read", null)).StatusCode);

        // workflow definitions CRUD-lite
        var def = await admin.PostAsJsonAsync("/api/workflow-definitions",
            new { workflow_name = "Standard", review_mode = "sequential",
                  configuration = "{\"decision_rule\":\"all\"}" });
        Assert.Equal(HttpStatusCode.Created, def.StatusCode);
        var defs = (await Body(await admin.GetAsync("/api/workflow-definitions")))
            .EnumerateArray();
        Assert.Contains(defs, d => d.GetProperty("workflow_name").GetString() == "Standard");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await requester.GetAsync("/api/workflow-definitions")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync("/api/workflow-definitions",
                new { workflow_name = "Bad", review_mode = "sideways" })).StatusCode);
    }
}
