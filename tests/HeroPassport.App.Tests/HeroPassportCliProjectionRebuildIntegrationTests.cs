using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace HeroPassport.App.Tests;

public sealed class HeroPassportCliProjectionRebuildIntegrationTests
{
    [Fact]
    public async Task ProjectionRebuildPublishesBoundedResultAndRestoresCorruptedProjections()
    {
        var token = TestContext.Current.CancellationToken;
        var home = CreateHome();
        try
        {
            var databasePath = Path.Combine(home, "hero-passport.db");
            await HeroPassportDatabase.InitializeAsync(databasePath, token);
            var app = new HeroPassportApplication(new SqliteHeroPassportStateStore(databasePath), TimeProvider.System);
            var hero = (await app.BootstrapAsync(
                new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true),
                token)).Hero;
            var project = new ProjectBindingContext("CLI Rebuild", new string('c', 64), "project-identity/1");
            var quest = (await app.StartQuestAsync(
                new StartQuestRequest(
                    MutationRequestId.New(), hero.HeroId, "coding",
                    "Qualify CLI rebuild",
                    "Create canonical history for the explicit projection rebuild CLI command."),
                project,
                token)).Quest;
            await app.FinishQuestAsync(
                new FinishQuestRequest(
                    MutationRequestId.New(),
                    quest.QuestId,
                    "success",
                    "Completed CLI projection rebuild qualification with clean scope and observed tests.",
                    new FinishQuestMetrics(true, 0, 0, "not_run", "none", "passed", "observed"),
                    ["coding"]),
                project,
                token);

            var before = await app.GetCardAsync(hero.HeroId, project, token);
            await using (var connection = await HeroPassportDatabase.OpenConnectionAsync(databasePath, token))
            await using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    """
                    UPDATE heroes SET total_xp=0,trust=1,strain=99,success_streak=55;
                    DELETE FROM hero_skills;
                    DELETE FROM hero_project_stats;
                    """;
                await command.ExecuteNonQueryAsync(token);
            }

            var result = await RunCliAsync(home, token, "rebuild", "projections", "--json");

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
            using var json = JsonDocument.Parse(result.StandardOutput);
            var root = json.RootElement;
            Assert.Equal(1, root.GetProperty("heroesRebuilt").GetInt32());
            Assert.Equal(1, root.GetProperty("heroSkillsRebuilt").GetInt32());
            Assert.Equal(1, root.GetProperty("heroProjectStatsRebuilt").GetInt32());
            Assert.True(root.GetProperty("healthy").GetBoolean());
            Assert.Equal(4, root.EnumerateObject().Count());

            Assert.Equal(before, await app.GetCardAsync(hero.HeroId, project, token));
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
        var path = Path.Combine(Path.GetTempPath(), "HeroPassport.Cli.ProjectionRebuild.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteHome(string home)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(home, recursive: true); } catch (DirectoryNotFoundException) { } catch (IOException) { }
    }

    private sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);
}
