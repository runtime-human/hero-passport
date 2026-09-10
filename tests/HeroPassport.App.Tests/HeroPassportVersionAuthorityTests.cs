using HeroPassport.Application.Runtime;
using System.Diagnostics;
using Xunit;

namespace HeroPassport.App.Tests;

public sealed class HeroPassportVersionAuthorityTests
{
    [Fact]
    public async Task RootVersionMatchesRuntimeProductVersionWithoutInitializingStorage()
    {
        var token = TestContext.Current.CancellationToken;
        var home = Path.Combine(Path.GetTempPath(), "HeroPassport.Version.Tests", Guid.NewGuid().ToString("N"));
        Assert.False(Directory.Exists(home));

        var result = await RunCliAsync(home, token, "--version");

        Assert.Equal(0, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
        Assert.Equal(HeroPassportVersions.ProductVersion, result.StandardOutput.Trim());
        Assert.Matches("^0\\.1\\.0(?:-dev)?$", HeroPassportVersions.ProductVersion);
        Assert.False(Directory.Exists(home));
    }

    private static async Task<CliResult> RunCliAsync(string home, CancellationToken token, params string[] args)
    {
        var repoRoot = FindRepoRoot();
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

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HeroPassport.slnx")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Hero Passport repository root was not found from the test output directory.");
    }

    private sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);
}
