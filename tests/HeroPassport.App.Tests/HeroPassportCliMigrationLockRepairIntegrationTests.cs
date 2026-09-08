using HeroPassport.Infrastructure.Persistence;
using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace HeroPassport.App.Tests;

public sealed class HeroPassportCliMigrationLockRepairIntegrationTests
{
    [Fact]
    public async Task MigrationLockRepairRequiresExplicitStoppedProcessesConfirmationAndPublishesBoundedResult()
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
                "--hero-name", "Repair Hero",
                "--json");
            Assert.Equal(0, initialized.ExitCode);

            var databasePath = Path.Combine(home, "hero-passport.db");
            await SeedMigrationLockAsync(databasePath, token);
            Assert.True((await HeroPassportDatabaseDoctor.InspectAsync(databasePath, token)).MigrationLockSuspected);

            var rejected = await RunCliAsync(
                home,
                token,
                "repair", "migration-lock",
                "--json");
            Assert.NotEqual(0, rejected.ExitCode);
            Assert.Contains("confirm-processes-stopped", rejected.StandardError, StringComparison.OrdinalIgnoreCase);
            Assert.True((await HeroPassportDatabaseDoctor.InspectAsync(databasePath, token)).MigrationLockSuspected);

            var repaired = await RunCliAsync(
                home,
                token,
                "repair", "migration-lock",
                "--confirm-processes-stopped",
                "--json");
            Assert.Equal(0, repaired.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(repaired.StandardError), repaired.StandardError);
            using var document = JsonDocument.Parse(repaired.StandardOutput);
            var root = document.RootElement;
            Assert.True(root.GetProperty("lockCleared").GetBoolean());
            Assert.True(root.GetProperty("beforeMigrationLockSuspected").GetBoolean());
            Assert.False(root.GetProperty("afterMigrationLockSuspected").GetBoolean());
            Assert.Equal("current", root.GetProperty("migrationState").GetString());
            Assert.True(root.GetProperty("quickCheckPassed").GetBoolean());
            Assert.Equal(0, root.GetProperty("foreignKeyViolationCount").GetInt32());
            Assert.True(root.GetProperty("healthy").GetBoolean());
        }
        finally
        {
            DeleteHome(home);
        }
    }

    private static async Task SeedMigrationLockAsync(string databasePath, CancellationToken token)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(databasePath, token);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsLock" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK___EFMigrationsLock" PRIMARY KEY,
                "Timestamp" TEXT NOT NULL
            );
            DELETE FROM "__EFMigrationsLock";
            INSERT INTO "__EFMigrationsLock" ("Id", "Timestamp") VALUES (1, '2026-09-08 00:00:00+00:00');
            """;
        await command.ExecuteNonQueryAsync(token);
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
        var path = Path.Combine(Path.GetTempPath(), "HeroPassport.Cli.MigrationRepair.Tests", Guid.NewGuid().ToString("N"));
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
