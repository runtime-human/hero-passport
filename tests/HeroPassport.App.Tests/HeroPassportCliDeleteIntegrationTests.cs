using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace HeroPassport.App.Tests;

public sealed class HeroPassportCliDeleteIntegrationTests
{
    [Fact]
    public async Task HeroDeleteRequiresExplicitConfirmationAndPublishesBoundedLogicalDeleteResult()
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
                "--hero-name", "Active",
                "--json");
            Assert.Equal(0, initialized.ExitCode);
            using var initializedJson = JsonDocument.Parse(initialized.StandardOutput);
            var activeHeroId = initializedJson.RootElement.GetProperty("hero").GetProperty("heroId").GetString()!;

            var databasePath = Path.Combine(home, "hero-passport.db");
            var application = new HeroPassportApplication(new SqliteHeroPassportStateStore(databasePath), TimeProvider.System);
            var disposable = await application.CreateHeroAsync(
                new CreateHeroRequest(MutationRequestId.New(), "Disposable"),
                token);
            var disposableHeroId = disposable.Hero.HeroId.ToString();

            var missingConfirmation = await RunCliAsync(
                home,
                token,
                "hero", "delete",
                "--hero-id", disposableHeroId,
                "--json");
            Assert.NotEqual(0, missingConfirmation.ExitCode);
            Assert.Contains("confirm-logical-delete", missingConfirmation.StandardError, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                (await application.ListHeroesAsync(token)).Heroes,
                hero => hero.HeroId == disposable.Hero.HeroId);

            var deleted = await RunCliAsync(
                home,
                token,
                "hero", "delete",
                "--hero-id", disposableHeroId,
                "--confirm-logical-delete",
                "--json");
            Assert.Equal(0, deleted.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(deleted.StandardError), deleted.StandardError);
            using var deletedJson = JsonDocument.Parse(deleted.StandardOutput);
            var root = deletedJson.RootElement;
            Assert.Equal(disposableHeroId, root.GetProperty("heroId").GetString());
            Assert.True(root.GetProperty("deleted").GetBoolean());
            Assert.Equal("logical_application_state", root.GetProperty("deletionScope").GetString());
            Assert.False(root.GetProperty("forensicErasure").GetBoolean());

            var activeDelete = await RunCliAsync(
                home,
                token,
                "hero", "delete",
                "--hero-id", activeHeroId,
                "--confirm-logical-delete",
                "--json");
            Assert.Equal(2, activeDelete.ExitCode);
            Assert.Contains("HP145", activeDelete.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            DeleteHome(home);
        }
    }

    [Fact]
    public async Task HeroDeleteHelpStatesLogicalDeletionIsNotForensicErasure()
    {
        var token = TestContext.Current.CancellationToken;
        var home = CreateHome();
        try
        {
            var result = await RunCliAsync(home, token, "hero", "delete", "--help");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("logical", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("forensic", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(home, "hero-passport.db")));
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
        var path = Path.Combine(Path.GetTempPath(), "HeroPassport.Cli.Delete.Tests", Guid.NewGuid().ToString("N"));
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
