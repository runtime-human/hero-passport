using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace HeroPassport.Web.Tests;

public sealed class StartQuestAdditionalProcessTests
{
    private const string TestBootstrap = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8";
    private const string TestSession = "ICEiIyQlJicoKSorLC0uLzAxMjM0NTY3ODk6Ozw9Pj8";

    [Fact]
    public async Task AnonymousCrossSiteAndInvalidPreparePostsFailWithoutMutationOrDiagnosticLeakage()
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
            using var anonymousContent = new FormUrlEncodedContent(
            [
                new("_handler", "StartQuestPrepare"),
                new("Input.QuestType", "coding"),
                new("Input.Title", "Anonymous Secret Title"),
                new("Input.Goal", "Anonymous Secret Goal"),
            ]);
            using var anonymousRequest = SameOriginPost(web.Address, "/quests/start", anonymousContent);
            using var anonymousResponse = await anonymous.SendAsync(anonymousRequest, token);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

            using var client = CreateClient(web.Address);
            await BootstrapAsync(client, web, token);
            using var startGet = await client.GetAsync("/quests/start", token);
            var startHtml = await startGet.Content.ReadAsStringAsync(token);
            Assert.Equal(HttpStatusCode.OK, startGet.StatusCode);
            var antiforgery = HiddenValue(startHtml, "__RequestVerificationToken");
            var handler = HiddenValue(startHtml, "_handler");

