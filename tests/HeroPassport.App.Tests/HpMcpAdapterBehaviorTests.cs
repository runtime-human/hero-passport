using HeroPassport.App.Mcp;
using HeroPassport.Application.Runtime;
using HeroPassport.Infrastructure.Persistence;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using Xunit;

namespace HeroPassport.App.Tests;

public sealed class HpMcpAdapterBehaviorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] CreateHeroWireFields = ["heroId", "name", "level", "rankKey", "trust", "strain", "archived"];

    [Fact]
    public async Task FinishAndCardExposeFullWireProjectionAndCreateDoesNotLeakListOnlyFields()
    {
        var token = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), "HeroPassport.App.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "hero-passport.db");
        Directory.CreateDirectory(directory);
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var application = new HeroPassportApplication(new SqliteHeroPassportStateStore(path), TimeProvider.System);
            var project = new ProjectBindingContext("Project", new string('d', 64), "project-identity/1");
            var adapter = new HpMcpAdapter(application, _ => Task.FromResult(project));

            var bootstrap = await adapter.InvokeAsync(
                "hero.bootstrap",
                Arguments(new
                {
                    bootstrapRequestId = RequestId(),
                    locale = "en-US",
                    heroName = "Nova",
                    presentationStyle = "rpg_engineering",
                    autoStartQuest = true,
                    autoFinishQuest = true,
                }),
                token);
            var bootstrapJson = Structured(bootstrap);
            var heroId = bootstrapJson.GetProperty("hero").GetProperty("heroId").GetString()!;

            var created = await adapter.InvokeAsync(
                "hero.create",
                Arguments(new { createRequestId = RequestId(), name = "Second" }),
                token);
            var createdHero = Structured(created).GetProperty("hero");
            Assert.Equal(CreateHeroWireFields, createdHero.EnumerateObject().Select(static property => property.Name).ToArray());

            var started = await adapter.InvokeAsync(
                "hero.start_quest",
                Arguments(new
                {
                    startRequestId = RequestId(),
                    heroId,
                    questType = "coding",
                    title = "MCP projection",
                    goal = "Verify the explicit structured MCP result contract end to end.",
                }),
                token);
            var questId = Structured(started).GetProperty("quest").GetProperty("questId").GetString()!;

            var finished = await adapter.InvokeAsync(
                "hero.finish_quest",
                Arguments(new
                {
                    finishRequestId = RequestId(),
                    questId,
                    result = "success",
                    summary = "Finish the MCP projection slice and verify immutable structured response fields.",
                    metrics = new
                    {
                        testsMentioned = false,
                        scopeViolations = 0,
                        userCorrections = 0,
                        buildStatus = "not_run",
                        buildEvidence = "none",
                        testsStatus = "not_run",
                        testsEvidence = "none",
                    },
                    skillsUsed = new[] { "coding" },
                }),
                token);
            var finishJson = Structured(finished);
            Assert.True(finishJson.TryGetProperty("trustStrain", out _));
            Assert.True(finishJson.TryGetProperty("streak", out _));
            Assert.True(finishJson.TryGetProperty("skillProgress", out _));
            Assert.True(finishJson.TryGetProperty("traitsUnlocked", out _));
            Assert.True(finishJson.TryGetProperty("titlesUnlocked", out _));
            Assert.True(finishJson.TryGetProperty("milestones", out _));
            Assert.Equal(JsonValueKind.Null, finishJson.GetProperty("activeTitle").ValueKind);
            var progress = finishJson.GetProperty("heroProgress");
            Assert.False(progress.GetProperty("isLevelCapped").GetBoolean());
            Assert.Equal(60, progress.GetProperty("levelXp").GetInt64());
            Assert.Equal(100, progress.GetProperty("nextLevelXpRequired").GetInt64());

            var card = await adapter.InvokeAsync("hero.get_card", Arguments(new { heroId }), token);
            var cardHero = Structured(card).GetProperty("hero");
            Assert.False(cardHero.GetProperty("isLevelCapped").GetBoolean());
            Assert.Equal(60, cardHero.GetProperty("levelXp").GetInt64());
            Assert.Equal(100, cardHero.GetProperty("nextLevelXpRequired").GetInt64());
            Assert.Equal(JsonValueKind.Null, cardHero.GetProperty("activeTitle").ValueKind);
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task SetupGatedToolReturnsSafeToolErrorWithoutStructuredContent()
    {
        var token = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), "HeroPassport.App.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "hero-passport.db");
        Directory.CreateDirectory(directory);
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var application = new HeroPassportApplication(new SqliteHeroPassportStateStore(path), TimeProvider.System);
            var project = new ProjectBindingContext("Project", new string('e', 64), "project-identity/1");
            var adapter = new HpMcpAdapter(application, _ => Task.FromResult(project));

            var result = await adapter.InvokeAsync("hero.list", Arguments(new { }), token);

            Assert.True(result.IsError);
            Assert.Null(result.StructuredContent);
            var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
            Assert.Contains("HP001", text.Text, StringComparison.Ordinal);
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch (IOException) { }
        }
    }

    private static Dictionary<string, JsonElement> Arguments<T>(T value)
    {
        var element = JsonSerializer.SerializeToElement(value, JsonOptions);
        return element.EnumerateObject().ToDictionary(
            static property => property.Name,
            static property => property.Value.Clone(),
            StringComparer.Ordinal);
    }

    private static JsonElement Structured(CallToolResult result)
    {
        Assert.NotEqual(true, result.IsError);
        return Assert.IsType<JsonElement>(result.StructuredContent);
    }

    private static string RequestId() => Guid.CreateVersion7().ToString("D");
}
