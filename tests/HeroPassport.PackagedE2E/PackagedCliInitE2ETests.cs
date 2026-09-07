using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace HeroPassport.PackagedE2E;

public sealed class PackagedCliInitE2ETests
{
    [Fact]
    public async Task PublishedCliInitBootstrapsAndReplaysWithStableRequestIdentity()
    {
        var token = TestContext.Current.CancellationToken;
        var publishDirectory = Environment.GetEnvironmentVariable("HERO_PASSPORT_PUBLISH_DIR");
        Assert.False(string.IsNullOrWhiteSpace(publishDirectory));
        publishDirectory = Path.GetFullPath(publishDirectory!);
        var appDll = Path.Combine(publishDirectory, "HeroPassport.App.dll");
        Assert.True(File.Exists(appDll), $"Published Hero Passport app was not found at {appDll}.");

        var home = Path.Combine(Path.GetTempPath(), "HeroPassport.PackagedCli", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(home);
        try
        {
            var requestId = Guid.CreateVersion7().ToString("D");
            var args = new[]
            {
                "init",
                "--locale", "en-US",
                "--hero-name", "Packaged CLI Nova",
                "--request-id", requestId,
                "--json",
            };

            var first = await RunCliAsync(appDll, publishDirectory, home, token, args);
            Assert.Equal(0, first.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(first.StandardError), first.StandardError);
            using var firstDocument = JsonDocument.Parse(first.StandardOutput);
            var firstRoot = firstDocument.RootElement;
            Assert.Equal(requestId, firstRoot.GetProperty("bootstrapRequestId").GetString());
            Assert.False(firstRoot.GetProperty("replayed").GetBoolean());
            var heroId = firstRoot.GetProperty("hero").GetProperty("heroId").GetString();
            Assert.NotNull(heroId);
            Assert.True(Guid.TryParse(heroId, out var parsedHeroId));
            Assert.Equal(7, parsedHeroId.Version);
            var settings = firstRoot.GetProperty("settings");
            Assert.Equal("rpg_engineering", settings.GetProperty("presentationStyle").GetString());
            Assert.True(settings.GetProperty("autoStartQuest").GetBoolean());
            Assert.True(settings.GetProperty("autoFinishQuest").GetBoolean());

            var replay = await RunCliAsync(appDll, publishDirectory, home, token, args);
            Assert.Equal(0, replay.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(replay.StandardError), replay.StandardError);
            using var replayDocument = JsonDocument.Parse(replay.StandardOutput);
            var replayRoot = replayDocument.RootElement;
            Assert.True(replayRoot.GetProperty("replayed").GetBoolean());
            Assert.Equal(heroId, replayRoot.GetProperty("hero").GetProperty("heroId").GetString());
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

    private static async Task<CliResult> RunCliAsync(
        string appDll,
        string workingDirectory,
        string home,
        CancellationToken token,
        params string[] args)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
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

        using var process = Process.Start(startInfo) ?? throw new Xunit.Sdk.XunitException("Published Hero Passport CLI did not start.");
        process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEndAsync(token);
        var stderr = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        return new CliResult(process.ExitCode, await stdout, await stderr);
    }

    private sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);
}
