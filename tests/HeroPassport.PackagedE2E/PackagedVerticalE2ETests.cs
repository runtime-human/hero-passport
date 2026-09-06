using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace HeroPassport.PackagedE2E;

public sealed class PackagedVerticalE2ETests
{
    private static readonly string[] CodingSkills = ["coding"];
    private static readonly string[] SkillReferenceFiles =
    [
        "lifecycle.md",
        "finish-attestations.md",
        "recovery.md",
        "presentation.md",
    ];

    [Fact]
    public async Task PublishedAppAndSkillSurviveRestartAndRetryBoundaries()
    {
        var token = TestContext.Current.CancellationToken;
        var publishDirectory = Environment.GetEnvironmentVariable("HERO_PASSPORT_PUBLISH_DIR");
        Assert.False(string.IsNullOrWhiteSpace(publishDirectory));
        publishDirectory = Path.GetFullPath(publishDirectory!);
        var appDll = Path.Combine(publishDirectory, "HeroPassport.App.dll");
        Assert.True(File.Exists(appDll), $"Published Hero Passport app was not found at {appDll}.");

        var root = Path.Combine(Path.GetTempPath(), "HeroPassport.PackagedE2E", Guid.NewGuid().ToString("N"));
        var home = Path.Combine(root, "home");
        var repository = Path.Combine(root, "repo");
        Directory.CreateDirectory(home);
        Directory.CreateDirectory(repository);

        try
        {
            await InitGitRepositoryAsync(repository, token);

            var bootstrapRequestId = RequestId();
            var startRequestId = RequestId();
            var finishRequestId = RequestId();
            string heroId;
            string questId;

            await using (var client = await CreateClientAsync(appDll, home, repository, token))
            {
                var beforeSetup = Structured(await client.CallToolAsync("hero.get_context", cancellationToken: token));
                Assert.False(beforeSetup.GetProperty("setupCompleted").GetBoolean());

                var bootstrap = Structured(await client.CallToolAsync(
                    "hero.bootstrap",
                    new Dictionary<string, object?>
                    {
                        ["bootstrapRequestId"] = bootstrapRequestId,
                        ["locale"] = "en-US",
                        ["heroName"] = "Packaged Nova",
                        ["presentationStyle"] = "rpg_engineering",
                        ["autoStartQuest"] = true,
                        ["autoFinishQuest"] = true,
                    },
                    cancellationToken: token));
                heroId = bootstrap.GetProperty("hero").GetProperty("heroId").GetString()!;

                var start = Structured(await client.CallToolAsync(
                    "hero.start_quest",
                    StartArguments(startRequestId, heroId),
                    cancellationToken: token));
                questId = start.GetProperty("quest").GetProperty("questId").GetString()!;
                Assert.False(start.GetProperty("replayed").GetBoolean());

                var finish = Structured(await client.CallToolAsync(
                    "hero.finish_quest",
                    FinishArguments(finishRequestId, questId),
                    cancellationToken: token));
                Assert.False(finish.GetProperty("replayed").GetBoolean());
                Assert.False(finish.GetProperty("alreadyFinalized").GetBoolean());
                Assert.Equal(60, finish.GetProperty("reward").GetProperty("xpGained").GetInt64());
            }

            await using (var restarted = await CreateClientAsync(appDll, home, repository, token))
            {
                var context = Structured(await restarted.CallToolAsync("hero.get_context", cancellationToken: token));
                Assert.True(context.GetProperty("setupCompleted").GetBoolean());
                Assert.Equal(heroId, context.GetProperty("activeHero").GetProperty("heroId").GetString());
                Assert.Empty(context.GetProperty("openQuests").EnumerateArray());

                var card = Structured(await restarted.CallToolAsync(
                    "hero.get_card",
                    new Dictionary<string, object?> { ["heroId"] = heroId },
                    cancellationToken: token));
                Assert.Equal(60, card.GetProperty("hero").GetProperty("totalXp").GetInt64());

                var startReplay = Structured(await restarted.CallToolAsync(
                    "hero.start_quest",
                    StartArguments(startRequestId, heroId),
                    cancellationToken: token));
                Assert.True(startReplay.GetProperty("replayed").GetBoolean());
                Assert.Equal(questId, startReplay.GetProperty("quest").GetProperty("questId").GetString());

                var finishReplay = Structured(await restarted.CallToolAsync(
                    "hero.finish_quest",
                    FinishArguments(finishRequestId, questId),
                    cancellationToken: token));
                Assert.True(finishReplay.GetProperty("replayed").GetBoolean());
                Assert.Equal(60, finishReplay.GetProperty("reward").GetProperty("xpGained").GetInt64());

                var changedStart = await restarted.CallToolAsync(
                    "hero.start_quest",
                    StartArguments(startRequestId, heroId, goal: "Changed canonical intent must conflict after restart."),
                    cancellationToken: token);
                AssertToolError(changedStart, "HP135");

                var changedSameFinishRequest = await restarted.CallToolAsync(
                    "hero.finish_quest",
                    FinishArguments(finishRequestId, questId, summary: "Changed same-request payload must conflict."),
                    cancellationToken: token);
                AssertToolError(changedSameFinishRequest, "HP135");

                var conflictingFinalization = await restarted.CallToolAsync(
                    "hero.finish_quest",
                    FinishArguments(RequestId(), questId, summary: "Fresh request with different finalization must not overwrite history."),
                    cancellationToken: token);
                AssertToolError(conflictingFinalization, "HP136");
            }

            AssertPackagedSkill(publishDirectory);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    private static async Task<McpClient> CreateClientAsync(
        string appDll,
        string home,
        string repository,
        CancellationToken token)
    {
        var environment = StdioClientTransportOptions.GetDefaultEnvironmentVariables();
        environment["HERO_PASSPORT_HOME"] = home;
        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(dotnetRoot))
        {
            environment["DOTNET_ROOT"] = dotnetRoot;
        }

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "hero-passport-packaged-e2e",
            Command = "dotnet",
            Arguments = [appDll, "mcp", "--project-root", repository],
            WorkingDirectory = repository,
            InheritEnvironmentVariables = false,
            EnvironmentVariables = environment,
            ShutdownTimeout = TimeSpan.FromSeconds(5),
        });

