using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace HeroPassport.Web.Tests;

public sealed class FinishQuestProcessTests
{
    private const string TestBootstrap = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8";
    private const string TestSession = "ICEiIyQlJicoKSorLC0uLzAxMjM0NTY3ODk6Ozw9Pj8";

    [Fact]
    public async Task ConfirmedFinishMutatesOnlyOnConfirmAndDuplicateConfirmIsIdempotent()
    {
        var token = TestContext.Current.CancellationToken;
        var sandbox = CreateSandbox();
        try
        {
            var init = await RunCliAsync(
                sandbox.Home,
                token,
                "init", "--locale", "en-US", "--hero-name", "Finish Nova", "--json");
            Assert.Equal(0, init.ExitCode);

            await using var web = await StartWebAsync(sandbox.Home, sandbox.ProjectRoot, token);
            using var client = CreateClient(web.Address);
            await BootstrapAsync(client, web.Address, token);
            await StartQuestAsync(client, web.Address, token);

            using var dashboard = await client.GetAsync("/", token);
            var dashboardHtml = await dashboard.Content.ReadAsStringAsync(token);
            Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
            var finishPath = FinishPath(dashboardHtml);

            using var finishGet = await client.GetAsync(finishPath, token);
            var finishHtml = await finishGet.Content.ReadAsStringAsync(token);
            Assert.Equal(HttpStatusCode.OK, finishGet.StatusCode);
            Assert.Contains("FinishQuestPrepare", finishHtml, StringComparison.Ordinal);
            Assert.Contains("Process Quest", finishHtml, StringComparison.Ordinal);
            Assert.Contains("Finish Nova", finishHtml, StringComparison.Ordinal);

            var prepareToken = HiddenValue(finishHtml, "__RequestVerificationToken");
            var prepareHandler = HiddenValue(finishHtml, "_handler");
            using var prepareContent = FinishForm(
                prepareToken,
                prepareHandler,
                "  Finished   through   confirmation  ");
            using var prepareRequest = SameOriginPost(web.Address, finishPath, prepareContent);
            using var prepareResponse = await client.SendAsync(prepareRequest, token);

            Assert.True(
                prepareResponse.StatusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther,
                $"Finish prepare returned {(int)prepareResponse.StatusCode}: {await prepareResponse.Content.ReadAsStringAsync(token)}");
            var confirmationPath = RedirectPath(prepareResponse, web.Address);
            Assert.Matches("^/quests/finish/confirm/[A-Za-z0-9_-]{22}$", confirmationPath);
            Assert.DoesNotContain("Finished", confirmationPath, StringComparison.Ordinal);

            var databasePath = Path.Combine(sandbox.Home, "hero-passport.db");
            Assert.Equal("open", await ScalarTextAsync(databasePath, "SELECT status FROM quest_sessions LIMIT 1;", token));
            Assert.Equal(0, await RowCountAsync(databasePath, "quest_reports", token));
            Assert.Equal(0, await RowCountAsync(databasePath, "xp_events", token));
            Assert.Equal(2, await RowCountAsync(databasePath, "mutation_receipts", token));

            using var confirmGet = await client.GetAsync(confirmationPath, token);
            var confirmHtml = await confirmGet.Content.ReadAsStringAsync(token);
            Assert.Equal(HttpStatusCode.OK, confirmGet.StatusCode);
            Assert.Contains("Finished through confirmation", confirmHtml, StringComparison.Ordinal);
            Assert.Contains("coding", confirmHtml, StringComparison.Ordinal);
            Assert.Contains("testing_awareness", confirmHtml, StringComparison.Ordinal);
            Assert.Contains("passed", confirmHtml, StringComparison.Ordinal);
            Assert.DoesNotContain(sandbox.ProjectRoot, confirmHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("finishrequestid", confirmHtml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("workspace_fingerprint", confirmHtml, StringComparison.OrdinalIgnoreCase);

            using var confirmResponse = await PostConfirmAsync(client, web.Address, confirmationPath, confirmHtml, token);
            Assert.True(
                confirmResponse.StatusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther,
                $"Finish confirm returned {(int)confirmResponse.StatusCode}: {await confirmResponse.Content.ReadAsStringAsync(token)}");
            Assert.Equal("/", RedirectPath(confirmResponse, web.Address));

            Assert.Equal("finished", await ScalarTextAsync(databasePath, "SELECT status FROM quest_sessions LIMIT 1;", token));
            Assert.Equal(1, await RowCountAsync(databasePath, "quest_reports", token));
            Assert.Equal(1, await RowCountAsync(databasePath, "xp_events", token));
            Assert.Equal(3, await RowCountAsync(databasePath, "mutation_receipts", token));

            using var committedGet = await client.GetAsync(confirmationPath, token);
            var committedHtml = await committedGet.Content.ReadAsStringAsync(token);
            Assert.Equal(HttpStatusCode.OK, committedGet.StatusCode);
            Assert.Contains("Quest already finished", committedHtml, StringComparison.Ordinal);

            using var duplicate = await PostConfirmAsync(client, web.Address, confirmationPath, committedHtml, token);
            Assert.True(duplicate.StatusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther);
            Assert.Equal("/", RedirectPath(duplicate, web.Address));
            Assert.Equal(1, await RowCountAsync(databasePath, "quest_reports", token));
            Assert.Equal(1, await RowCountAsync(databasePath, "xp_events", token));
            Assert.Equal(3, await RowCountAsync(databasePath, "mutation_receipts", token));

            using var finalDashboard = await client.GetAsync("/", token);
            var finalHtml = await finalDashboard.Content.ReadAsStringAsync(token);
            Assert.Equal(HttpStatusCode.OK, finalDashboard.StatusCode);
            Assert.Contains("No open Quest", finalHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("/quests/finish/", finalHtml, StringComparison.Ordinal);
        }
        finally
        {
            DeleteSandbox(sandbox.Root);
        }
    }

    [Fact]
    public async Task FinishRoutesEnforceSelectorCsrfOriginAndBoundedMaximumUnicodePayload()
    {
        var token = TestContext.Current.CancellationToken;
        var sandbox = CreateSandbox();
        try
        {
            var init = await RunCliAsync(
                sandbox.Home,
                token,
                "init", "--locale", "en-US", "--hero-name", "Boundary Nova", "--json");
            Assert.Equal(0, init.ExitCode);

            await using var web = await StartWebAsync(sandbox.Home, sandbox.ProjectRoot, token);
            using var anonymous = CreateClient(web.Address);
            using var anonymousGet = await anonymous.GetAsync($"/quests/finish/{Guid.CreateVersion7():D}", token);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousGet.StatusCode);

            using var client = CreateClient(web.Address);
            await BootstrapAsync(client, web.Address, token);
            await StartQuestAsync(client, web.Address, token);

            using var malformed = await client.GetAsync("/quests/finish/not-a-quest", token);
            Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
            using var unknown = await client.GetAsync($"/quests/finish/{Guid.CreateVersion7():D}", token);
            Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

            using var dashboard = await client.GetAsync("/", token);
            var finishPath = FinishPath(await dashboard.Content.ReadAsStringAsync(token));
            using var finishGet = await client.GetAsync(finishPath, token);
            var finishHtml = await finishGet.Content.ReadAsStringAsync(token);
            var antiforgery = HiddenValue(finishHtml, "__RequestVerificationToken");
            var handler = HiddenValue(finishHtml, "_handler");

            using (var missingToken = FinishForm(null, handler, "Must fail"))
            using (var request = SameOriginPost(web.Address, finishPath, missingToken))
            using (var response = await client.SendAsync(request, token))
            {
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }

            using (var crossSiteContent = FinishForm(antiforgery, handler, "Must fail"))
            using (var request = new HttpRequestMessage(HttpMethod.Post, finishPath) { Content = crossSiteContent })
            {
                request.Headers.TryAddWithoutValidation("Origin", "https://attacker.example");
                request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "cross-site");
                using var response = await client.SendAsync(request, token);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }

            using (var multipart = new MultipartFormDataContent())
            using (var request = SameOriginPost(web.Address, finishPath, multipart))
            using (var response = await client.SendAsync(request, token))
            {
                Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
            }

            using (var oversized = new StringContent(new string('x', 32_769), Encoding.UTF8, "application/x-www-form-urlencoded"))
            using (var request = SameOriginPost(web.Address, finishPath, oversized))
            using (var response = await client.SendAsync(request, token))
            {
                Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
            }

            var databasePath = Path.Combine(sandbox.Home, "hero-passport.db");
            Assert.Equal(0, await RowCountAsync(databasePath, "quest_reports", token));
            Assert.Equal("open", await ScalarTextAsync(databasePath, "SELECT status FROM quest_sessions LIMIT 1;", token));

            var maxSummary = string.Concat(Enumerable.Repeat("🚀", 2000));
            using var maximum = FinishForm(antiforgery, handler, maxSummary, threeSkills: true);
            using var maximumRequest = SameOriginPost(web.Address, finishPath, maximum);
            using var maximumResponse = await client.SendAsync(maximumRequest, token);
            Assert.True(
                maximumResponse.StatusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther,
                $"Maximum valid Finish payload returned {(int)maximumResponse.StatusCode}: {await maximumResponse.Content.ReadAsStringAsync(token)}");
            var confirmPath = RedirectPath(maximumResponse, web.Address);

            Assert.Equal(0, await RowCountAsync(databasePath, "quest_reports", token));
            using var confirmGet = await client.GetAsync(confirmPath, token);
            var confirmHtml = await confirmGet.Content.ReadAsStringAsync(token);
            Assert.Equal(HttpStatusCode.OK, confirmGet.StatusCode);
            Assert.Contains(maxSummary, WebUtility.HtmlDecode(confirmHtml), StringComparison.Ordinal);

            using var confirmResponse = await PostConfirmAsync(client, web.Address, confirmPath, confirmHtml, token);
            Assert.True(confirmResponse.StatusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther);
            Assert.Equal(1, await RowCountAsync(databasePath, "quest_reports", token));
            Assert.Equal("finished", await ScalarTextAsync(databasePath, "SELECT status FROM quest_sessions LIMIT 1;", token));
        }
        finally
        {
            DeleteSandbox(sandbox.Root);
        }
    }

    private static FormUrlEncodedContent FinishForm(
        string? antiforgery,
        string handler,
        string summary,
        bool threeSkills = false)
    {
        var values = new List<KeyValuePair<string, string>>
        {
            new("_handler", handler),
            new("Input.Result", "success"),
            new("Input.Summary", summary),
            new("Input.TestsMentioned", "true"),
            new("Input.ScopeViolations", "0"),
            new("Input.UserCorrections", "0"),
            new("Input.BuildStatus", "passed"),
            new("Input.BuildEvidence", "observed"),
            new("Input.TestsStatus", "passed"),
            new("Input.TestsEvidence", "observed"),
            new("Input.SkillsUsed", "coding"),
            new("Input.SkillsUsed", "testing_awareness"),
        };
        if (threeSkills)
        {
            values.Add(new("Input.SkillsUsed", "scope_control"));
        }
        if (antiforgery is not null)
        {
            values.Insert(0, new("__RequestVerificationToken", antiforgery));
        }
        return new FormUrlEncodedContent(values);
    }

    private static async Task StartQuestAsync(HttpClient client, Uri address, CancellationToken token)
    {
        using var startGet = await client.GetAsync("/quests/start", token);
        var html = await startGet.Content.ReadAsStringAsync(token);
        Assert.Equal(HttpStatusCode.OK, startGet.StatusCode);
        using var prepare = new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", HiddenValue(html, "__RequestVerificationToken")),
            new("_handler", HiddenValue(html, "_handler")),
            new("Input.QuestType", "coding"),
            new("Input.Title", "Process Quest"),
            new("Input.Goal", "Prove the Finish Web vertical"),
        ]);
        using var request = SameOriginPost(address, "/quests/start", prepare);
        using var response = await client.SendAsync(request, token);
        var confirmPath = RedirectPath(response, address);
        using var confirmGet = await client.GetAsync(confirmPath, token);
        var confirmHtml = await confirmGet.Content.ReadAsStringAsync(token);
        using var confirm = await PostConfirmAsync(client, address, confirmPath, confirmHtml, token);
        Assert.True(confirm.StatusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther);
    }

    private static string FinishPath(string dashboardHtml)
    {
        var match = Regex.Match(
            dashboardHtml,
            "href=\"(/quests/finish/[0-9a-f-]+)\"",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        Assert.True(match.Success, $"Dashboard did not render an explicit Finish link. Body: {dashboardHtml}");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static string RedirectPath(HttpResponseMessage response, Uri baseAddress)
    {
        Assert.True(
            response.StatusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther,
            $"Expected redirect but received {(int)response.StatusCode}.");
        var location = response.Headers.Location;
        Assert.NotNull(location);
        var resolved = location.IsAbsoluteUri ? location : new Uri(baseAddress, location);
        Assert.Equal(Uri.UriSchemeHttp, resolved.Scheme);
        Assert.Equal(IPAddress.Loopback.ToString(), resolved.Host);
        Assert.Equal(baseAddress.Port, resolved.Port);
        Assert.True(string.IsNullOrEmpty(resolved.Query));
        Assert.True(string.IsNullOrEmpty(resolved.Fragment));
        return resolved.AbsolutePath;
    }

    private static HttpRequestMessage SameOriginPost(Uri address, string path, HttpContent content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
        request.Headers.TryAddWithoutValidation("Origin", $"http://127.0.0.1:{address.Port}");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
        return request;
    }

    private static async Task<HttpResponseMessage> PostConfirmAsync(
        HttpClient client,
        Uri address,
        string location,
        string html,
        CancellationToken token)
    {
        var content = new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", HiddenValue(html, "__RequestVerificationToken")),
            new("_handler", HiddenValue(html, "_handler")),
        ]);
        var request = SameOriginPost(address, location, content);
        var response = await client.SendAsync(request, token);
        request.Dispose();
        content.Dispose();
        return response;
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

    private static async Task<long> RowCountAsync(string databasePath, string table, CancellationToken token)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "quest_sessions", "quest_reports", "xp_events", "mutation_receipts",
        };
        Assert.Contains(table, allowed);
        var value = await ScalarAsync(databasePath, $"SELECT COUNT(*) FROM {table};", token);
        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static async Task<string> ScalarTextAsync(string databasePath, string sql, CancellationToken token) =>
        Convert.ToString(await ScalarAsync(databasePath, sql, token), CultureInfo.InvariantCulture) ?? string.Empty;

    private static async Task<object?> ScalarAsync(string databasePath, string sql, CancellationToken token)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(token);
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

    private static async Task<CliResult> RunCliAsync(string home, CancellationToken token, params string[] args)
    {
        var root = FindRepositoryRoot();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var appDll = Path.Combine(root, "src", "HeroPassport.App", "bin", configuration, "net10.0", "HeroPassport.App.dll");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };
        startInfo.ArgumentList.Add(appDll);
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }
        startInfo.Environment["HERO_PASSPORT_HOME"] = home;
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Hero Passport CLI process did not start.");
        process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEndAsync(token);
        var stderr = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        return new(process.ExitCode, await stdout, await stderr);
    }

    private static Sandbox CreateSandbox()
    {
        var root = Path.Combine(Path.GetTempPath(), "HeroPassport.Web.FinishQuest.Tests", Guid.NewGuid().ToString("N"));
        var home = Path.Combine(root, "home");
        var project = Path.Combine(root, "sample-project");
        Directory.CreateDirectory(home);
        Directory.CreateDirectory(project);
        return new(root, home, project);
    }

    private static void DeleteSandbox(string root)
    {
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

    private sealed record Sandbox(string Root, string Home, string ProjectRoot);
    private sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);

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
