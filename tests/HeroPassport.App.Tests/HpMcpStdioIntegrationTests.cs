using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using Xunit;

namespace HeroPassport.App.Tests;

public sealed class HpMcpStdioIntegrationTests
{
    private static readonly string[] ExpectedToolNames =
    [
        "hero.bootstrap",
        "hero.configure",
        "hero.get_context",
        "hero.create",
        "hero.list",
        "hero.activate",
        "hero.archive",
        "hero.restore",
        "hero.start_quest",
        "hero.finish_quest",
        "hero.get_card",
    ];

    private static readonly string[] CodingSkills = ["coding"];

    private const string ExpectedInstructions =
        "Use the installed Hero Passport Agent Skill for ambient lifecycle policy.\n" +
        "Call hero.get_context to hydrate/recover uncertain state.\n" +
        "Pass explicit heroId when starting a Quest and carry returned questId.\n" +
        "Reuse mutation request IDs only for retries of the same canonical intent.\n" +
        "Never send source, diffs, raw logs, prompts, secrets, environment dumps or workspace paths.";

    [Theory]
    [InlineData("2026-07-28")]
    [InlineData("2025-11-25")]
    public async Task RealStdioServerQualifiesToolInventoryLifecycleAndInstructions(string protocolVersion)
    {
        var token = TestContext.Current.CancellationToken;
        var repoRoot = FindRepoRoot();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var appDll = Path.Combine(repoRoot, "src", "HeroPassport.App", "bin", configuration, "net10.0", "HeroPassport.App.dll");
        Assert.True(File.Exists(appDll), $"HeroPassport.App was not built at {appDll}.");

        var home = Path.Combine(Path.GetTempPath(), "HeroPassport.App.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(home);
        try
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
                Name = $"hero-passport-{protocolVersion}",
                Command = "dotnet",
                Arguments = [appDll, "mcp", "--project-root", repoRoot],
                WorkingDirectory = repoRoot,
                InheritEnvironmentVariables = false,
                EnvironmentVariables = environment,
                ShutdownTimeout = TimeSpan.FromSeconds(5),
            });
            var clientOptions = new McpClientOptions
            {
                ProtocolVersion = protocolVersion,
                InitializationTimeout = TimeSpan.FromSeconds(15),
                DiscoverProbeTimeout = TimeSpan.FromSeconds(5),
            };

            await using var client = await McpClient.CreateAsync(transport, clientOptions, cancellationToken: token);

            Assert.Equal(protocolVersion, client.NegotiatedProtocolVersion);
            Assert.Equal(ExpectedInstructions, client.ServerInstructions);

            var tools = await client.ListToolsAsync(cancellationToken: token);
            Assert.Equal(ExpectedToolNames, tools.Select(static tool => tool.Name).ToArray());
            Assert.All(tools, static tool => Assert.NotNull(tool.ProtocolTool.OutputSchema));

            var preSetupContext = await client.CallToolAsync("hero.get_context", cancellationToken: token);
            Assert.False(Structured(preSetupContext).GetProperty("setupCompleted").GetBoolean());

            var gatedList = await client.CallToolAsync("hero.list", cancellationToken: token);
            Assert.True(gatedList.IsError);
            Assert.Null(gatedList.StructuredContent);
            Assert.Contains("HP001", Assert.IsType<TextContentBlock>(Assert.Single(gatedList.Content)).Text, StringComparison.Ordinal);

            var bootstrap = await client.CallToolAsync(
                "hero.bootstrap",
                new Dictionary<string, object?>
                {
                    ["bootstrapRequestId"] = RequestId(),
                    ["locale"] = "en-US",
                    ["heroName"] = "Stdio Nova",
                    ["presentationStyle"] = "rpg_engineering",
                    ["autoStartQuest"] = true,
                    ["autoFinishQuest"] = true,
                },
                cancellationToken: token);
            var bootstrapJson = Structured(bootstrap);
            var heroId = bootstrapJson.GetProperty("hero").GetProperty("heroId").GetString()!;

            var start = await client.CallToolAsync(
                "hero.start_quest",
                new Dictionary<string, object?>
                {
                    ["startRequestId"] = RequestId(),
                    ["heroId"] = heroId,
                    ["questType"] = "coding",
                    ["title"] = "Real stdio qualification",
                    ["goal"] = "Qualify the real Hero Passport stdio MCP lifecycle across supported protocol eras.",
                },
                cancellationToken: token);
            var questId = Structured(start).GetProperty("quest").GetProperty("questId").GetString()!;

            var finish = await client.CallToolAsync(
                "hero.finish_quest",
                new Dictionary<string, object?>
                {
                    ["finishRequestId"] = RequestId(),
                    ["questId"] = questId,
                    ["result"] = "success",
                    ["summary"] = "Qualified the real stdio MCP lifecycle and structured result boundary.",
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
                },
                cancellationToken: token);
            var finishJson = Structured(finish);
            var reward = finishJson.GetProperty("reward");
            Assert.Equal(85, reward.GetProperty("xpGained").GetInt64());
            Assert.Collection(
                reward.GetProperty("components").EnumerateArray().ToArray(),
                component => AssertRewardComponent(component, "clean_scope_bonus", 10),
                component => AssertRewardComponent(component, "clear_summary_bonus", 10),
                component => AssertRewardComponent(component, "no_user_corrections_bonus", 5));
            Assert.False(finishJson.TryGetProperty("activeTitle", out _));
            AssertStructuredTextEquality(finish);

            var card = await client.CallToolAsync(
                "hero.get_card",
                new Dictionary<string, object?> { ["heroId"] = heroId },
                cancellationToken: token);
            var cardHero = Structured(card).GetProperty("hero");
            Assert.Equal(85, cardHero.GetProperty("totalXp").GetInt64());
            Assert.Equal(85, cardHero.GetProperty("levelXp").GetInt64());
            Assert.Equal(100, cardHero.GetProperty("nextLevelXpRequired").GetInt64());
            Assert.False(cardHero.TryGetProperty("activeTitle", out _));
            AssertStructuredTextEquality(card);
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

    private static void AssertRewardComponent(JsonElement component, string key, long xpDelta)
    {
        Assert.Equal(key, component.GetProperty("key").GetString());
        Assert.Equal(xpDelta, component.GetProperty("xpDelta").GetInt64());
    }

    private static JsonElement Structured(CallToolResult result)
    {
        Assert.NotEqual(true, result.IsError);
        return Assert.IsType<JsonElement>(result.StructuredContent);
    }

    private static void AssertStructuredTextEquality(CallToolResult result)
    {
        var structured = Structured(result);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        using var document = JsonDocument.Parse(text.Text);
        Assert.True(JsonElement.DeepEquals(structured, document.RootElement));
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

    private static string RequestId() => Guid.CreateVersion7().ToString("D");
}
