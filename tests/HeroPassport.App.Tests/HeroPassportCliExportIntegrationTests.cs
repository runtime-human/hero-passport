using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace HeroPassport.App.Tests;

public sealed class HeroPassportCliExportIntegrationTests
{
    [Fact]
    public async Task ExportPublishesBoundedCommandResultAndUserFacingSnapshot()
    {
        var token = TestContext.Current.CancellationToken;
        var home = CreateHome();
        try
        {
            var databasePath = Path.Combine(home, "hero-passport.db");
            var exportPath = Path.Combine(home, "exports", "hero-passport.json");
            await HeroPassportDatabase.InitializeAsync(databasePath, token);
            var app = new HeroPassportApplication(new SqliteHeroPassportStateStore(databasePath), TimeProvider.System);
            var hero = (await app.BootstrapAsync(
                new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true),
                token)).Hero;
            var project = new ProjectBindingContext("CLI Export", new string('a', 64), "project-identity/1");
            var quest = (await app.StartQuestAsync(
                new StartQuestRequest(
                    MutationRequestId.New(), hero.HeroId, "coding",
                    "Qualify CLI export",
                    "Create user-facing RPG history for the explicit privacy-bounded export command."),
                project,
                token)).Quest;
            await app.FinishQuestAsync(
                new FinishQuestRequest(
                    MutationRequestId.New(),
                    quest.QuestId,
                    "success",
                    "Completed CLI export qualification with clean scope and observed tests.",
                    new FinishQuestMetrics(true, 0, 0, "not_run", "none", "passed", "observed"),
                    ["coding"]),
                project,
                token);

            var result = await RunCliAsync(home, token, "export", "--output", exportPath, "--json");

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
            using var commandJson = JsonDocument.Parse(result.StandardOutput);
            var commandRoot = commandJson.RootElement;
            Assert.Equal("hero-passport-export/1", commandRoot.GetProperty("schemaVersion").GetString());
            Assert.Equal(Path.GetFullPath(exportPath), commandRoot.GetProperty("destinationPath").GetString());
            Assert.True(commandRoot.GetProperty("sizeBytes").GetInt64() > 0);
            Assert.Equal(3, commandRoot.EnumerateObject().Count());

            Assert.True(File.Exists(exportPath));
            using var exportJson = JsonDocument.Parse(await File.ReadAllTextAsync(exportPath, token));
            var exportRoot = exportJson.RootElement;
            Assert.Equal("hero-passport-export/1", exportRoot.GetProperty("schemaVersion").GetString());
            Assert.Equal(hero.HeroId.ToString(), exportRoot.GetProperty("heroes")[0].GetProperty("heroId").GetString());
            Assert.Equal(95, exportRoot.GetProperty("heroes")[0].GetProperty("totalXp").GetInt64());
            Assert.Equal(quest.QuestId.ToString(), exportRoot.GetProperty("quests")[0].GetProperty("questId").GetString());
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
        var path = Path.Combine(Path.GetTempPath(), "HeroPassport.Cli.Export.Tests", Guid.NewGuid().ToString("N"));
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
