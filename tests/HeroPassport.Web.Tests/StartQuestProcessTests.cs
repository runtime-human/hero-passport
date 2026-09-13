using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace HeroPassport.Web.Tests;

public sealed class StartQuestProcessTests
{
    private const string TestBootstrap = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8";
    private const string TestSession = "ICEiIyQlJicoKSorLC0uLzAxMjM0NTY3ODk6Ozw9Pj8";

    [Fact]
    public async Task ConfirmedStartMutatesOnlyOnConfirmAndDuplicateConfirmIsIdempotent()
    {
        var token = TestContext.Current.CancellationToken;
        var sandbox = CreateSandbox();
        try
        {
            var init = await RunCliAsync(
                sandbox.Home,
                token,
                "init", "--locale", "en-US", "--hero-name", "Web Nova", "--json");
            Assert.Equal(0, init.ExitCode);

            await using var web = await StartWebAsync(sandbox.Home, sandbox.ProjectRoot, token);
            using var client = CreateClient(web.Address);
            await BootstrapAsync(client, web, token);

            using var startGet = await client.GetAsync("/quests/start", token);
            var startHtml = await startGet.Content.ReadAsStringAsync(token);
            Assert.Equal(HttpStatusCode.OK, startGet.StatusCode);
            Assert.Contains("StartQuestPrepare", startHtml, StringComparison.Ordinal);
            Assert.Contains("Web Nova", startHtml, StringComparison.Ordinal);

            var prepareToken = HiddenValue(startHtml, "__RequestVerificationToken");
            var prepareHandler = HiddenValue(startHtml, "_handler");
            using var prepareContent = new FormUrlEncodedContent(
            [
                new("__RequestVerificationToken", prepareToken),
                new("_handler", prepareHandler),
                new("Input.QuestType", "coding"),
                new("Input.Title", "  Process   Quest  "),
                new("Input.Goal", "  Prove   confirm   before   mutation  "),
            ]);
            using var prepareRequest = SameOriginPost(
                web.Address,
                "/quests/start",
                prepareContent);
            using var prepareResponse = await client.SendAsync(prepareRequest, token);

            Assert.True(
                prepareResponse.StatusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther,
                $"Prepare returned {(int)prepareResponse.StatusCode}: {await prepareResponse.Content.ReadAsStringAsync(token)}");
            var location = prepareResponse.Headers.Location?.OriginalString;
            Assert.Matches("^/quests/start/confirm/[A-Za-z0-9_-]{22}$", location ?? string.Empty);
            Assert.DoesNotContain("Process", location ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain("confirm", location?.Split('/').LastOrDefault() ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            var databasePath = Path.Combine(sandbox.Home, "hero-passport.db");
            await AssertRowCountAsync(databasePath, "quest_sessions", 0, token);
            await AssertRowCountAsync(databasePath, "mutation_receipts", 1, token); // bootstrap only

            using var confirmGet = await client.GetAsync(location, token);
            var confirmHtml = await confirmGet.Content.ReadAsStringAsync(token);
            Assert.Equal(HttpStatusCode.OK, confirmGet.StatusCode);
            Assert.Contains("Process Quest", confirmHtml, StringComparison.Ordinal);
            Assert.Contains("Prove confirm before mutation", confirmHtml, StringComparison.Ordinal);
            Assert.Contains("Web Nova", confirmHtml, StringComparison.Ordinal);
            Assert.Contains(Path.GetFileName(sandbox.ProjectRoot), confirmHtml, StringComparison.Ordinal);
            Assert.DoesNotContain(sandbox.ProjectRoot, confirmHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("hero_id", confirmHtml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("request_id", confirmHtml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("workspace_fingerprint", confirmHtml, StringComparison.OrdinalIgnoreCase);

            using var confirmResponse = await PostConfirmAsync(client, web.Address, location!, confirmHtml, token);
            Assert.True(
                confirmResponse.StatusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther,
                $"Confirm returned {(int)confirmResponse.StatusCode}: {await confirmResponse.Content.ReadAsStringAsync(token)}");
            Assert.Equal("/", confirmResponse.Headers.Location?.OriginalString);

            await AssertRowCountAsync(databasePath, "quest_sessions", 1, token);
            await AssertRowCountAsync(databasePath, "mutation_receipts", 2, token); // bootstrap + start
            await AssertRowCountAsync(databasePath, "hero_project_stats", 1, token);

            using var committedGet = await client.GetAsync(location, token);
            var committedHtml = await committedGet.Content.ReadAsStringAsync(token);
            Assert.Equal(HttpStatusCode.OK, committedGet.StatusCode);
            Assert.Contains("Quest already started", committedHtml, StringComparison.Ordinal);

            using var duplicateResponse = await PostConfirmAsync(client, web.Address, location!, committedHtml, token);
            Assert.True(
                duplicateResponse.StatusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther,
                $"Duplicate confirm returned {(int)duplicateResponse.StatusCode}.");
            await AssertRowCountAsync(databasePath, "quest_sessions", 1, token);
            await AssertRowCountAsync(databasePath, "mutation_receipts", 2, token);

            using var dashboard = await client.GetAsync("/", token);
            var dashboardHtml = await dashboard.Content.ReadAsStringAsync(token);
            Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
            Assert.Contains("Process Quest", dashboardHtml, StringComparison.Ordinal);
        }
        finally
        {
            DeleteSandbox(sandbox.Root);
        }
    }

    [Fact]
    public async Task StartMutationRoutesFailClosedForSessionAntiforgeryAndRequestAbuse()
    {
        var token = TestContext.Current.CancellationToken;
        var sandbox = CreateSandbox();
        try
        {
            var init = await RunCliAsync(
                sandbox.Home,
                token,
                "init", "--locale", "en-US", "--hero-name", "Boundary Hero", "--json");
            Assert.Equal(0, init.ExitCode);

            await using var web = await StartWebAsync(sandbox.Home, sandbox.ProjectRoot, token);
            using var anonymous = CreateClient(web.Address);
            using var unauthorizedGet = await anonymous.GetAsync("/quests/start", token);
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedGet.StatusCode);

            using var client = CreateClient(web.Address);
            await BootstrapAsync(client, web, token);

            using var missingTokenContent = new FormUrlEncodedContent(
            [
                new("_handler", "StartQuestPrepare"),
                new("Input.QuestType", "coding"),
                new("Input.Title", "No token"),
                new("Input.Goal", "Must fail"),
            ]);
            using var missingTokenRequest = SameOriginPost(web.Address, "/quests/start", missingTokenContent);
            using var missingTokenResponse = await client.SendAsync(missingTokenRequest, token);
            Assert.Equal(HttpStatusCode.BadRequest, missingTokenResponse.StatusCode);

            using var multipart = new MultipartFormDataContent();
            multipart.Add(new StringContent("coding"), "Input.QuestType");
            using var multipartRequest = SameOriginPost(web.Address, "/quests/start", multipart);
            using var multipartResponse = await client.SendAsync(multipartRequest, token);
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, multipartResponse.StatusCode);

            using var oversizedContent = new StringContent(
                new string('x', 9000),
                System.Text.Encoding.UTF8,
                "application/x-www-form-urlencoded");
            using var oversizedRequest = SameOriginPost(web.Address, "/quests/start", oversizedContent);
            using var oversizedResponse = await client.SendAsync(oversizedRequest, token);
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversizedResponse.StatusCode);

            var databasePath = Path.Combine(sandbox.Home, "hero-passport.db");
            await AssertRowCountAsync(databasePath, "quest_sessions", 0, token);
        }
        finally
        {
            DeleteSandbox(sandbox.Root);
        }
    }

    [Fact]
    public async Task MalformedAndUnknownConfirmationHandlesReturnBoundedHttpStatus()
    {
        var token = TestContext.Current.CancellationToken;
        var sandbox = CreateSandbox();
        try
        {
            var init = await RunCliAsync(
                sandbox.Home,
                token,
                "init", "--locale", "en-US", "--hero-name", "Status Hero", "--json");
            Assert.Equal(0, init.ExitCode);

            await using var web = await StartWebAsync(sandbox.Home, sandbox.ProjectRoot, token);
            using var client = CreateClient(web.Address);
            await BootstrapAsync(client, web, token);

            using var malformed = await client.GetAsync("/quests/start/confirm/not-valid", token);
            Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
            var malformedBody = await malformed.Content.ReadAsStringAsync(token);
            Assert.DoesNotContain(sandbox.ProjectRoot, malformedBody, StringComparison.Ordinal);

            using var unknown = await client.GetAsync(
                "/quests/start/confirm/AAAAAAAAAAAAAAAAAAAAAA",
                token);
            Assert.Equal(HttpStatusCode.Gone, unknown.StatusCode);
            var unknownBody = await unknown.Content.ReadAsStringAsync(token);
            Assert.Contains("no longer available", unknownBody, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteSandbox(sandbox.Root);
        }
    }

    private static HttpRequestMessage SameOriginPost(
        Uri address,
        string path,
        HttpContent content)
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
        var antiforgery = HiddenValue(html, "__RequestVerificationToken");
        var handler = HiddenValue(html, "_handler");
        var content = new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", antiforgery),
            new("_handler", handler),
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
        var value = Regex.Match(
            tag.Value,
            "value=\"([^\"]*)\"",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
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

    private static async Task BootstrapAsync(
        HttpClient client,
        WebProcessHandle web,
        CancellationToken token)
    {
        using var shell = await client.GetAsync("/__hero/bootstrap", token);
        var html = await shell.Content.ReadAsStringAsync(token);
        Assert.Equal(HttpStatusCode.OK, shell.StatusCode);
        var antiforgery = HiddenValue(html, "__RequestVerificationToken");
        using var content = new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", antiforgery),
            new("capability", TestBootstrap),
        ]);
        using var request = SameOriginPost(web.Address, "/__hero/bootstrap/claim", content);
        using var response = await client.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
    }

    private static async Task AssertRowCountAsync(
        string databasePath,
        string table,
        long expected,
        CancellationToken token)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "quest_sessions", "mutation_receipts", "hero_project_stats",
        };
        Assert.Contains(table, allowed);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        var count = Convert.ToInt64(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
        Assert.Equal(expected, count);
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

    private static async Task<CliResult> RunCliAsync(
        string home,
        CancellationToken token,
        params string[] args)
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
        var root = Path.Combine(Path.GetTempPath(), "HeroPassport.Web.StartQuest.Tests", Guid.NewGuid().ToString("N"));
        var home = Path.Combine(root, "home");
        var project = Path.Combine(root, "sample-project");
        Directory.CreateDirectory(home);
        Directory.CreateDirectory(project);
        return new(root, home, project);
    }

    private static void DeleteSandbox(string root)
    {
        try { Directory.Delete(root, recursive: true); } catch (IOException) { }
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
        throw new DirectoryNotFoundException("Could not find HeroPassport.slnx.");
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
