using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace HeroPassport.Web.Tests;

public sealed class BootstrapRequestBoundaryTests
{
    private const string TestBootstrap = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8";
    private const string TestSession = "ICEiIyQlJicoKSorLC0uLzAxMjM0NTY3ODk6Ozw9Pj8";

    [Fact]
    public async Task OversizedUrlEncodedClaimIsRejectedBeforeBootstrapConsumption()
    {
        var token = TestContext.Current.CancellationToken;
        var sandbox = CreateSandbox();
        try
        {
            await using var web = await StartWebAsync(sandbox, token);
            using var client = CreateClient(web.Address);
            var antiforgery = await GetAntiforgeryTokenAsync(client, token);

            var body = string.Concat(
                "__RequestVerificationToken=", Uri.EscapeDataString(antiforgery),
                "&capability=", Uri.EscapeDataString(TestBootstrap),
                "&padding=", new string('x', 16 * 1024));
            using var content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/__hero/bootstrap/claim")
            {
                Content = content,
            };
            request.Headers.TryAddWithoutValidation("Origin", CanonicalOrigin(web.Address));
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");

            using var oversized = await client.SendAsync(request, token);
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);

            antiforgery = await GetAntiforgeryTokenAsync(client, token);
            using var accepted = await PostValidClaimAsync(client, web.Address, antiforgery, token);
            Assert.Equal(HttpStatusCode.SeeOther, accepted.StatusCode);
        }
        finally
        {
            DeleteSandbox(sandbox.Root);
        }
    }

    [Fact]
    public async Task MultipartClaimIsRejectedWithoutConsumingBootstrap()
    {
        var token = TestContext.Current.CancellationToken;
        var sandbox = CreateSandbox();
        try
        {
            await using var web = await StartWebAsync(sandbox, token);
            using var client = CreateClient(web.Address);
            var antiforgery = await GetAntiforgeryTokenAsync(client, token);

            using var multipart = new MultipartFormDataContent();
            multipart.Add(new StringContent(antiforgery), "__RequestVerificationToken");
            multipart.Add(new StringContent(TestBootstrap), "capability");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/__hero/bootstrap/claim")
            {
                Content = multipart,
            };
            request.Headers.TryAddWithoutValidation("Origin", CanonicalOrigin(web.Address));
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");

            using var rejected = await client.SendAsync(request, token);
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, rejected.StatusCode);

            antiforgery = await GetAntiforgeryTokenAsync(client, token);
            using var accepted = await PostValidClaimAsync(client, web.Address, antiforgery, token);
            Assert.Equal(HttpStatusCode.SeeOther, accepted.StatusCode);
        }
        finally
        {
            DeleteSandbox(sandbox.Root);
        }
    }

    private static HttpClient CreateClient(Uri address)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            CookieContainer = new CookieContainer(),
        };
        return new HttpClient(handler, disposeHandler: true) { BaseAddress = address };
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, CancellationToken token)
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

    private static async Task<HttpResponseMessage> PostValidClaimAsync(
        HttpClient client,
        Uri address,
        string antiforgery,
        CancellationToken token)
    {
        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("__RequestVerificationToken", antiforgery),
            new KeyValuePair<string, string>("capability", TestBootstrap),
        ]);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/__hero/bootstrap/claim")
        {
            Content = content,
        };
        request.Headers.TryAddWithoutValidation("Origin", CanonicalOrigin(address));
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
        return await client.SendAsync(request, token);
    }

    private static string CanonicalOrigin(Uri address) => $"http://127.0.0.1:{address.Port}";

    private static async Task<WebProcessHandle> StartWebAsync(Sandbox sandbox, CancellationToken token)
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
        startInfo.ArgumentList.Add("--no-open-browser");
        startInfo.Environment["HERO_PASSPORT_HOME"] = sandbox.Home;
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

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
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
        var root = Path.Combine(Path.GetTempPath(), "HeroPassport.Web.RequestBoundary", Guid.NewGuid().ToString("N"));
        var home = Path.Combine(root, "home");
        var projectRoot = Path.Combine(root, "project");
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
            _stdout = stdout;
            _stderr = stderr;
        }

        public Uri Address { get; }

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
