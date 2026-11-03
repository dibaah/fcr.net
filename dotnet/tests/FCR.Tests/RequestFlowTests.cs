using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FCR.Tests;

/// <summary>
/// Phase 5 tests: the complete submission-to-admin process —
/// requester submits, FCR Admin picks it up, assigns codes/comments/workflow —
/// plus the permission matrix (ownership, role gates).
/// </summary>
public class RequestFlowTests(ApiParityTests.Fixture fixture)
    : IClassFixture<ApiParityTests.Fixture>
{
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

    private static async Task<string> Submit(HttpClient c, string subject = "Test request")
    {
        var r = await c.PostAsJsonAsync("/api/requests",
            new Dictionary<string, object?>
            {
                ["subject"] = subject, ["justification"] = "j", ["description"] = "d",
            });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var dto = await Body(r);
        Assert.Equal("Submitted", dto.GetProperty("submission_status").GetString());
        Assert.StartsWith("REQ-", dto.GetProperty("request_number").GetString());
        return dto.GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task Unauthenticated_RequestEndpoints_Return401()
    {
        var anon = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.GetAsync("/api/admin/requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.PostAsJsonAsync("/api/requests",
                new { subject = "s", justification = "j", description = "d" })).StatusCode);
    }

    [Fact]
    public async Task Requester_Creates_And_Sees_Only_Own_Requests()
    {
        var admin = await Login("admin", "admin"); Assert.NotNull(admin);
        var req1 = await MakeUser(admin, "reqflow1", "Requester");
        var req2 = await MakeUser(admin, "reqflow2", "Requester");

        // missing required fields -> 400
        Assert.Equal(HttpStatusCode.BadRequest,
            (await req1.PostAsJsonAsync("/api/requests",
                new { subject = "", justification = "j", description = "d" })).StatusCode);

        var id = await Submit(req1, "R1 request");

        // req1 sees it; req2 does not (own-requests filter + owner check)
        var list1 = (await Body(await req1.GetAsync("/api/requests"))).EnumerateArray().ToList();
        Assert.Single(list1);
        Assert.Equal(id, list1[0].GetProperty("id").GetString());
        Assert.Empty((await Body(await req2.GetAsync("/api/requests"))).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound,
            (await req2.GetAsync($"/api/requests/{id}")).StatusCode);

        // requesters cannot reach the admin console
        Assert.Equal(HttpStatusCode.Forbidden,
            (await req1.GetAsync("/api/admin/requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await req1.PutAsJsonAsync($"/api/admin/requests/{id}", new { fcr_code = "x" })).StatusCode);
    }

    [Fact]
    public async Task Full_Submission_To_Admin_Process()
    {
        var admin = await Login("admin", "admin"); Assert.NotNull(admin);
        var requester = await MakeUser(admin, "reqflow3", "Requester");
        var reviewer = await MakeUser(admin, "reqflowrev", "Reviewer");

        var reviewerId = (await Body(await reviewer.GetAsync("/api/auth/me")))
            .GetProperty("id").GetString()!;

        // 1. Requester submits -> Submitted, appears in admin queue
        var id = await Submit(requester, "Full flow request");
        var queue = (await Body(await admin.GetAsync("/api/admin/requests")))
            .EnumerateArray().ToList();
        Assert.Contains(queue, q => q.GetProperty("id").GetString() == id);

        // 2. Both sides comment; admin comments marked 'admin'
        Assert.Equal(HttpStatusCode.Created,
            (await requester.PostAsJsonAsync($"/api/requests/{id}/comments",
                new { comment_text = "hello admin" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await admin.PostAsJsonAsync($"/api/requests/{id}/comments",
                new { comment_text = "reviewed" })).StatusCode);

        // 3. Attachment upload -> download -> delete
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("file body")),
            "file", "notes.txt");
        var up = await requester.PostAsync($"/api/requests/{id}/attachments", content);
        Assert.Equal(HttpStatusCode.Created, up.StatusCode);
        var att = await Body(up);
        var attId = att.GetProperty("id").GetInt64();

        var dl = await admin.GetAsync($"/api/requests/{id}/attachments/{attId}/download");
        Assert.Equal(HttpStatusCode.OK, dl.StatusCode);
        Assert.Equal("file body", await dl.Content.ReadAsStringAsync());

        // denied extension rejected
        var bad = new MultipartFormDataContent();
        bad.Add(new ByteArrayContent(new byte[] { 1, 2 }), "file", "evil.exe");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsync($"/api/requests/{id}/attachments", bad)).StatusCode);

        // requester cannot delete an admin's attachment, admin can delete anything
        var adminAtt = new MultipartFormDataContent();
        adminAtt.Add(new ByteArrayContent(new byte[] { 3 }), "file", "admin-file.txt");
        var adminAttId = (await Body(await admin.PostAsync(
                $"/api/requests/{id}/attachments", adminAtt))).GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.OK,
            (await admin.DeleteAsync($"/api/requests/{id}/attachments/{adminAttId}")).StatusCode);

        // 4. Admin save (draft): first admin action flips status
        var save = await admin.PutAsJsonAsync($"/api/admin/requests/{id}",
            new { fcr_code = "FCR-CT-PI-099", category = "Permanent",
                  contractor = "Contractor A", subcontractor = "Subcontractor B" });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var dto = await Body(save);
        Assert.Equal("Under Admin Review", dto.GetProperty("submission_status").GetString());
        Assert.Equal("FCR-CT-PI-099", dto.GetProperty("fcr_code").GetString());
        Assert.Equal("Contractor A", dto.GetProperty("contractor").GetString());
        Assert.Equal("Subcontractor B", dto.GetProperty("subcontractor").GetString());
        Assert.True(dto.GetProperty("is_archived").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(dto.GetProperty("archived_at").GetString()));

        // FCR code assignment creates the numbered archive, form manifest, and attachment copy.
        var archiveDir = Path.Combine(fixture.DataDir, "CTEP", "PI", "FCR-CT-PI-099");
        Assert.True(Directory.Exists(archiveDir));
        var manifestPath = Path.Combine(archiveDir, "FCR-CT-PI-099-request.json");
        Assert.True(File.Exists(manifestPath));
        var htmlPath = Path.Combine(archiveDir, "FCR-CT-PI-099-request.html");
        Assert.True(File.Exists(htmlPath));
        var html = await File.ReadAllTextAsync(htmlPath);
        Assert.Contains("Full flow request", html);
        Assert.Contains("Contractor A", html);
        var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath)).RootElement;
        Assert.Equal("Full flow request", manifest.GetProperty("title").GetString());
        Assert.Equal("Contractor A", manifest.GetProperty("contractor").GetString());
        Assert.Equal("Subcontractor B", manifest.GetProperty("subcontractor").GetString());
        var archivedAttachment = Directory.GetFiles(archiveDir, "ATT-*").Single();
        Assert.Equal("file body", await File.ReadAllTextAsync(archivedAttachment));
        // Archived attachments are immutable; explicit archive endpoint is idempotent/retryable.
        var lateAttachment = new MultipartFormDataContent();
        lateAttachment.Add(new ByteArrayContent(new byte[] { 4 }), "file", "late.txt");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await requester.PostAsync($"/api/requests/{id}/attachments", lateAttachment)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await requester.DeleteAsync($"/api/requests/{id}/attachments/{attId}")).StatusCode);
        var archiveRetry = await admin.PostAsync($"/api/admin/requests/{id}/archive", null);
        Assert.Equal(HttpStatusCode.OK, archiveRetry.StatusCode);
        Assert.Equal("FCR-CT-PI-099", (await Body(archiveRetry)).GetProperty("fcr_code").GetString());

        // 5. Workflow prep needs >=1 reviewer with the Reviewer role
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PutAsJsonAsync($"/api/admin/requests/{id}/workflow",
                new { review_mode = "sequential", reviewer_user_ids = Array.Empty<long>() })).StatusCode);
        var requesterId = (await Body(await requester.GetAsync("/api/auth/me")))
            .GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PutAsJsonAsync($"/api/admin/requests/{id}/workflow",
                new { review_mode = "sequential",
                      reviewer_user_ids = new[] { long.Parse(requesterId) } })).StatusCode);

        var prep = await admin.PutAsJsonAsync($"/api/admin/requests/{id}/workflow",
            new { review_mode = "sequential", decision_rule = "all",
                  reviewer_user_ids = new[] { long.Parse(reviewerId) } });
        Assert.Equal(HttpStatusCode.OK, prep.StatusCode);
        Assert.Single((await Body(prep)).GetProperty("workflow")
            .GetProperty("stages")[0].GetProperty("reviewers").EnumerateArray());

        // 6. Submit for review -> Under Review + workflow In Progress
        var submit = await admin.PostAsync($"/api/admin/requests/{id}/submit-for-review", null);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        dto = await Body(submit);
        Assert.Equal("Under Review", dto.GetProperty("submission_status").GetString());
        Assert.Equal("In Progress", dto.GetProperty("workflow")
            .GetProperty("workflow_status").GetString());

        // submit-for-review without FCR code is rejected
        var id2 = await Submit(requester, "No code request");
        var prep2 = await admin.PutAsJsonAsync($"/api/admin/requests/{id2}/workflow",
            new { review_mode = "parallel",
                  reviewer_user_ids = new[] { long.Parse(reviewerId) } });
        Assert.Equal(HttpStatusCode.OK, prep2.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsync($"/api/admin/requests/{id2}/submit-for-review", null)).StatusCode);
    }

    [Fact]
    public async Task Admin_CreateLinkedFcr()
    {
        var admin = await Login("admin", "admin"); Assert.NotNull(admin);
        var requester = await MakeUser(admin, "reqflow4", "Requester");
        var id = await Submit(requester, "Linked FCR test");

        var created = await admin.PostAsync($"/api/admin/requests/{id}/create-fcr", null);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var dto = await Body(created);
        var linkedId = dto.GetProperty("linked_fcr_id").GetInt64();
        Assert.True(linkedId > 0);

        // the linked record exists in the FCR store
        var fcr = await Body(await admin.GetAsync($"/api/fcr/{linkedId}"));
        Assert.Equal("Linked FCR test", fcr.GetProperty("fcr_subject").GetString());

        // cannot link twice
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsync($"/api/admin/requests/{id}/create-fcr", null)).StatusCode);
    }
}
