using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using HeroPassport.Infrastructure.ProjectIdentity;
using Microsoft.Data.Sqlite;
using Xunit;

namespace HeroPassport.Web.Tests;

public sealed class HistoryProcessTests
{
    private const string TestBootstrap = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8";
    private const string TestSession = "ICEiIyQlJicoKSorLC0uLzAxMjM0NTY3ODk6Ozw9Pj8";

    [Fact]
    public async Task HistoryRequiresSessionAndUnseenProjectReadDoesNotPersistProject()
    {
        var token = TestContext.Current.CancellationToken;
        var sandbox = CreateSandbox();
        try
        {
            var databasePath = Path.Combine(sandbox.Home, "hero-passport.db");
            await HeroPassportDatabase.InitializeAsync(databasePath, token);
            var app = CreateApplication(databasePath);
            _ = await app.BootstrapAsync(
                new BootstrapRequest(
                    MutationRequestId.New(),
                    "en-US",
                    "History Nova",
                    "rpg_engineering",
                    true,
                    true),
                token);

            Assert.Equal(0, await RowCountAsync(databasePath, "projects", token));

            await using var web = await StartWebAsync(sandbox.Home, sandbox.ProjectRoot, token);
            using (var anonymous = CreateClient(web.Address))
            using (var response = await anonymous.GetAsync("/history", token))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }

            using var client = CreateClient(web.Address);
            await BootstrapAsync(client, web.Address, token);

            await using var witness = await HeroPassportDatabase.OpenConnectionAsync(databasePath, token);
            var beforeDataVersion = await DataVersionAsync(witness, token);
            var beforeCounts = await ProductTableCountsAsync(witness, token);

            using var history = await client.GetAsync("/history", token);
            var html = await history.Content.ReadAsStringAsync(token);

            Assert.Equal(HttpStatusCode.OK, history.StatusCode);
            Assert.Contains("No Quest history for this project yet.", html, StringComparison.Ordinal);
            Assert.Contains("Latest 25 Quests", html, StringComparison.Ordinal);
            Assert.DoesNotContain(sandbox.ProjectRoot, html, StringComparison.Ordinal);
            Assert.Equal(0, await RowCountAsync(databasePath, "projects", token));
            Assert.Equal(beforeDataVersion, await DataVersionAsync(witness, token));
            Assert.Equal(beforeCounts, await ProductTableCountsAsync(witness, token));
        }
        finally
        {
            DeleteSandbox(sandbox.Root);
        }
    }

    [Fact]
    public async Task HistoryIsProjectScopedBoundedPrivateAndReadOnly()
    {
        var token = TestContext.Current.CancellationToken;
        var sandbox = CreateSandbox();
        try
        {
            var databasePath = Path.Combine(sandbox.Home, "hero-passport.db");
            var fixture = await SeedHistoryAsync(databasePath, sandbox.ProjectRoot, sandbox.ForeignProjectRoot, token);

            await using var web = await StartWebAsync(sandbox.Home, sandbox.ProjectRoot, token);
            using var client = CreateClient(web.Address);
            await BootstrapAsync(client, web.Address, token);

            await using var witness = await HeroPassportDatabase.OpenConnectionAsync(databasePath, token);
            var beforeDataVersion = await DataVersionAsync(witness, token);
            var beforeCounts = await ProductTableCountsAsync(witness, token);

            using var listResponse = await client.GetAsync("/history", token);
            var listHtml = await listResponse.Content.ReadAsStringAsync(token);
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
            Assert.Contains("Latest 25 Quests", listHtml, StringComparison.Ordinal);
            Assert.Contains("Open Quest 25", listHtml, StringComparison.Ordinal);
            Assert.Contains("Quest 24", listHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("Quest 00", listHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("Foreign Quest", listHtml, StringComparison.Ordinal);

            var historyLinks = Regex.Matches(
                listHtml,
                "href=\"/history/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\"",
                RegexOptions.CultureInvariant);
            Assert.Equal(25, historyLinks.Count);
            Assert.Contains(fixture.OpenQuestId.ToString(), historyLinks[0].Value, StringComparison.Ordinal);

            using var detailResponse = await client.GetAsync($"/history/{fixture.DetailQuestId}", token);
            var detailHtml = await detailResponse.Content.ReadAsStringAsync(token);
            Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
            Assert.Contains("Qualified process history detail.", detailHtml, StringComparison.Ordinal);
            Assert.Contains("Scope violations: 1", detailHtml, StringComparison.Ordinal);
            Assert.Contains("User corrections: 2", detailHtml, StringComparison.Ordinal);
            Assert.Contains("testing_awareness", detailHtml, StringComparison.Ordinal);
            Assert.Contains("review", detailHtml, StringComparison.Ordinal);
            Assert.Contains("scope_control", detailHtml, StringComparison.Ordinal);

            using var malformedResponse = await client.GetAsync("/history/not-a-quest", token);
            var malformedHtml = await malformedResponse.Content.ReadAsStringAsync(token);
            Assert.Equal(HttpStatusCode.BadRequest, malformedResponse.StatusCode);
            Assert.Contains("The Quest identifier is invalid.", malformedHtml, StringComparison.Ordinal);

            var missingQuestId = QuestId.New();
            using var missingResponse = await client.GetAsync($"/history/{missingQuestId}", token);
            var missingHtml = await missingResponse.Content.ReadAsStringAsync(token);
            Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
            Assert.Contains("This Quest is not available in the current project.", missingHtml, StringComparison.Ordinal);

            using var foreignResponse = await client.GetAsync($"/history/{fixture.ForeignQuestId}", token);
            var foreignHtml = await foreignResponse.Content.ReadAsStringAsync(token);
            Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);
            Assert.Equal(missingHtml, foreignHtml);

            foreach (var html in new[] { listHtml, detailHtml, malformedHtml, missingHtml, foreignHtml })
            {
                Assert.DoesNotContain(fixture.CurrentFingerprint, html, StringComparison.Ordinal);
                Assert.DoesNotContain(fixture.ForeignFingerprint, html, StringComparison.Ordinal);
                Assert.DoesNotContain(sandbox.ProjectRoot, html, StringComparison.Ordinal);
                Assert.DoesNotContain(sandbox.ForeignProjectRoot, html, StringComparison.Ordinal);
                Assert.DoesNotContain(databasePath, html, StringComparison.Ordinal);
                Assert.DoesNotContain("request_id", html, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("args_hash", html, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("project_id", html, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("workspace_fingerprint", html, StringComparison.OrdinalIgnoreCase);
            }

            Assert.Equal(beforeDataVersion, await DataVersionAsync(witness, token));
            Assert.Equal(beforeCounts, await ProductTableCountsAsync(witness, token));
        }
        finally
        {
            DeleteSandbox(sandbox.Root);
        }
    }

    private static async Task<SeedFixture> SeedHistoryAsync(
        string databasePath,
        string projectRoot,
        string foreignProjectRoot,
        CancellationToken token)
    {
        await HeroPassportDatabase.InitializeAsync(databasePath, token);
        var installationSalt = await HeroPassportDatabase.ReadProjectIdentitySaltAsync(databasePath, token);
        var currentIdentity = await ProjectIdentityResolver.ResolveAsync(
            projectRoot,
            FindRepositoryRoot(),
            installationSalt,
            token);
        var foreignIdentity = await ProjectIdentityResolver.ResolveAsync(
            foreignProjectRoot,
            FindRepositoryRoot(),
            installationSalt,
            token);
        var currentProject = new ProjectBindingContext(
            currentIdentity.DisplayName,
            currentIdentity.WorkspaceFingerprint,
            currentIdentity.IdentityVersion);
        var foreignProject = new ProjectBindingContext(
            foreignIdentity.DisplayName,
            foreignIdentity.WorkspaceFingerprint,
            foreignIdentity.IdentityVersion);

        var app = CreateApplication(databasePath);
        var hero = (await app.BootstrapAsync(
            new BootstrapRequest(
                MutationRequestId.New(),
                "en-US",
                "History Nova",
                "rpg_engineering",
                true,
                true),
            token)).Hero;

        QuestId detailQuestId = default;
        for (var index = 0; index < 25; index++)
        {
            var quest = (await app.StartQuestAsync(
                new StartQuestRequest(
                    MutationRequestId.New(),
                    hero.HeroId,
                    index == 24 ? "review" : "coding",
                    $"Quest {index:D2}",
                    $"Canonical process history fixture {index:D2}."),
                currentProject,
                token)).Quest;

            if (index == 24)
            {
                detailQuestId = quest.QuestId;
                await app.FinishQuestAsync(
                    new FinishQuestRequest(
                        MutationRequestId.New(),
                        quest.QuestId,
                        "partial",
                        "Qualified process history detail.",
                        new FinishQuestMetrics(
                            TestsMentioned: true,
                            ScopeViolations: 1,
                            UserCorrections: 2,
                            BuildStatus: "passed",
                            BuildEvidence: "observed",
                            TestsStatus: "failed",
                            TestsEvidence: "observed"),
                        ["testing_awareness", "review", "scope_control"]),
                    currentProject,
                    token);
            }
            else
            {
                await app.FinishQuestAsync(
                    CleanFinish(MutationRequestId.New(), quest.QuestId, ["coding"]),
                    currentProject,
                    token);
            }
        }

        var foreignQuest = (await app.StartQuestAsync(
            new StartQuestRequest(
                MutationRequestId.New(),
                hero.HeroId,
                "coding",
                "Foreign Quest",
                "This Quest belongs to another Project and must remain invisible."),
            foreignProject,
            token)).Quest;
        await app.FinishQuestAsync(
            CleanFinish(MutationRequestId.New(), foreignQuest.QuestId, ["coding"]),
            foreignProject,
            token);

        var openQuest = (await app.StartQuestAsync(
            new StartQuestRequest(
                MutationRequestId.New(),
                hero.HeroId,
                "review",
                "Open Quest 25",
                "Remain open so process history proves nullable finish facts."),
            currentProject,
            token)).Quest;

        Assert.NotEqual(default, detailQuestId);
        return new SeedFixture(
            detailQuestId,
            openQuest.QuestId,
            foreignQuest.QuestId,
            currentIdentity.WorkspaceFingerprint,
            foreignIdentity.WorkspaceFingerprint);
    }

    private static HeroPassportApplication CreateApplication(string databasePath) =>
        new(new SqliteHeroPassportStateStore(databasePath), new IncrementingTimeProvider());

    private static FinishQuestRequest CleanFinish(
        MutationRequestId requestId,
        QuestId questId,
        IReadOnlyList<string> skills) =>
        new(
            requestId,
            questId,
            "success",
            "Completed the process history fixture with observed passing build and tests.",
            new FinishQuestMetrics(
                TestsMentioned: true,
                ScopeViolations: 0,
                UserCorrections: 0,
                BuildStatus: "passed",
                BuildEvidence: "observed",
                TestsStatus: "passed",
                TestsEvidence: "observed"),
            skills);

    private static async Task<long> RowCountAsync(string databasePath, string table, CancellationToken token)
    {
        Assert.Contains(table, new[] { "projects" });
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(databasePath, token);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        return Convert.ToInt64(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
    }

    private static async Task<long> DataVersionAsync(SqliteConnection connection, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA data_version;";
        return Convert.ToInt64(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
    }

    private static async Task<string> ProductTableCountsAsync(SqliteConnection connection, CancellationToken token)
    {
        await using var tablesCommand = connection.CreateCommand();
        tablesCommand.CommandText = """
            SELECT name
            FROM sqlite_master
            WHERE type='table'
              AND name NOT LIKE 'sqlite_%'
              AND name NOT LIKE '__EF%'
            ORDER BY name;
            """;
        var tables = new List<string>();
        await using (var reader = await tablesCommand.ExecuteReaderAsync(token))
        {
            while (await reader.ReadAsync(token))
            {
                tables.Add(reader.GetString(0));
            }
        }

        var snapshots = new List<string>(tables.Count);
        foreach (var table in tables)
        {
            await using var countCommand = connection.CreateCommand();
            countCommand.CommandText = $"SELECT COUNT(*) FROM \"{table.Replace("\"", "\"\"", StringComparison.Ordinal)}\";";
            var count = Convert.ToInt64(await countCommand.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
            snapshots.Add($"{table}:{count.ToString(CultureInfo.InvariantCulture)}");
        }

        return string.Join("|", snapshots);
    }

    private static HttpClient CreateClient(Uri baseAddress)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            CookieContainer = new CookieContainer(),
        };
        return new HttpClient(handler, disposeHandler: true) { BaseAddress = baseAddress };
    }

    private static async Task BootstrapAsync(HttpClient client, Uri address, CancellationToken token)
    {
        using var shell = await client.GetAsync("/__hero/bootstrap", token);
        var html = await shell.Content.ReadAsStringAsync(token);
        Assert.Equal(HttpStatusCode.OK, shell.StatusCode);
        using var content = new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", HiddenValue(html, "__RequestVerificationToken")),
            new("capability", TestBootstrap),
        ]);
        using var request = SameOriginPost(address, "/__hero/bootstrap/claim", content);
        using var response = await client.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
    }

    private static string HiddenValue(string html, string name)
    {
        var tag = Regex.Match(
            html,
            $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*>",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        Assert.True(tag.Success, $"Hidden form field '{name}' was not rendered. Body: {html}");
        var value = Regex.Match(tag.Value, "value=\"([^\"]*)\"", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        Assert.True(value.Success, $"Hidden form field '{name}' has no value. Tag: {tag.Value}");
        return WebUtility.HtmlDecode(value.Groups[1].Value);
    }

    private static HttpRequestMessage SameOriginPost(Uri address, string path, HttpContent content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
        request.Headers.TryAddWithoutValidation("Origin", $"http://127.0.0.1:{address.Port}");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
        return request;
    }

    private static async Task<WebProcessHandle> StartWebAsync(
        string home,
        string projectRoot,
        CancellationToken token)
    {
        var root = FindRepositoryRoot();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var webDll = Path.Combine(root, "src", "HeroPassport.Web", "bin", configuration, "net10.0", "HeroPassport.Web.dll");
        Assert.True(File.Exists(webDll), $"HeroPassport.Web was not built at {webDll}.");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(webDll);
        startInfo.ArgumentList.Add("--project-root");
        startInfo.ArgumentList.Add(projectRoot);
        startInfo.ArgumentList.Add("--no-open-browser");
        startInfo.Environment["HERO_PASSPORT_HOME"] = home;
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
        startInfo.Environment["HERO_PASSPORT_WEB_TEST_BOOTSTRAP"] = TestBootstrap;
        startInfo.Environment["HERO_PASSPORT_WEB_TEST_SESSION"] = TestSession;

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Hero Passport Web process did not start.");
        var output = new ConcurrentQueue<string>();
        var addressSource = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stdout = PumpAsync(process.StandardOutput, output, addressSource);
        var stderr = PumpAsync(process.StandardError, output, addressSource);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var address = await addressSource.Task.WaitAsync(timeout.Token);
            return new WebProcessHandle(process, address, output, stdout, stderr);
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(stdout, stderr);
            process.Dispose();
            throw;
        }
    }

    private static async Task PumpAsync(
        StreamReader reader,
        ConcurrentQueue<string> output,
        TaskCompletionSource<Uri> addressSource)
    {
        const string marker = "Now listening on:";
        while (await reader.ReadLineAsync() is { } line)
        {
            output.Enqueue(line);
            var index = line.IndexOf(marker, StringComparison.Ordinal);
            if (index >= 0 && Uri.TryCreate(line[(index + marker.Length)..].Trim(), UriKind.Absolute, out var uri))
            {
                addressSource.TrySetResult(uri);
            }
        }
    }

    private static Sandbox CreateSandbox()
    {
        var root = Path.Combine(Path.GetTempPath(), "HeroPassport.Web.History.Tests", Guid.NewGuid().ToString("N"));
        var home = Path.Combine(root, "home");
        var project = Path.Combine(root, "sample-project");
        var foreignProject = Path.Combine(root, "foreign-project");
        Directory.CreateDirectory(home);
        Directory.CreateDirectory(project);
        Directory.CreateDirectory(foreignProject);
        return new Sandbox(root, home, project, foreignProject);
    }

    private static void DeleteSandbox(string root)
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "HeroPassport.slnx")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not find HeroPassport.slnx from the test base directory.");
    }

    private sealed record Sandbox(string Root, string Home, string ProjectRoot, string ForeignProjectRoot);

    private sealed record SeedFixture(
        QuestId DetailQuestId,
        QuestId OpenQuestId,
        QuestId ForeignQuestId,
        string CurrentFingerprint,
        string ForeignFingerprint);

    private sealed class IncrementingTimeProvider : TimeProvider
    {
        private long _ticks;

        public override DateTimeOffset GetUtcNow() =>
            new DateTimeOffset(2026, 9, 17, 0, 0, 0, TimeSpan.Zero)
                .AddSeconds(Interlocked.Increment(ref _ticks));
    }

    private sealed class WebProcessHandle : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly ConcurrentQueue<string> _output;
        private readonly Task _stdout;
        private readonly Task _stderr;

        public WebProcessHandle(Process process, Uri address, ConcurrentQueue<string> output, Task stdout, Task stderr)
        {
            _process = process;
            Address = address;
            _output = output;
            _stdout = stdout;
            _stderr = stderr;
        }

        public Uri Address { get; }
        public string JoinedOutput => string.Join(Environment.NewLine, _output);

        public async ValueTask DisposeAsync()
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
            await _process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(_stdout, _stderr);
            _process.Dispose();
        }
    }
}
