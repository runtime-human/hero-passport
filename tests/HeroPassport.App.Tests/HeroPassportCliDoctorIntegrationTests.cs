using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace HeroPassport.App.Tests;

public sealed class HeroPassportCliDoctorIntegrationTests
{
    [Fact]
    public async Task DoctorJsonReportsUninitializedStorageWithoutCreatingDatabase()
    {
        var token = TestContext.Current.CancellationToken;
        var home = CreateHome();
        try
        {
            var result = await RunCliAsync(home, token, "doctor", "--json");

            Assert.Equal(1, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            Assert.False(root.GetProperty("databaseExists").GetBoolean());
            Assert.False(root.GetProperty("healthy").GetBoolean());
            Assert.Equal("not_initialized", root.GetProperty("migrationState").GetString());
            Assert.Equal(JsonValueKind.Null, root.GetProperty("sqliteVersion").ValueKind);
            Assert.False(File.Exists(Path.Combine(home, "hero-passport.db")));
        }
        finally
        {
            DeleteHome(home);
        }
    }

    [Fact]
    public async Task DoctorJsonReportsQualifiedHealthyDatabase()
    {
        var token = TestContext.Current.CancellationToken;
        var home = CreateHome();
        try
        {
            var initialized = await RunCliAsync(
                home,
                token,
                "init",
                "--locale", "en-US",
                "--hero-name", "Doctor Hero",
                "--json");
            Assert.Equal(0, initialized.ExitCode);

            var result = await RunCliAsync(home, token, "doctor", "--json");

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            Assert.True(root.GetProperty("databaseExists").GetBoolean());
            Assert.True(root.GetProperty("healthy").GetBoolean());
            Assert.True(root.GetProperty("sqliteVersionSupported").GetBoolean());
            Assert.Equal("wal", root.GetProperty("journalMode").GetString());
            Assert.Equal(2, root.GetProperty("synchronous").GetInt32());
            Assert.True(root.GetProperty("foreignKeys").GetBoolean());
            Assert.False(root.GetProperty("trustedSchema").GetBoolean());
            Assert.Equal("current", root.GetProperty("migrationState").GetString());
            Assert.False(root.GetProperty("migrationLockSuspected").GetBoolean());
            Assert.True(root.GetProperty("quickCheckPassed").GetBoolean());
            Assert.Equal(0, root.GetProperty("foreignKeyViolationCount").GetInt32());
        }
        finally
        {
            DeleteHome(home);
        }
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
        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(dotnetRoot))
        {
            startInfo.Environment["DOTNET_ROOT"] = dotnetRoot;
        }

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

    private static string CreateHome()
    {
        var path = Path.Combine(Path.GetTempPath(), "HeroPassport.Cli.Doctor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteHome(string home)
    {
        try
        {
            Directory.Delete(home, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);
}
