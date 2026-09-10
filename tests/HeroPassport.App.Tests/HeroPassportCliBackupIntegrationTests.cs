using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Xunit;

namespace HeroPassport.App.Tests;

public sealed class HeroPassportCliBackupIntegrationTests
{
    [Fact]
    public async Task BackupPublishesValidatedSnapshotAndBoundedJsonResult()
    {
        var token = TestContext.Current.CancellationToken;
        var home = CreateHome();
        try
        {
            var databasePath = Path.Combine(home, "hero-passport.db");
            var backupPath = Path.Combine(home, "backups", "qualified.db");
            await HeroPassportDatabase.InitializeAsync(databasePath, token);
            var app = new HeroPassportApplication(new SqliteHeroPassportStateStore(databasePath), TimeProvider.System);
            var hero = (await app.BootstrapAsync(
                new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true),
                token)).Hero;
            var project = new ProjectBindingContext("CLI Backup", new string('e', 64), "project-identity/1");
            var quest = (await app.StartQuestAsync(
                new StartQuestRequest(
                    MutationRequestId.New(), hero.HeroId, "coding",
                    "Qualify CLI backup",
                    "Create canonical state that must survive the explicit validated backup command."),
                project,
                token)).Quest;
            await app.FinishQuestAsync(
                new FinishQuestRequest(
                    MutationRequestId.New(),
                    quest.QuestId,
                    "success",
                    "Completed CLI backup qualification with clean scope and observed tests.",
                    new FinishQuestMetrics(true, 0, 0, "not_run", "none", "passed", "observed"),
                    ["coding"]),
                project,
                token);

            var result = await RunCliAsync(home, token, "backup", "--output", backupPath, "--json");

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
            using var json = JsonDocument.Parse(result.StandardOutput);
            var root = json.RootElement;
            Assert.True(root.GetProperty("validated").GetBoolean());
            Assert.Equal(Path.GetFullPath(backupPath), root.GetProperty("destinationPath").GetString());
            Assert.True(root.GetProperty("sizeBytes").GetInt64() > 0);
            Assert.Equal("current", root.GetProperty("migrationState").GetString());
            Assert.Equal(4, root.EnumerateObject().Count());
            Assert.True(File.Exists(backupPath));

            await using var backup = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = backupPath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
            }.ToString());
            await backup.OpenAsync(token);
            await using (var command = backup.CreateCommand())
            {
                command.CommandText = "SELECT total_xp FROM heroes WHERE id=$id;";
                command.Parameters.AddWithValue("$id", hero.HeroId.ToString());
                Assert.Equal(95L, Convert.ToInt64(
                    await command.ExecuteScalarAsync(token),
                    CultureInfo.InvariantCulture));
            }

            await using (var command = backup.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM quest_reports;";
                Assert.Equal(1L, Convert.ToInt64(
                    await command.ExecuteScalarAsync(token),
                    CultureInfo.InvariantCulture));
            }
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

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Hero Passport CLI process did not start.");
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
        var path = Path.Combine(Path.GetTempPath(), "HeroPassport.Cli.Backup.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteHome(string home)
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(home, recursive: true); } catch (DirectoryNotFoundException) { } catch (IOException) { }
    }

    private sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);
}
