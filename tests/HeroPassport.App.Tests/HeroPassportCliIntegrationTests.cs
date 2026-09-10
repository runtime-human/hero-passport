using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace HeroPassport.App.Tests;

public sealed class HeroPassportCliIntegrationTests
{
    [Fact]
    public async Task InitJsonBootstrapsAndReplaysStableRequestWithCanonicalDefaults()
    {
        var token = TestContext.Current.CancellationToken;
        var home = CreateHome();
        try
        {
            var requestId = Guid.CreateVersion7().ToString("D");
            var args = new[]
            {
                "init",
                "--locale", "en-US",
                "--hero-name", "CLI Nova",
                "--request-id", requestId,
                "--json",
            };

            var first = await RunCliAsync(home, token, args);
            Assert.Equal(0, first.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(first.StandardError), first.StandardError);
            using var firstDocument = JsonDocument.Parse(first.StandardOutput);
            var firstRoot = firstDocument.RootElement;
            Assert.False(firstRoot.GetProperty("replayed").GetBoolean());
            var firstHero = firstRoot.GetProperty("hero");
            Assert.Equal("CLI Nova", firstHero.GetProperty("name").GetString());
            var heroId = firstHero.GetProperty("heroId").GetString();
            Assert.NotNull(heroId);
            Assert.True(Guid.TryParse(heroId, out var parsedHeroId));
            Assert.Equal(7, parsedHeroId.Version);
            var settings = firstRoot.GetProperty("settings");
            Assert.Equal("en-US", settings.GetProperty("locale").GetString());
            Assert.Equal("rpg_engineering", settings.GetProperty("presentationStyle").GetString());
            Assert.True(settings.GetProperty("autoStartQuest").GetBoolean());
            Assert.True(settings.GetProperty("autoFinishQuest").GetBoolean());

            var replay = await RunCliAsync(home, token, args);
            Assert.Equal(0, replay.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(replay.StandardError), replay.StandardError);
            using var replayDocument = JsonDocument.Parse(replay.StandardOutput);
            var replayRoot = replayDocument.RootElement;
            Assert.True(replayRoot.GetProperty("replayed").GetBoolean());
            Assert.Equal(heroId, replayRoot.GetProperty("hero").GetProperty("heroId").GetString());
        }
        finally
        {
            DeleteHome(home);
        }
    }

    [Fact]
    public async Task InitAcceptsExplicitPresentationAndDisabledAutomationPreferences()
    {
        var token = TestContext.Current.CancellationToken;
        var home = CreateHome();
        try
        {
            var result = await RunCliAsync(
                home,
                token,
                "init",
                "--locale", "ru-RU",
                "--hero-name", "Кли Герой",
                "--presentation-style", "minimal",
                "--auto-start-quest", "false",
                "--auto-finish-quest", "false",
                "--json");

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var settings = document.RootElement.GetProperty("settings");
            Assert.Equal("ru-RU", settings.GetProperty("locale").GetString());
            Assert.Equal("minimal", settings.GetProperty("presentationStyle").GetString());
            Assert.False(settings.GetProperty("autoStartQuest").GetBoolean());
            Assert.False(settings.GetProperty("autoFinishQuest").GetBoolean());
        }
        finally
        {
            DeleteHome(home);
        }
    }

    [Fact]
    public async Task MissingRequiredInitOptionFailsBeforeDatabaseInitialization()
    {
        var token = TestContext.Current.CancellationToken;
        var home = CreateHome();
        try
        {
            var result = await RunCliAsync(home, token, "init", "--locale", "en-US", "--json");

            Assert.NotEqual(0, result.ExitCode);
            Assert.False(File.Exists(Path.Combine(home, "hero-passport.db")));
            Assert.True(string.IsNullOrWhiteSpace(result.StandardOutput), result.StandardOutput);
            Assert.False(string.IsNullOrWhiteSpace(result.StandardError));
            Assert.DoesNotContain("Exception", result.StandardError, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteHome(home);
        }
    }

    [Fact]
    public async Task MalformedRequestIdReturnsSafeErrorBeforeDatabaseInitialization()
    {
        var token = TestContext.Current.CancellationToken;
        var home = CreateHome();
        try
        {
            var result = await RunCliAsync(
                home,
                token,
                "init",
                "--locale", "en-US",
                "--hero-name", "CLI Nova",
                "--request-id", "not-a-uuid",
                "--json");

            Assert.Equal(2, result.ExitCode);
            Assert.False(File.Exists(Path.Combine(home, "hero-passport.db")));
            Assert.True(string.IsNullOrWhiteSpace(result.StandardOutput), result.StandardOutput);
            Assert.Contains("HP300", result.StandardError, StringComparison.Ordinal);
            Assert.DoesNotContain("Exception", result.StandardError, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(home, result.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            DeleteHome(home);
        }
    }

    [Fact]
    public async Task DataPathJsonReportsResolvedPathsWithoutInitializingStorage()
    {
        var token = TestContext.Current.CancellationToken;
        var home = Path.Combine(
            Path.GetTempPath(),
            "HeroPassport.Cli.DataPath.Tests",
            Guid.NewGuid().ToString("N"));

        Assert.False(Directory.Exists(home));
        var result = await RunCliAsync(home, token, "data-path", "--json");

        Assert.Equal(0, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
        using var document = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal(Path.GetFullPath(home), document.RootElement.GetProperty("dataRoot").GetString());
        Assert.Equal(
            Path.Combine(Path.GetFullPath(home), "hero-passport.db"),
            document.RootElement.GetProperty("databasePath").GetString());
        Assert.False(Directory.Exists(home));
        Assert.False(File.Exists(Path.Combine(home, "hero-passport.db")));
    }

    [Fact]
    public async Task RootHelpPublishesInitMcpAndDataPathCommands()
    {
        var token = TestContext.Current.CancellationToken;
        var home = CreateHome();
        try
        {
            var result = await RunCliAsync(home, token, "--help");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("init", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("mcp", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("data-path", result.StandardOutput, StringComparison.Ordinal);
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
        var path = Path.Combine(Path.GetTempPath(), "HeroPassport.Cli.Tests", Guid.NewGuid().ToString("N"));
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
