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
        var repoRoot = FindRepositoryRoot();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var webDll = Path.Combine(repoRoot, "src", "HeroPassport.Web", "bin", configuration, "net10.0", "HeroPassport.Web.dll");
        Assert.True(File.Exists(webDll), $"HeroPassport.Web was not built at {webDll}.");

        var home = Path.Combine(Path.GetTempPath(), "HeroPassport.Web.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(home);
        try
        {
            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = repoRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add(webDll);
            startInfo.Environment["HERO_PASSPORT_HOME"] = home;
            startInfo.Environment["ASPNETCORE_URLS"] = "http://0.0.0.0:0";
            startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Hero Passport Web process did not start.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            var output = new ConcurrentQueue<string>();
            var listeningAddress = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);

            var stdout = PumpAsync(process.StandardOutput, output, listeningAddress, timeout.Token);
            var stderr = PumpAsync(process.StandardError, output, listeningAddress, timeout.Token);

            try
            {
                var uri = await listeningAddress.Task.WaitAsync(timeout.Token);
                Assert.True(
                    IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address),
                    $"Expected a loopback listener, got '{uri}'. Output: {string.Join(Environment.NewLine, output)}");
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                await process.WaitForExitAsync(CancellationToken.None);
                await Task.WhenAll(stdout, stderr);
            }
        }
        finally
        {
            try
            {
                Directory.Delete(home, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static async Task PumpAsync(
        StreamReader reader,
        ConcurrentQueue<string> output,
        TaskCompletionSource<Uri> listeningAddress,
        CancellationToken cancellationToken)
    {
        const string marker = "Now listening on:";
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            output.Enqueue(line);
            var index = line.IndexOf(marker, StringComparison.Ordinal);
            if (index >= 0 && Uri.TryCreate(line[(index + marker.Length)..].Trim(), UriKind.Absolute, out var uri))
            {
                listeningAddress.TrySetResult(uri);
            }
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
}
