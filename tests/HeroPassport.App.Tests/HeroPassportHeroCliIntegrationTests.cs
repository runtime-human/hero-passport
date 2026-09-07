using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace HeroPassport.App.Tests;

public sealed class HeroPassportHeroCliIntegrationTests
{
    [Fact]
    public async Task HeroAdministrationCommandsExposeDeterministicJsonLifecycleAndCreateReplay()
    {
        var token = TestContext.Current.CancellationToken;
        var home = CreateHome();
        try
        {
            var initial = await RunCliAsync(home, token, "init", "--locale", "en-US", "--hero-name", "Initial Hero", "--json");
            Assert.Equal(0, initial.ExitCode);
            using var initialDocument = JsonDocument.Parse(initial.StandardOutput);
            var initialHeroId = initialDocument.RootElement.GetProperty("hero").GetProperty("heroId").GetString()!;

            var createRequestId = Guid.CreateVersion7().ToString("D");
            var createArgs = new[] { "hero", "create", "--name", "CLI Echo", "--request-id", createRequestId, "--json" };
            var created = await RunCliAsync(home, token, createArgs);
            Assert.Equal(0, created.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(created.StandardError), created.StandardError);
            using var createdDocument = JsonDocument.Parse(created.StandardOutput);
            var createdRoot = createdDocument.RootElement;
            Assert.Equal(createRequestId, createdRoot.GetProperty("createRequestId").GetString());
            Assert.False(createdRoot.GetProperty("replayed").GetBoolean());
            var secondHeroId = createdRoot.GetProperty("hero").GetProperty("heroId").GetString()!;
            Assert.Equal("CLI Echo", createdRoot.GetProperty("hero").GetProperty("name").GetString());

            var replay = await RunCliAsync(home, token, createArgs);
            Assert.Equal(0, replay.ExitCode);
            using var replayDocument = JsonDocument.Parse(replay.StandardOutput);
            Assert.True(replayDocument.RootElement.GetProperty("replayed").GetBoolean());
            Assert.Equal(secondHeroId, replayDocument.RootElement.GetProperty("hero").GetProperty("heroId").GetString());

            var listed = await RunCliAsync(home, token, "hero", "list", "--json");
            Assert.Equal(0, listed.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(listed.StandardError), listed.StandardError);
            using var listedDocument = JsonDocument.Parse(listed.StandardOutput);
            var heroes = listedDocument.RootElement.GetProperty("heroes").EnumerateArray().ToArray();
            Assert.Equal(2, heroes.Length);
            AssertHero(heroes[0], initialHeroId, "Initial Hero", archived: false, active: true);
            AssertHero(heroes[1], secondHeroId, "CLI Echo", archived: false, active: false);

            var activated = await RunCliAsync(home, token, "hero", "activate", "--hero-id", secondHeroId, "--json");
            Assert.Equal(0, activated.ExitCode);
            using var activatedDocument = JsonDocument.Parse(activated.StandardOutput);
            Assert.True(activatedDocument.RootElement.GetProperty("changed").GetBoolean());
            AssertHero(activatedDocument.RootElement.GetProperty("hero"), secondHeroId, "CLI Echo", archived: false, active: true);

            var activationReplay = await RunCliAsync(home, token, "hero", "activate", "--hero-id", secondHeroId, "--json");
            Assert.Equal(0, activationReplay.ExitCode);
            using var activationReplayDocument = JsonDocument.Parse(activationReplay.StandardOutput);
            Assert.False(activationReplayDocument.RootElement.GetProperty("changed").GetBoolean());

            var archived = await RunCliAsync(home, token, "hero", "archive", "--hero-id", initialHeroId, "--json");
            Assert.Equal(0, archived.ExitCode);
            using var archivedDocument = JsonDocument.Parse(archived.StandardOutput);
            Assert.True(archivedDocument.RootElement.GetProperty("changed").GetBoolean());
            AssertHero(archivedDocument.RootElement.GetProperty("hero"), initialHeroId, "Initial Hero", archived: true, active: false);

            var archiveReplay = await RunCliAsync(home, token, "hero", "archive", "--hero-id", initialHeroId, "--json");
            Assert.Equal(0, archiveReplay.ExitCode);
            using var archiveReplayDocument = JsonDocument.Parse(archiveReplay.StandardOutput);
            Assert.False(archiveReplayDocument.RootElement.GetProperty("changed").GetBoolean());

            var restored = await RunCliAsync(home, token, "hero", "restore", "--hero-id", initialHeroId, "--json");
            Assert.Equal(0, restored.ExitCode);
            using var restoredDocument = JsonDocument.Parse(restored.StandardOutput);
            Assert.True(restoredDocument.RootElement.GetProperty("changed").GetBoolean());
            AssertHero(restoredDocument.RootElement.GetProperty("hero"), initialHeroId, "Initial Hero", archived: false, active: false);

            var restoreReplay = await RunCliAsync(home, token, "hero", "restore", "--hero-id", initialHeroId, "--json");
            Assert.Equal(0, restoreReplay.ExitCode);
            using var restoreReplayDocument = JsonDocument.Parse(restoreReplay.StandardOutput);
            Assert.False(restoreReplayDocument.RootElement.GetProperty("changed").GetBoolean());
        }
        finally
        {
            DeleteHome(home);
        }
    }