            using var crossSiteContent = new FormUrlEncodedContent(
            [
                new("__RequestVerificationToken", antiforgery),
                new("_handler", handler),
                new("Input.QuestType", "coding"),
                new("Input.Title", "Cross Site Secret Title"),
                new("Input.Goal", "Cross Site Secret Goal"),
            ]);
            using var crossSiteRequest = new HttpRequestMessage(HttpMethod.Post, "/quests/start")
            {
                Content = crossSiteContent,
            };
            crossSiteRequest.Headers.TryAddWithoutValidation("Origin", "https://attacker.example");
            crossSiteRequest.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "cross-site");
            using var crossSiteResponse = await client.SendAsync(crossSiteRequest, token);
            Assert.Equal(HttpStatusCode.BadRequest, crossSiteResponse.StatusCode);

            using var invalidContent = new FormUrlEncodedContent(
            [
                new("__RequestVerificationToken", antiforgery),
                new("_handler", handler),
                new("Input.QuestType", "not-a-quest-type"),
                new("Input.Title", "Invalid Secret Title"),
                new("Input.Goal", "Invalid Secret Goal"),
            ]);
            using var invalidRequest = SameOriginPost(web.Address, "/quests/start", invalidContent);
            using var invalidResponse = await client.SendAsync(invalidRequest, token);
            var invalidBody = await invalidResponse.Content.ReadAsStringAsync(token);
            Assert.Equal(HttpStatusCode.OK, invalidResponse.StatusCode);
            Assert.Contains("invalid", invalidBody, StringComparison.OrdinalIgnoreCase);

            var databasePath = Path.Combine(sandbox.Home, "hero-passport.db");
            await AssertRowCountAsync(databasePath, "quest_sessions", 0, token);
            Assert.DoesNotContain("Anonymous Secret Title", web.JoinedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Anonymous Secret Goal", web.JoinedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Cross Site Secret Title", web.JoinedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Cross Site Secret Goal", web.JoinedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Invalid Secret Title", web.JoinedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Invalid Secret Goal", web.JoinedOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteSandbox(sandbox.Root);
        }
    }

    [Fact]
    public async Task CompetingStartBetweenPrepareAndConfirmReturnsBoundedConflictWithoutDuplicateQuest()
    {
        var token = TestContext.Current.CancellationToken;
        var sandbox = CreateSandbox();
        try
        {
            var init = await RunCliAsync(
                sandbox.Home,
                token,
                "init", "--locale", "en-US", "--hero-name", "Race Hero", "--json");
            Assert.Equal(0, init.ExitCode);

            await using var web = await StartWebAsync(sandbox.Home, sandbox.ProjectRoot, token);
            using var client = CreateClient(web.Address);
            await BootstrapAsync(client, web, token);

            var first = await PrepareAsync(
                client,
                web.Address,
                "First Prepared Quest",
                "First prepared goal",
                token);
            var competing = await PrepareAsync(
                client,
                web.Address,
                "Competing Quest",
                "Competing goal",
                token);
            Assert.NotEqual(first, competing);

            using var competingGet = await client.GetAsync(competing, token);
            var competingHtml = await competingGet.Content.ReadAsStringAsync(token);
            Assert.Equal(HttpStatusCode.OK, competingGet.StatusCode);
            using var competingConfirm = await PostConfirmAsync(
                client,
                web.Address,
                competing,
                competingHtml,
                token);
            Assert.True(
                competingConfirm.StatusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther,
                $"Competing confirmation returned {(int)competingConfirm.StatusCode}.");

            var databasePath = Path.Combine(sandbox.Home, "hero-passport.db");
            await AssertRowCountAsync(databasePath, "quest_sessions", 1, token);

            using var firstGet = await client.GetAsync(first, token);
            var firstHtml = await firstGet.Content.ReadAsStringAsync(token);
            Assert.Equal(HttpStatusCode.OK, firstGet.StatusCode);
            Assert.Contains("First Prepared Quest", firstHtml, StringComparison.Ordinal);
            using var firstConfirm = await PostConfirmAsync(
                client,
                web.Address,
                first,
                firstHtml,
                token);
            var conflictBody = await firstConfirm.Content.ReadAsStringAsync(token);
            Assert.Equal(HttpStatusCode.OK, firstConfirm.StatusCode);
            Assert.Contains("current Hero or project state changed", conflictBody, StringComparison.Ordinal);
            Assert.Contains("First Prepared Quest", conflictBody, StringComparison.Ordinal);
            await AssertRowCountAsync(databasePath, "quest_sessions", 1, token);

            using var dashboard = await client.GetAsync("/", token);
            var dashboardHtml = await dashboard.Content.ReadAsStringAsync(token);
            Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
            Assert.Contains("Competing Quest", dashboardHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("First Prepared Quest", dashboardHtml, StringComparison.Ordinal);
        }
        finally
        {
            DeleteSandbox(sandbox.Root);
        }
    }

    private static async Task<string> PrepareAsync(
        HttpClient client,
        Uri address,
        string title,
        string goal,
        CancellationToken token)
    {
        using var get = await client.GetAsync("/quests/start", token);
        var html = await get.Content.ReadAsStringAsync(token);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var antiforgery = HiddenValue(html, "__RequestVerificationToken");
        var handler = HiddenValue(html, "_handler");
        using var content = new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", antiforgery),
            new("_handler", handler),
            new("Input.QuestType", "coding"),
            new("Input.Title", title),
            new("Input.Goal", goal),
        ]);
        using var request = SameOriginPost(address, "/quests/start", content);
        using var response = await client.SendAsync(request, token);
        Assert.True(
            response.StatusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther,
            $"Prepare returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(token)}");
        return RedirectPath(response, address);
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

    private static string RedirectPath(HttpResponseMessage response, Uri baseAddress)
    {
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
        Assert.Equal("quest_sessions", table);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM quest_sessions;";
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
        Assert.True(File.Exists(appDll), $"HeroPassport.App was not built at {appDll}.");
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
        var root = Path.Combine(Path.GetTempPath(), "HeroPassport.Web.StartQuest.Additional.Tests", Guid.NewGuid().ToString("N"));
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
            Directory.Delete(root, recursive: true);
        }
        catch (IOException)
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

        public WebProcessHandle(
            Process process,
            Uri address,
            ConcurrentQueue<string> output,
            Task stdout,
            Task stderr)
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
