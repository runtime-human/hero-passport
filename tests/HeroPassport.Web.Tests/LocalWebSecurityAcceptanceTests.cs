using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace HeroPassport.Web.Tests;

public sealed class LocalWebSecurityAcceptanceTests
{
    private const string TestBootstrap = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8";
    private const string TestSession = "ICEiIyQlJicoKSorLC0uLzAxMjM0NTY3ODk6Ozw9Pj8";
    private const string AlternateBootstrap = "QEFCQ0RFRkdISUpLTE1OT1BRUlNUVVZXWFlaW1xdXl8";
    private const string AlternateSession = "YGFiY2RlZmdoaWprbG1ub3BxcnN0dXZ3eHl6e3x9fn8";

    [Fact]
    public async Task WrongCapabilityDoesNotConsumeLegitimateBootstrap()
    {
        var token = TestContext.Current.CancellationToken;
        var sandbox = CreateSandbox();
        try
        {
            await using var web = await StartTestingWebAsync(sandbox, token);
            using var client = CreateClient(web.Address);

            var anti = await GetAntiforgeryTokenAsync(client, token);
            using var wrong = await PostClaimAsync(client, web.Address, anti, AlternateBootstrap, token);
            Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);

            anti = await GetAntiforgeryTokenAsync(client, token);
            using var correct = await PostClaimAsync(client, web.Address, anti, TestBootstrap, token);
            Assert.Equal(HttpStatusCode.SeeOther, correct.StatusCode);
        }
        finally
        {
            DeleteSandbox(sandbox.Root);
        }
    }

    [Fact]
    public async Task SuccessfulBootstrapReplayIsRejected()
    {
        var token = TestContext.Current.CancellationToken;
        var sandbox = CreateSandbox();
        try
        {
            await using var web = await StartTestingWebAsync(sandbox, token);
            using var client = CreateClient(web.Address);

            var anti = await GetAntiforgeryTokenAsync(client, token);
            using var first = await PostClaimAsync(client, web.Address, anti, TestBootstrap, token);
            Assert.Equal(HttpStatusCode.SeeOther, first.StatusCode);

            anti = await GetAntiforgeryTokenAsync(client, token);
            using var replay = await PostClaimAsync(client, web.Address, anti, TestBootstrap, token);
            Assert.Equal(HttpStatusCode.Forbidden, replay.StatusCode);
        }
        finally
        {
            DeleteSandbox(sandbox.Root);
        }
    }

    [Fact]
    public async Task MissingAntiforgeryTokenIsRejectedWithoutConsumingBootstrap()
    {
        var token = TestContext.Current.CancellationToken;
        var sandbox = CreateSandbox();
        try
        {
            await using var web = await StartTestingWebAsync(sandbox, token);
            using var client = CreateClient(web.Address);

            using var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("capability", TestBootstrap),
            ]);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/__hero/bootstrap/claim")
            {
                Content = content,
            };
            request.Headers.TryAddWithoutValidation("Origin", CanonicalOrigin(web.Address));
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
            using var rejected = await client.SendAsync(request, token);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

            var anti = await GetAntiforgeryTokenAsync(client, token);
            using var accepted = await PostClaimAsync(client, web.Address, anti, TestBootstrap, token);
            Assert.Equal(HttpStatusCode.SeeOther, accepted.StatusCode);
        }
        finally
        {
            DeleteSandbox(sandbox.Root);
        }
    }

    [Fact]
    public async Task CrossSiteBootstrapClaimIsRejectedWithoutConsumingBootstrap()
    {
        var token = TestContext.Current.CancellationToken;
        var sandbox = CreateSandbox();
        try
        {
            await using var web = await StartTestingWebAsync(sandbox, token);
            using var client = CreateClient(web.Address);

            var anti = await GetAntiforgeryTokenAsync(client, token);
            using var hostile = await PostClaimAsync(
                client,
                web.Address,
                anti,
                TestBootstrap,
                token,
                origin: "https://attacker.example",
                fetchSite: "cross-site");
            Assert.Equal(HttpStatusCode.BadRequest, hostile.StatusCode);

            anti = await GetAntiforgeryTokenAsync(client, token);
            using var accepted = await PostClaimAsync(client, web.Address, anti, TestBootstrap, token);
            Assert.Equal(HttpStatusCode.SeeOther, accepted.StatusCode);
        }
        finally
        {
            DeleteSandbox(sandbox.Root);
        }
    }

    [Fact]
    public async Task PreviousProcessSessionCookieIsRejectedByNewProcess()
    {
        var token = TestContext.Current.CancellationToken;
        var firstSandbox = CreateSandbox();
        var secondSandbox = CreateSandbox();
        try
        {
            await using (var first = await StartTestingWebAsync(firstSandbox, token))
            {
                using var client = CreateClient(first.Address);
                var anti = await GetAntiforgeryTokenAsync(client, token);
                using var accepted = await PostClaimAsync(client, first.Address, anti, TestBootstrap, token);
                Assert.Equal(HttpStatusCode.SeeOther, accepted.StatusCode);
                using var dashboard = await client.GetAsync("/", token);
                Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
            }

            await using var second = await StartTestingWebAsync(
                secondSandbox,
                token,
                AlternateBootstrap,
                AlternateSession);
            using var staleClient = CreateClient(second.Address);
            staleClient.DefaultRequestHeaders.TryAddWithoutValidation(
                "Cookie",
                $".HeroPassport.LocalSession={TestSession}");

            using var stale = await staleClient.GetAsync("/", token);
            Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
        }
        finally
        {
            DeleteSandbox(firstSandbox.Root);
            DeleteSandbox(secondSandbox.Root);
        }
    }

    [Fact]
    public async Task DeterministicSecuritySecretsAreNotPersistedInSqlite()
    {
        var token = TestContext.Current.CancellationToken;
        var sandbox = CreateSandbox();
        try
        {
            await using var web = await StartTestingWebAsync(sandbox, token);
            using var client = CreateClient(web.Address);
            var anti = await GetAntiforgeryTokenAsync(client, token);
            using var accepted = await PostClaimAsync(client, web.Address, anti, TestBootstrap, token);
            Assert.Equal(HttpStatusCode.SeeOther, accepted.StatusCode);

            var databasePath = Path.Combine(sandbox.Home, "hero-passport.db");
            Assert.True(File.Exists(databasePath));
            var databaseBytes = await File.ReadAllBytesAsync(databasePath, token);
            var searchable = Encoding.Latin1.GetString(databaseBytes);
            Assert.DoesNotContain(TestBootstrap, searchable, StringComparison.Ordinal);
            Assert.DoesNotContain(TestSession, searchable, StringComparison.Ordinal);
        }
        finally
        {
            DeleteSandbox(sandbox.Root);
        }
    }

    [Fact]
    public async Task ProductionRejectsNoOpenBrowserTestBypass()
    {
        var token = TestContext.Current.CancellationToken;
        var sandbox = CreateSandbox();
        try
        {
            var result = await RunWebToExitAsync(
                sandbox,
                environment: "Production",
                addNoOpenBrowser: true,
                bootstrap: null,
                session: null,
                token);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(
                "--no-open-browser is only available in the Testing environment",
                result.Output,
                StringComparison.Ordinal);
            Assert.DoesNotContain(TestBootstrap, result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain(TestSession, result.Output, StringComparison.Ordinal);
        }
        finally
        {
            DeleteSandbox(sandbox.Root);
        }
    }

    [Fact]
    public async Task ProductionRejectsDeterministicTestSecrets()
    {
        var token = TestContext.Current.CancellationToken;
        var sandbox = CreateSandbox();
        try
        {
            var result = await RunWebToExitAsync(
                sandbox,
                environment: "Production",
                addNoOpenBrowser: false,
                bootstrap: TestBootstrap,
                session: TestSession,
                token);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(
                "Web test secrets are only accepted in the Testing environment",
                result.Output,
                StringComparison.Ordinal);
            Assert.DoesNotContain(TestBootstrap, result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain(TestSession, result.Output, StringComparison.Ordinal);
        }
        finally
        {
            DeleteSandbox(sandbox.Root);
        }
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

    private static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        CancellationToken token)
    {
        using var response = await client.GetAsync("/__hero/bootstrap", token);
        var html = await response.Content.ReadAsStringAsync(token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"Bootstrap shell did not render an antiforgery token. Body: {html}");
        return match.Groups[1].Value;
    }

    private static async Task<HttpResponseMessage> PostClaimAsync(
        HttpClient client,
        Uri address,
        string antiforgeryToken,
        string capability,
        CancellationToken token,
        string? origin = null,
        string fetchSite = "same-origin")
    {
        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("__RequestVerificationToken", antiforgeryToken),
            new KeyValuePair<string, string>("capability", capability),
        ]);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/__hero/bootstrap/claim")
        {
            Content = content,
        };
        request.Headers.TryAddWithoutValidation("Origin", origin ?? CanonicalOrigin(address));
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", fetchSite);
        return await client.SendAsync(request, token);
    }

    private static string CanonicalOrigin(Uri address) =>
        $"http://127.0.0.1:{address.Port}";

    private static async Task<WebProcessHandle> StartTestingWebAsync(
        Sandbox sandbox,
        CancellationToken cancellationToken,
        string bootstrap = TestBootstrap,
        string session = TestSession)
    {
        var startInfo = CreateStartInfo(sandbox, "Testing", addNoOpenBrowser: true, bootstrap, session);
        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Hero Passport Web process did not start.");
        var output = new ConcurrentQueue<string>();
        var listeningAddress = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stdout = PumpAsync(process.StandardOutput, output, listeningAddress);
        var stderr = PumpAsync(process.StandardError, output, listeningAddress);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var address = await listeningAddress.Task.WaitAsync(timeout.Token);
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

    private static async Task<ProcessResult> RunWebToExitAsync(
        Sandbox sandbox,
        string environment,
        bool addNoOpenBrowser,
        string? bootstrap,
        string? session,
        CancellationToken token)
    {
        var startInfo = CreateStartInfo(
            sandbox,
            environment,
            addNoOpenBrowser,
            bootstrap,
            session);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Hero Passport Web process did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync(token);
        var stderr = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        return new ProcessResult(
            process.ExitCode,
            string.Concat(await stdout, Environment.NewLine, await stderr));
    }

    private static ProcessStartInfo CreateStartInfo(
        Sandbox sandbox,
        string environment,
        bool addNoOpenBrowser,
        string? bootstrap,
        string? session)
    {
        var repoRoot = FindRepositoryRoot();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var webDll = Path.Combine(
            repoRoot,
            "src",
            "HeroPassport.Web",
            "bin",
            configuration,
            "net10.0",
            "HeroPassport.Web.dll");
        Assert.True(File.Exists(webDll), $"HeroPassport.Web was not built at {webDll}.");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(webDll);
        startInfo.ArgumentList.Add("--project-root");
        startInfo.ArgumentList.Add(sandbox.ProjectRoot);
        if (addNoOpenBrowser)
        {
            startInfo.ArgumentList.Add("--no-open-browser");
        }

        startInfo.Environment["HERO_PASSPORT_HOME"] = sandbox.Home;
        startInfo.Environment["ASPNETCORE_URLS"] = "http://0.0.0.0:0";
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = environment;
        if (bootstrap is not null)
        {
            startInfo.Environment["HERO_PASSPORT_WEB_TEST_BOOTSTRAP"] = bootstrap;
        }
        if (session is not null)
        {
            startInfo.Environment["HERO_PASSPORT_WEB_TEST_SESSION"] = session;
        }

        return startInfo;
    }

    private static async Task PumpAsync(
        StreamReader reader,
        ConcurrentQueue<string> output,
        TaskCompletionSource<Uri> listeningAddress)
    {
        const string marker = "Now listening on:";
        while (await reader.ReadLineAsync() is { } line)
        {
            output.Enqueue(line);
            var index = line.IndexOf(marker, StringComparison.Ordinal);
            if (index >= 0 && Uri.TryCreate(line[(index + marker.Length)..].Trim(), UriKind.Absolute, out var uri))
            {
                listeningAddress.TrySetResult(uri);
            }
        }
    }

    private static Sandbox CreateSandbox()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "HeroPassport.Web.Security.Acceptance",
            Guid.NewGuid().ToString("N"));
        var home = Path.Combine(root, "home");
        var projectRoot = Path.Combine(root, "sample-project");
        Directory.CreateDirectory(home);
        Directory.CreateDirectory(projectRoot);
        return new Sandbox(root, home, projectRoot);
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

        throw new DirectoryNotFoundException("Could not find HeroPassport.slnx from the test base directory.");
    }

    private sealed record Sandbox(string Root, string Home, string ProjectRoot);
    private sealed record ProcessResult(int ExitCode, string Output);

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