    [Fact]
    public async Task HeroAdministrationPreservesSafeErrorsAndDoesNotMutateOnRejectedCommands()
    {
        var token = TestContext.Current.CancellationToken;
        var home = CreateHome();
        try
        {
            var init = await RunCliAsync(home, token, "init", "--locale", "en-US", "--hero-name", "Protected Hero", "--json");
            Assert.Equal(0, init.ExitCode);
            using var initDocument = JsonDocument.Parse(init.StandardOutput);
            var activeHeroId = initDocument.RootElement.GetProperty("hero").GetProperty("heroId").GetString()!;

            var activeArchive = await RunCliAsync(home, token, "hero", "archive", "--hero-id", activeHeroId, "--json");
            Assert.Equal(2, activeArchive.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(activeArchive.StandardOutput), activeArchive.StandardOutput);
            Assert.Contains("HP145", activeArchive.StandardError, StringComparison.Ordinal);
            Assert.DoesNotContain("Exception", activeArchive.StandardError, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(home, activeArchive.StandardError, StringComparison.Ordinal);

            var requestId = Guid.CreateVersion7().ToString("D");
            var firstCreate = await RunCliAsync(home, token, "hero", "create", "--name", "Stable Intent", "--request-id", requestId, "--json");
            Assert.Equal(0, firstCreate.ExitCode);

            var changedCreate = await RunCliAsync(home, token, "hero", "create", "--name", "Changed Intent", "--request-id", requestId, "--json");
            Assert.Equal(2, changedCreate.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(changedCreate.StandardOutput), changedCreate.StandardOutput);
            Assert.Contains("HP135", changedCreate.StandardError, StringComparison.Ordinal);

            var malformed = await RunCliAsync(home, token, "hero", "activate", "--hero-id", "not-a-uuid", "--json");
            Assert.Equal(2, malformed.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(malformed.StandardOutput), malformed.StandardOutput);
            Assert.Contains("HP300", malformed.StandardError, StringComparison.Ordinal);
            Assert.DoesNotContain("Exception", malformed.StandardError, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteHome(home);
        }
    }

    [Fact]
    public async Task RootAndHeroHelpPublishAdministrationCommandsWithoutInitializingStorage()
    {
        var token = TestContext.Current.CancellationToken;
        var home = CreateHome();
        try
        {
            var root = await RunCliAsync(home, token, "--help");
            Assert.Equal(0, root.ExitCode);
            Assert.Contains("hero", root.StandardOutput, StringComparison.Ordinal);

            var hero = await RunCliAsync(home, token, "hero", "--help");
            Assert.Equal(0, hero.ExitCode);
            foreach (var command in new[] { "create", "list", "activate", "archive", "restore" })
            {
                Assert.Contains(command, hero.StandardOutput, StringComparison.Ordinal);
            }

            Assert.False(File.Exists(Path.Combine(home, "hero-passport.db")));
        }
        finally
        {
            DeleteHome(home);
        }
    }

    private static void AssertHero(JsonElement hero, string heroId, string name, bool archived, bool active)
    {
        Assert.Equal(heroId, hero.GetProperty("heroId").GetString());
        Assert.Equal(name, hero.GetProperty("name").GetString());
        Assert.Equal(archived, hero.GetProperty("archived").GetBoolean());
        Assert.Equal(active, hero.GetProperty("active").GetBoolean());
        Assert.Equal(0, hero.GetProperty("totalXp").GetInt64());
        Assert.Equal(1, hero.GetProperty("level").GetInt32());
        Assert.Equal("code_squire", hero.GetProperty("rankKey").GetString());
        Assert.Equal(50, hero.GetProperty("trust").GetInt32());
        Assert.Equal(20, hero.GetProperty("strain").GetInt32());
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
            CreateNoWindow = true,
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

        using var process = Process.Start(startInfo) ?? throw new Xunit.Sdk.XunitException("Hero Passport CLI process did not start.");
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
        var path = Path.Combine(Path.GetTempPath(), "HeroPassport.HeroCli.Tests", Guid.NewGuid().ToString("N"));
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
