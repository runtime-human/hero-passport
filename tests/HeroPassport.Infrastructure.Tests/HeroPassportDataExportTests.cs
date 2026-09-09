using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using Xunit;

namespace HeroPassport.Infrastructure.Tests;

public sealed class HeroPassportDataExportTests
{
    [Fact]
    public async Task ExportPublishesAllowlistedPortableJsonWithoutPrivatePersistenceMetadata()
    {
        var token = TestContext.Current.CancellationToken;
        var root = CreateRoot();
        var source = Path.Combine(root, "source", "hero-passport.db");
        var destination = Path.Combine(root, "exports", "hero-passport.json");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);

        try
        {
            await HeroPassportDatabase.InitializeAsync(source, token);
            var app = new HeroPassportApplication(new SqliteHeroPassportStateStore(source), TimeProvider.System);
            var hero = (await app.BootstrapAsync(
                new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true),
                token)).Hero;

            const string privateFingerprint = "f4c3a2b1f4c3a2b1f4c3a2b1f4c3a2b1f4c3a2b1f4c3a2b1f4c3a2b1f4c3a2b1";
            var project = new ProjectBindingContext("Project Phoenix", privateFingerprint, "project-identity/1");
            var quest = (await app.StartQuestAsync(
                new StartQuestRequest(
                    MutationRequestId.New(),
                    hero.HeroId,
                    "coding",
                    "Implement export",
                    "Produce a bounded user-facing snapshot without private persistence metadata."),
                project,
                token)).Quest;
            await app.FinishQuestAsync(
                new FinishQuestRequest(
                    MutationRequestId.New(),
                    quest.QuestId,
                    "success",
                    "Export completed with bounded fields and observed tests.",
                    new FinishQuestMetrics(true, 0, 0, "not_run", "none", "passed", "observed"),
                    ["coding"]),
                project,
                token);

            var result = await HeroPassportDataExport.CreateAsync(source, destination, token);

            Assert.Equal("hero-passport-export/1", result.SchemaVersion);
            Assert.Equal(Path.GetFullPath(destination), result.DestinationPath);
            Assert.True(result.SizeBytes > 0);
            Assert.True(File.Exists(destination));

            var jsonText = await File.ReadAllTextAsync(destination, token);
            Assert.DoesNotContain(privateFingerprint, jsonText, StringComparison.Ordinal);
            foreach (var forbiddenName in new[]
            {
                "workspaceFingerprint",
                "projectId",
                "projectIdentitySalt",
                "requestId",
                "argsHash",
                "finalizationArgsHash",
                "operationKey",
            })
            {
                Assert.DoesNotContain($"\"{forbiddenName}\"", jsonText, StringComparison.OrdinalIgnoreCase);
            }

            using var document = JsonDocument.Parse(jsonText);
            var rootElement = document.RootElement;
            Assert.Equal("hero-passport-export/1", rootElement.GetProperty("schemaVersion").GetString());

            var settings = rootElement.GetProperty("settings");
            Assert.Equal(hero.HeroId.ToString(), settings.GetProperty("activeHeroId").GetString());
            Assert.Equal("en-US", settings.GetProperty("locale").GetString());
            Assert.Equal("rpg_engineering", settings.GetProperty("presentationStyle").GetString());
            Assert.True(settings.GetProperty("autoStartQuest").GetBoolean());
            Assert.True(settings.GetProperty("autoFinishQuest").GetBoolean());

            var heroes = rootElement.GetProperty("heroes");
            Assert.Equal(1, heroes.GetArrayLength());
            var exportedHero = heroes[0];
            Assert.Equal(hero.HeroId.ToString(), exportedHero.GetProperty("heroId").GetString());
            Assert.Equal("Nova", exportedHero.GetProperty("name").GetString());
            Assert.Equal(95, exportedHero.GetProperty("totalXp").GetInt64());
            Assert.Equal(52, exportedHero.GetProperty("trust").GetInt32());
            Assert.Equal(18, exportedHero.GetProperty("strain").GetInt32());
            Assert.Equal(1, exportedHero.GetProperty("successStreak").GetInt64());
            Assert.Contains(
                exportedHero.GetProperty("skills").EnumerateArray(),
                skill => skill.GetProperty("skillKey").GetString() == "coding" &&
                    skill.GetProperty("xp").GetInt64() == 95);

            var quests = rootElement.GetProperty("quests");
            Assert.Equal(1, quests.GetArrayLength());
            var exportedQuest = quests[0];
            Assert.Equal(quest.QuestId.ToString(), exportedQuest.GetProperty("questId").GetString());
            Assert.Equal(hero.HeroId.ToString(), exportedQuest.GetProperty("heroId").GetString());
            Assert.Equal("Project Phoenix", exportedQuest.GetProperty("projectDisplayName").GetString());
            Assert.Equal("coding", exportedQuest.GetProperty("questType").GetString());
            Assert.Equal("Implement export", exportedQuest.GetProperty("title").GetString());
            Assert.Equal("finished", exportedQuest.GetProperty("status").GetString());

            var report = exportedQuest.GetProperty("report");
            Assert.Equal("success", report.GetProperty("result").GetString());
            Assert.Equal(95, report.GetProperty("xpGained").GetInt64());
            Assert.Equal("passed", report.GetProperty("testsStatus").GetString());
            Assert.Equal("observed", report.GetProperty("testsEvidence").GetString());
            Assert.Contains(
                report.GetProperty("skills").EnumerateArray(),
                skill => skill.GetProperty("skillKey").GetString() == "coding" &&
                    skill.GetProperty("xpGained").GetInt64() == 95);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ExportNeverOverwritesExistingDestination()
    {
        var token = TestContext.Current.CancellationToken;
        var root = CreateRoot();
        var source = Path.Combine(root, "source.db");
        var destination = Path.Combine(root, "known-good.json");
        try
        {
            await HeroPassportDatabase.InitializeAsync(source, token);
            var knownGood = "{\"knownGood\":true}";
            await File.WriteAllTextAsync(destination, knownGood, token);

            await Assert.ThrowsAsync<IOException>(() =>
                HeroPassportDataExport.CreateAsync(source, destination, token));

            Assert.Equal(knownGood, await File.ReadAllTextAsync(destination, token));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task MissingSourceNeverCreatesExport()
    {
        var token = TestContext.Current.CancellationToken;
        var root = CreateRoot();
        var source = Path.Combine(root, "missing.db");
        var destination = Path.Combine(root, "export.json");
        try
        {
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                HeroPassportDataExport.CreateAsync(source, destination, token));

            Assert.False(File.Exists(destination));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "HeroPassport.DataExport.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteRoot(string root)
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(root, recursive: true); } catch (DirectoryNotFoundException) { } catch (IOException) { }
    }
}