        return await McpClient.CreateAsync(
            transport,
            new McpClientOptions
            {
                ProtocolVersion = "2026-07-28",
                InitializationTimeout = TimeSpan.FromSeconds(15),
                DiscoverProbeTimeout = TimeSpan.FromSeconds(5),
            },
            cancellationToken: token);
    }

    private static Dictionary<string, object?> StartArguments(
        string requestId,
        string heroId,
        string goal = "Qualify the published Hero Passport lifecycle across a real MCP process restart.") =>
        new()
        {
            ["startRequestId"] = requestId,
            ["heroId"] = heroId,
            ["questType"] = "coding",
            ["title"] = "Packaged vertical checkpoint",
            ["goal"] = goal,
        };

    private static Dictionary<string, object?> FinishArguments(
        string requestId,
        string questId,
        string summary = "Published Hero Passport completed the minimal vertical lifecycle before process restart.") =>
        new()
        {
            ["finishRequestId"] = requestId,
            ["questId"] = questId,
            ["result"] = "success",
            ["summary"] = summary,
            ["metrics"] = new Dictionary<string, object?>
            {
                ["testsMentioned"] = false,
                ["scopeViolations"] = 0,
                ["userCorrections"] = 0,
                ["buildStatus"] = "not_run",
                ["buildEvidence"] = "none",
                ["testsStatus"] = "not_run",
                ["testsEvidence"] = "none",
            },
            ["skillsUsed"] = CodingSkills,
        };

    private static JsonElement Structured(CallToolResult result)
    {
        Assert.NotEqual(true, result.IsError);
        return Assert.IsType<JsonElement>(result.StructuredContent);
    }

    private static void AssertToolError(CallToolResult result, string code)
    {
        Assert.True(result.IsError);
        Assert.Null(result.StructuredContent);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains(code, text.Text, StringComparison.Ordinal);
    }

    private static void AssertPackagedSkill(string publishDirectory)
    {
        var skillDirectory = Path.Combine(publishDirectory, "skills", "hero-passport");
        var skill = Path.Combine(skillDirectory, "SKILL.md");
        Assert.True(File.Exists(skill), $"Packaged Agent Skill was not found at {skill}.");
        Assert.Contains("hero-passport-skill/1", File.ReadAllText(skill), StringComparison.Ordinal);

        foreach (var reference in SkillReferenceFiles)
        {
            Assert.True(
                File.Exists(Path.Combine(skillDirectory, "references", reference)),
                $"Packaged Agent Skill reference {reference} is missing.");
        }
    }

    private static async Task InitGitRepositoryAsync(string path, CancellationToken token)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = path,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("init");
        start.ArgumentList.Add("--quiet");

        using var process = Process.Start(start) ?? throw new Xunit.Sdk.XunitException("git init could not be started.");
        await process.WaitForExitAsync(token);
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(token);
            throw new Xunit.Sdk.XunitException($"git init failed with exit {process.ExitCode}: {error}");
        }
    }

    private static string RequestId() => Guid.CreateVersion7().ToString("D");
}
