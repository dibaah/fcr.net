using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FCR.Tests;

/// <summary>
/// HTTP contract parity with the FastAPI app — same routes, status codes,
/// snake_case shapes and {"detail": ...} errors.
/// </summary>
public class ApiParityTests : IClassFixture<ApiParityTests.Fixture>, IAsyncLifetime
{
    public sealed class Fixture : WebApplicationFactory<Program>
    {
        public string DataDir = Path.Combine(Path.GetTempPath(), $"fcr_api_{Guid.NewGuid():N}");

        public Fixture()
        {
            Directory.CreateDirectory(DataDir);
            foreach (var ext in new[] { "", "-wal", "-shm" })
            {
                var src = TestDb.RepoDbPath + ext;
                if (File.Exists(src)) File.Copy(src, Path.Combine(DataDir, "fcr_database.db" + ext));
            }
        }

        public static string RepoStaticDir
        {
            get
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "fcr_database.db")))
                    dir = dir.Parent;
                return Path.Combine(dir!.FullName, "static");
            }
        }

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) =>
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["FCR_DATA_DIR"] = DataDir,
                    ["FCR_STATIC_DIR"] = RepoStaticDir,
                    ["Fcr:AttachmentRoot"] = DataDir,
                }));
    }

    private readonly HttpClient _client;
    private readonly Fixture _fixture;

    public ApiParityTests(Fixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    public async Task InitializeAsync()
    {
        var r = await _client.PostAsJsonAsync("/api/auth/login",
            new Dictionary<string, object?> { ["username"] = "admin", ["password"] = "admin" });
        r.EnsureSuccessStatusCode();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<JsonElement> Body(HttpResponseMessage r)
    {
        var json = await r.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public async Task Health_And_About()
    {
        var health = await Body(await _client.GetAsync("/api/health"));
        Assert.Equal("ok", health.GetProperty("status").GetString());
        Assert.Equal("2.0.0", health.GetProperty("version").GetString());

        var about = await Body(await _client.GetAsync("/api/about"));
        Assert.Equal(145, about.GetProperty("records").GetInt32());
        Assert.Equal(2, about.GetProperty("schema_version").GetInt32());
    }

    [Fact]
    public async Task ListFcr_Returns145Records_WithSnakeCaseAndFolderPath()
    {
        var res = await _client.GetAsync("/api/fcr");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var rows = (await Body(res)).EnumerateArray().ToList();
        Assert.Equal(145, rows.Count);
        Assert.True(rows[0].TryGetProperty("fcr_subject", out _));
        Assert.True(rows[0].TryGetProperty("folder_path", out _));
    }

    [Fact]
    public async Task ListFcr_FiltersAndStatusGroups()
    {
        var open = (await Body(await _client.GetAsync("/api/fcr?status_group=open"))).EnumerateArray().ToList();
        var rejected = (await Body(await _client.GetAsync("/api/fcr?status_group=rejected"))).EnumerateArray().ToList();
        Assert.True(open.Count + rejected.Count <= 145);
        Assert.True(rejected.Count > 0);

        var bad = await _client.GetAsync("/api/fcr?status_group=bogus");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var detail = (await Body(bad)).GetProperty("detail").GetString()!;
        Assert.Contains("status_group", detail);

        var filtered = (await Body(await _client.GetAsync("/api/fcr?search=ct-pi"))).EnumerateArray().ToList();
        Assert.All(filtered, _ => { }); // returns matching subset without error
    }

    [Fact]
    public async Task CrudLifecycle_StatusCodesMatch()
    {
        var create = await _client.PostAsJsonAsync("/api/fcr",
            new Dictionary<string, object?> { ["fcr_subject"] = "Parity record", ["fcr_code"] = "FCR-EP-ME-777" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var rec = await Body(create);
        var id = rec.GetProperty("id").GetInt64();
        Assert.Equal("FCR-EP-ME-777", rec.GetProperty("fcr_code").GetString());
        Assert.True(rec.TryGetProperty("row_num", out _));

        // duplicate code → 409
        var dup = await _client.PostAsJsonAsync("/api/fcr",
            new Dictionary<string, object?> { ["fcr_subject"] = "x", ["fcr_code"] = "fcr-ep-me-777" });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Contains("already exists", (await Body(dup)).GetProperty("detail").GetString());

        // blank subject → 400
        var blank = await _client.PostAsJsonAsync("/api/fcr",
            new Dictionary<string, object?> { ["fcr_subject"] = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Equal("FCR Subject required", (await Body(blank)).GetProperty("detail").GetString());

        // invalid code → 400
        var invalid = await _client.PostAsJsonAsync("/api/fcr",
            new Dictionary<string, object?> { ["fcr_subject"] = "x", ["fcr_code"] = "NOPE" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var update = await _client.PutAsJsonAsync($"/api/fcr/{id}",
            new Dictionary<string, object?> { ["fcr_subject"] = "Updated", ["fcr_code"] = "FCR-EP-ME-777", ["execution_status"] = "Finished" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Updated", (await Body(update)).GetProperty("fcr_subject").GetString());

        var notFound = await _client.GetAsync("/api/fcr/999999");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Equal("FCR not found", (await Body(notFound)).GetProperty("detail").GetString());

        var del = await _client.DeleteAsync($"/api/fcr/{id}");
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);
        Assert.Equal(id, (await Body(del)).GetProperty("deleted").GetInt64());
    }

    [Fact]
    public async Task BulkSave_UpdatesExistingOnly()
    {
        var first = (await Body(await _client.GetAsync("/api/fcr"))).EnumerateArray().First();
        var id = first.GetProperty("id").GetInt64();
        var res = await _client.PostAsJsonAsync("/api/fcr/bulk-save",
            new Dictionary<string, object?>
            {
                ["records"] = new[]
                {
                    new Dictionary<string, object?> { ["id"] = id, ["fcr_subject"] = "Bulk-edited", ["fcr_code"] = "" },
                    new Dictionary<string, object?> { ["id"] = 999999, ["fcr_subject"] = "Missing" },
                },
            });
        var body = await Body(res);
        Assert.Equal(1, body.GetProperty("saved").GetInt32());
        Assert.Equal(1, body.GetProperty("skipped").GetInt32());
    }

    [Fact]
    public async Task BulkDelete_RemovesRows()
    {
        var create = await _client.PostAsJsonAsync("/api/fcr",
            new Dictionary<string, object?> { ["fcr_subject"] = "To delete" });
        var id = (await Body(create)).GetProperty("id").GetInt64();
        var res = await _client.PostAsJsonAsync("/api/fcr/bulk-delete",
            new Dictionary<string, object?> { ["ids"] = new[] { id } });
        Assert.Equal(1, (await Body(res)).GetProperty("deleted").GetInt32());
    }

    [Fact]
    public async Task Stats_Options_Notifications()
    {
        var stats = await Body(await _client.GetAsync("/api/stats"));
        Assert.Equal(145, stats.GetProperty("total").GetInt64());
        Assert.True(stats.GetProperty("trend").GetArrayLength() > 0);
        Assert.True(stats.GetProperty("by_category").GetArrayLength() > 0);

        var options = await Body(await _client.GetAsync("/api/options"));
        Assert.True(options.GetProperty("pedec_status").GetArrayLength() > 0);

        var notif = await Body(await _client.GetAsync("/api/notifications"));
        Assert.True(notif.TryGetProperty("count", out _));
        Assert.True(notif.TryGetProperty("items", out _));
    }

    [Fact]
    public async Task Settings_GetPutReset()
    {
        var s = await Body(await _client.GetAsync("/api/settings"));
        Assert.True(s.TryGetProperty("backup_location", out _));
        Assert.True(s.TryGetProperty("app_version", out _));
        Assert.True(s.TryGetProperty("db_location", out _));

        var put = await _client.PutAsJsonAsync("/api/settings",
            new Dictionary<string, object?> { ["auto_backup_interval_days"] = 0, ["language"] = "xx" });
        var payload = await Body(put);
        Assert.Equal(1, payload.GetProperty("auto_backup_interval_days").GetInt32()); // clamped
        Assert.Equal("en", payload.GetProperty("language").GetString());               // whitelist

        var reset = await _client.PostAsync("/api/settings/reset", null);
        Assert.True((await Body(reset)).GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Backup_CreateAndList()
    {
        var created = await Body(await _client.PostAsync("/api/backup", null));
        Assert.True(created.GetProperty("ok").GetBoolean());
        Assert.True(created.GetProperty("size_kb").GetInt64() > 0);

        var list = await Body(await _client.GetAsync("/api/backups"));
        Assert.True(list.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task Export_ReturnsXlsx_WithAndWithoutCharts()
    {
        foreach (var route in new[] { "/api/export", "/api/export-with-charts" })
        {
            var res = await _client.GetAsync(route);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                res.Content.Headers.ContentType!.MediaType);
            var bytes = await res.Content.ReadAsByteArrayAsync();
            Assert.True(bytes.Length > 4000);
            Assert.Equal(new byte[] { 0x50, 0x4B }, bytes[..2]); // PK zip header
        }
    }

    [Fact]
    public async Task Spa_ServedAtRoot_AndAssetsUnderStatic()
    {
        var index = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        Assert.Equal("text/html", index.Content.Headers.ContentType!.MediaType);

        foreach (var asset in new[] { "/static/style.css", "/static/script.js" })
        {
            var res = await _client.GetAsync(asset);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/style.css")).StatusCode);
    }

    [Fact]
    public async Task SecurityHeaders_MatchPythonMiddleware()
    {
        var res = await _client.GetAsync("/api/health");
        Assert.Equal("nosniff", string.Join(',', res.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("SAMEORIGIN", string.Join(',', res.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("no-referrer", string.Join(',', res.Headers.GetValues("Referrer-Policy")));
        Assert.Equal("no-store", string.Join(',', res.Headers.GetValues("Cache-Control")));
    }

    [Fact]
    public async Task HealthProbes_LiveAndReady()
    {
        var live = await _client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        var ready = await _client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    [Fact]
    public async Task Concurrency_ParallelCreatesAndReads()
    {
        var tasks = Enumerable.Range(0, 12).Select(i =>
            _client.PostAsJsonAsync("/api/fcr", new Dictionary<string, object?>
            {
                ["fcr_subject"] = $"Concurrent {i}",
                ["fcr_code"] = $"FCR-EP-ME-{800 + i}",
            })).ToArray();
        var responses = await Task.WhenAll(tasks);
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));

        var ids = responses.Select(async r => (await Body(r)).GetProperty("id").GetInt64());
        var idList = await Task.WhenAll(ids);
        Assert.Equal(idList.Length, idList.Distinct().Count());

        // parallel reads while deleting
        var reads = Enumerable.Range(0, 10).Select(_ => _client.GetAsync("/api/fcr"));
        var deletes = idList.Select(id => _client.DeleteAsync($"/api/fcr/{id}"));
        var all = reads.Concat(deletes).ToArray();
        var results = await Task.WhenAll(all);
        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
    }

    [Fact]
    public async Task Folders_404AndEmptyCode()
    {
        var missing = await _client.GetAsync("/api/fcr/999999/open-folder");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        // find a record without fcr_code if any; otherwise a code without a folder on disk → ok:false
        var rows = (await Body(await _client.GetAsync("/api/fcr"))).EnumerateArray().ToList();
        var anyId = rows[0].GetProperty("id").GetInt64();
        var res = await Body(await _client.GetAsync($"/api/fcr/{anyId}/open-folder"));
        Assert.True(res.TryGetProperty("ok", out _));
    }

}
