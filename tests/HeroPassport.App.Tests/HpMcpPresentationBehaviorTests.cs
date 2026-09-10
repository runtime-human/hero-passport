using HeroPassport.App.Mcp;
using HeroPassport.Application.Runtime;
using HeroPassport.Infrastructure.Persistence;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using Xunit;

namespace HeroPassport.App.Tests;

public sealed class HpMcpPresentationBehaviorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] ScopeControlSkills = ["scope_control"];

    [Fact]
    public async Task StartFinishAndCardUseLocalizedDisplayWithoutChangingCanonicalWireKeys()
    {
        var token = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), "HeroPassport.App.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "hero-passport.db");
        Directory.CreateDirectory(directory);
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var application = new HeroPassportApplication(new SqliteHeroPassportStateStore(path), TimeProvider.System);
            var project = new ProjectBindingContext("Project", new string('c', 64), "project-identity/1");
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
            var heroId = Structured(bootstrap).GetProperty("hero").GetProperty("heroId").GetString()!;

            var started = await adapter.InvokeAsync(
                "hero.start_quest",
                Arguments(new
                {
                    startRequestId = RequestId(),
                    heroId,
                    questType = "coding",
                    title = "Localized Quest",
                    goal = "Prove localized display text without changing the canonical MCP contract.",
                }),
                token);
            var startJson = Structured(started);
            Assert.Equal("⚔ Quest: Localized Quest", startJson.GetProperty("displayText").GetString());
            var questId = startJson.GetProperty("quest").GetProperty("questId").GetString()!;

            _ = await adapter.InvokeAsync(
                "hero.configure",
                Arguments(new
                {
                    locale = "ru-RU",
                    presentationStyle = "minimal",
                    autoStartQuest = true,
                    autoFinishQuest = true,
                }),
                token);

            var finished = await adapter.InvokeAsync(
                "hero.finish_quest",
                Arguments(new
                {
                    finishRequestId = RequestId(),
                    questId,
                    result = "success",
                    summary = "Finish after switching the global locale so the Quest snapshot remains authoritative.",
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
                    skillsUsed = ScopeControlSkills,
                }),
                token);
            var finishJson = Structured(finished);
            var finishDisplay = finishJson.GetProperty("displayText").GetString()!;
            Assert.Contains("Success", finishDisplay, StringComparison.Ordinal);
            Assert.Contains("Control", finishDisplay, StringComparison.Ordinal);
            Assert.DoesNotContain("Успех", finishDisplay, StringComparison.Ordinal);
            Assert.Equal(
                "clean_scope_bonus",
                finishJson.GetProperty("reward").GetProperty("components")[0].GetProperty("key").GetString());

            var card = await adapter.InvokeAsync("hero.get_card", Arguments(new { heroId }), token);
            var cardJson = Structured(card);
            Assert.Contains("Оруженосец кода", cardJson.GetProperty("displayText").GetString(), StringComparison.Ordinal);
            Assert.Equal("code_squire", cardJson.GetProperty("hero").GetProperty("rankKey").GetString());
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
