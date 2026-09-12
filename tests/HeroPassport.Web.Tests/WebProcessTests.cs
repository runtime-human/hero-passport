using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Xunit;

namespace HeroPassport.Web.Tests;

public sealed class WebProcessTests
{
    [Fact]
    public async Task WebProcessIgnoresExternalUrlOverrideAndBindsLoopbackOnly()
    {
        var token = TestContext.Current.CancellationToken;
        var sandbox = CreateSandbox();
        try
        {
            await using var web = await StartWebAsync(sandbox.Home, sandbox.ProjectRoot, token);
            Assert.True(
                IPAddress.TryParse(web.Address.Host, out var address) && IPAddress.IsLoopback(address),
                $"Expected a loopback listener, got '{web.Address}'. Output: {web.JoinedOutput}");
        }
        finally
        {
            DeleteSandbox(sandbox.Root);
        }
    }

    [Fact]
    public async Task FreshStorageRendersBoundedSetupRequiredDashboard()
    {
        var token = TestContext.Current.CancellationToken;
        var sandbox = CreateSandbox();
        try
        {
            await using var web = await StartWebAsync(sandbox.Home, sandbox.ProjectRoot, token);
            using var client = new HttpClient { BaseAddress = web.Address };

            using var response = await client.GetAsync("/", token);
            var html = await response.Content.ReadAsStringAsync(token);

            Assert.True(response.IsSuccessStatusCode, $"Dashboard GET failed with {(int)response.StatusCode}. Body: {html}");
            Assert.Contains("Setup required", html, StringComparison.Ordinal);
            Assert.Contains(Path.GetFileName(sandbox.ProjectRoot), html, StringComparison.Ordinal);
            Assert.DoesNotContain(sandbox.ProjectRoot, html, StringComparison.Ordinal);
            Assert.DoesNotContain(sandbox.Home, html, StringComparison.Ordinal);
            Assert.DoesNotContain("Weather", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Counter", html, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteSandbox(sandbox.Root);
        }
    }

    [Fact]
    public async Task ConfiguredStorageRendersExistingApplicationHeroCardTruth()
    {
        var token = TestContext.Current.CancellationToken;
        var sandbox = CreateSandbox();
        try
        {
            var init = await RunCliAsync(
                sandbox.Home,
                token,
                "init",
                "--locale", "en-US",
                "--hero-name", "Web Nova",
                "--json");
            Assert.Equal(0, init.ExitCode);

            await using var web = await StartWebAsync(sandbox.Home, sandbox.ProjectRoot, token);
            using var client = new HttpClient { BaseAddress = web.Address };

            using var response = await client.GetAsync("/", token);
            var html = await response.Content.ReadAsStringAsync(token);

            Assert.True(response.IsSuccessStatusCode, $"Dashboard GET failed with {(int)response.StatusCode}. Body: {html}");
            Assert.Contains("Web Nova", html, StringComparison.Ordinal);
            Assert.Contains("Level 1", html, StringComparison.Ordinal);
            Assert.Contains("code_squire", html, StringComparison.Ordinal);
            Assert.Contains(Path.GetFileName(sandbox.ProjectRoot), html, StringComparison.Ordinal);
            Assert.Contains("No open Quest", html, StringComparison.Ordinal);
            Assert.DoesNotContain(sandbox.ProjectRoot, html, StringComparison.Ordinal);
            Assert.DoesNotContain(sandbox.Home, html, StringComparison.Ordinal);
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
        var webDll = Path.Combine(repoRoot, "src", "HeroPassport.Web", "bin", configuration, "net10.0", "HeroPassport.Web.dll");
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
        startInfo.Environment["HERO_PASSPORT_HOME"] = home;
        startInfo.Environment["ASPNETCORE_URLS"] = "http://0.0.0.0:0";
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Hero Passport Web process did not start.");
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

    private static async Task<CliResult> RunCliAsync(string home, CancellationToken token, params string[] args)
    {
        var repoRoot = FindRepositoryRoot();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var appDll = Path.Combine(repoRoot, "src", "HeroPassport.App", "bin", configuration, "net10.0", "HeroPassport.App.dll");
        Assert.True(File.Exists(appDll), $"HeroPassport.App was not built at {appDll}.");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repoRoot,
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
        return new CliResult(process.ExitCode, await stdout, await stderr);
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
        var root = Path.Combine(Path.GetTempPath(), "HeroPassport.Web.Tests", Guid.NewGuid().ToString("N"));
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
