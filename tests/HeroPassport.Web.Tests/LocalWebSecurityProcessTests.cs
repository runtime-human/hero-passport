using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Xunit;

namespace HeroPassport.Web.Tests;

public sealed class LocalWebSecurityProcessTests
{
    private const string TestBootstrap = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8";
    private const string TestSession = "ICEiIyQlJicoKSorLC0uLzAxMjM0NTY3ODk6Ozw9Pj8";

    [Fact]
    public async Task DirectDashboardGetWithoutSessionFailsClosed()
    {
        var token = TestContext.Current.CancellationToken;
        var sandbox = CreateSandbox();
        try
        {
            await using var web = await StartWebAsync(sandbox.Home, sandbox.ProjectRoot, token);
            using var client = new HttpClient { BaseAddress = web.Address };

            using var response = await client.GetAsync("/", token);
            var body = await response.Content.ReadAsStringAsync(token);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.DoesNotContain(Path.GetFileName(sandbox.ProjectRoot), body, StringComparison.Ordinal);
            Assert.DoesNotContain(sandbox.ProjectRoot, body, StringComparison.Ordinal);
            Assert.DoesNotContain(TestBootstrap, body, StringComparison.Ordinal);
            Assert.DoesNotContain(TestSession, body, StringComparison.Ordinal);
        }
        finally
        {
            DeleteSandbox(sandbox.Root);
        }
    }

    [Fact]
    public async Task HostOutsideCanonicalIpv4LoopbackIsRejected()
    {
        var token = TestContext.Current.CancellationToken;
        var sandbox = CreateSandbox();
        try
        {
            await using var web = await StartWebAsync(sandbox.Home, sandbox.ProjectRoot, token);
            using var client = new HttpClient { BaseAddress = web.Address };
            using var request = new HttpRequestMessage(HttpMethod.Get, "/__hero/bootstrap");
            request.Headers.Host = "attacker.example";

            using var response = await client.SendAsync(request, token);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            DeleteSandbox(sandbox.Root);
        }
    }

    [Fact]
    public async Task BootstrapShellIsPublicButContainsNoLocalStateOrCapability()
    {
        var token = TestContext.Current.CancellationToken;
        var sandbox = CreateSandbox();
        try
        {
            await using var web = await StartWebAsync(sandbox.Home, sandbox.ProjectRoot, token);
            using var client = new HttpClient { BaseAddress = web.Address };

            using var response = await client.GetAsync("/__hero/bootstrap", token);
            var html = await response.Content.ReadAsStringAsync(token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain(Path.GetFileName(sandbox.ProjectRoot), html, StringComparison.Ordinal);
            Assert.DoesNotContain(sandbox.ProjectRoot, html, StringComparison.Ordinal);
            Assert.DoesNotContain(sandbox.Home, html, StringComparison.Ordinal);
            Assert.DoesNotContain(TestBootstrap, html, StringComparison.Ordinal);
            Assert.DoesNotContain(TestSession, html, StringComparison.Ordinal);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        }
        finally
        {
            DeleteSandbox(sandbox.Root);
        }
    }

    [Fact]
    public async Task HostileUrlOverrideStillCannotWidenListener()
    {
        var token = TestContext.Current.CancellationToken;
        var sandbox = CreateSandbox();
        try
        {
            await using var web = await StartWebAsync(sandbox.Home, sandbox.ProjectRoot, token);

            Assert.True(
                IPAddress.TryParse(web.Address.Host, out var address) && IPAddress.IsLoopback(address),
                $"Expected loopback-only listener, got '{web.Address}'. Output: {web.JoinedOutput}");
            Assert.Equal(IPAddress.Loopback.ToString(), web.Address.Host);
        }
        finally
        {
            DeleteSandbox(sandbox.Root);
        }
    }

    [Fact]
    public async Task DeterministicSecretsNeverAppearInNormalProcessOutput()
    {
        var token = TestContext.Current.CancellationToken;
        var sandbox = CreateSandbox();
        try
        {
            await using var web = await StartWebAsync(sandbox.Home, sandbox.ProjectRoot, token);

            Assert.DoesNotContain(TestBootstrap, web.JoinedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain(TestSession, web.JoinedOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteSandbox(sandbox.Root);
        }
    }

    private static async Task<WebProcessHandle> StartWebAsync(
        string home,
        string projectRoot,
        CancellationToken cancellationToken)
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
        startInfo.ArgumentList.Add(projectRoot);
        startInfo.ArgumentList.Add("--no-open-browser");
        startInfo.Environment["HERO_PASSPORT_HOME"] = home;
        startInfo.Environment["ASPNETCORE_URLS"] = "http://0.0.0.0:0";
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
        startInfo.Environment["HERO_PASSPORT_WEB_TEST_BOOTSTRAP"] = TestBootstrap;
        startInfo.Environment["HERO_PASSPORT_WEB_TEST_SESSION"] = TestSession;

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
        var root = Path.Combine(Path.GetTempPath(), "HeroPassport.Web.Security.Tests", Guid.NewGuid().ToString("N"));
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
