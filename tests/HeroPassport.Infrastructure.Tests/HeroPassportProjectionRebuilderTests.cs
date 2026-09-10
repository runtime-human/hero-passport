using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using Xunit;

namespace HeroPassport.Infrastructure.Tests;

public sealed class HeroPassportProjectionRebuilderTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task RebuildRestoresPublicReadModelsFromCanonicalHistoryWithoutChangingHistory()
    {
        var token = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = new HeroPassportApplication(new SqliteHeroPassportStateStore(path), new FixedTimeProvider());
            var hero = (await app.BootstrapAsync(
                new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true),
                token)).Hero;

            var projectA = new ProjectBindingContext("Project A", new string('a', 64), "project-identity/1");
            var projectB = new ProjectBindingContext("Project B", new string('b', 64), "project-identity/1");

            await FinishCleanQuestAsync(app, hero.HeroId, projectA, "coding", "coding", token);
            await FinishCleanQuestAsync(app, hero.HeroId, projectA, "review", "review", token);
            await FinishCleanQuestAsync(app, hero.HeroId, projectB, "debugging", "debugging", token);

            await app.StartQuestAsync(
                new StartQuestRequest(
                    MutationRequestId.New(),
                    hero.HeroId,
                    "planning",
                    "Keep one Quest open",
                    "Keep one canonical open Quest so project stats rebuild includes started but unfinished work."),
                projectA,
                token);

            var beforeA = await PublicCardJsonAsync(app, hero.HeroId, projectA, token);
            var beforeB = await PublicCardJsonAsync(app, hero.HeroId, projectB, token);
            var canonicalBefore = await CanonicalHistoryFingerprintAsync(path, token);

            await CorruptOnlyRebuildableProjectionsAsync(path, token);
            var corruptedA = await PublicCardJsonAsync(app, hero.HeroId, projectA, token);
            Assert.NotEqual(beforeA, corruptedA);

            var result = await HeroPassportProjectionRebuilder.RebuildAsync(path, token);

            Assert.Equal(1, result.HeroesRebuilt);
            Assert.Equal(3, result.HeroSkillsRebuilt);
            Assert.Equal(2, result.HeroProjectStatsRebuilt);
            Assert.Equal(beforeA, await PublicCardJsonAsync(app, hero.HeroId, projectA, token));
            Assert.Equal(beforeB, await PublicCardJsonAsync(app, hero.HeroId, projectB, token));
            Assert.Equal(canonicalBefore, await CanonicalHistoryFingerprintAsync(path, token));

            var doctor = await HeroPassportDatabaseDoctor.InspectAsync(path, token);
            Assert.True(doctor.Healthy);
            Assert.True(doctor.QuickCheckPassed);
            Assert.Equal(0, doctor.ForeignKeyViolationCount);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    private static async Task FinishCleanQuestAsync(
        HeroPassportApplication app,
        HeroId heroId,
        ProjectBindingContext project,
        string questType,
        string skillKey,
        CancellationToken token)
    {
        var quest = (await app.StartQuestAsync(
            new StartQuestRequest(
                MutationRequestId.New(),
                heroId,
                questType,
                $"Qualify {skillKey}",
                $"Create canonical history for deterministic projection rebuild of {skillKey}."),
            project,
            token)).Quest;

        await app.FinishQuestAsync(
            new FinishQuestRequest(
                MutationRequestId.New(),
                quest.QuestId,
                "success",
                "Completed deterministic projection rebuild qualification with observed passing tests and clean scope.",
                new FinishQuestMetrics(
                    TestsMentioned: true,
                    ScopeViolations: 0,
                    UserCorrections: 0,
                    BuildStatus: "not_run",
                    BuildEvidence: "none",
                    TestsStatus: "passed",
                    TestsEvidence: "observed"),
                [skillKey]),
            project,
            token);
    }

    private static async Task<string> PublicCardJsonAsync(
        HeroPassportApplication app,
        HeroId heroId,
        ProjectBindingContext project,
        CancellationToken token) =>
        JsonSerializer.Serialize(await app.GetCardAsync(heroId, project, token), JsonOptions);

    private static async Task CorruptOnlyRebuildableProjectionsAsync(string path, CancellationToken token)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE heroes
            SET total_xp=0,
                trust=1,
                strain=99,
                success_streak=77;
            DELETE FROM hero_skills;
            DELETE FROM hero_project_stats;
            """;
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task<string> CanonicalHistoryFingerprintAsync(string path, CancellationToken token)
    {
        await using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT line FROM (
                SELECT 'quest|' || id || '|' || hero_id || '|' || project_id || '|' || quest_type || '|' || status || '|' || started_at_utc || '|' || COALESCE(finished_at_utc,'') AS line
                FROM quest_sessions
                UNION ALL
                SELECT 'report|' || id || '|' || quest_id || '|' || result || '|' || xp_gained || '|' || hero_total_xp_before || '|' || hero_total_xp_after || '|' || trust_before || '|' || trust_after || '|' || strain_before || '|' || strain_after || '|' || streak_before || '|' || streak_after || '|' || COALESCE(active_title_before,'') || '|' || COALESCE(active_title_after,'')
                FROM quest_reports
                UNION ALL
                SELECT 'xp|' || id || '|' || quest_id || '|' || hero_id || '|' || project_id || '|' || xp_delta || '|' || reward_rule_version
                FROM xp_events
                UNION ALL
                SELECT 'skill|' || quest_report_id || '|' || ordinal || '|' || skill_key || '|' || xp_gained || '|' || xp_before || '|' || xp_after || '|' || level_before || '|' || level_after
                FROM quest_report_skills
                UNION ALL
                SELECT 'reward|' || quest_report_id || '|' || ordinal || '|' || component_key || '|' || xp_delta
                FROM quest_reward_components
                UNION ALL
                SELECT 'trust|' || quest_report_id || '|' || ordinal || '|' || component_key || '|' || trust_delta || '|' || strain_delta
                FROM quest_trust_strain_components
                UNION ALL
                SELECT 'trait|' || hero_id || '|' || trait_key || '|' || unlocked_at_utc || '|' || COALESCE(source_quest_id,'')
                FROM hero_traits
                UNION ALL
                SELECT 'title|' || hero_id || '|' || title_key || '|' || unlocked_at_utc || '|' || COALESCE(source_quest_id,'')
                FROM hero_titles
                UNION ALL
                SELECT 'milestone|' || quest_report_id || '|' || ordinal || '|' || event_key || '|' || semantic_key
                FROM quest_milestones
            )
            ORDER BY line;
            """;

        var lines = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            lines.Add(reader.GetString(0));
        }

        return string.Join('\n', lines);
    }

    private static string CreateDatabasePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HeroPassport.ProjectionRebuild.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "hero-passport.db");
    }

    private static void DeleteDatabase(string path)
    {
        SqliteConnection.ClearAllPools();
        var directory = Path.GetDirectoryName(path);
        if (directory is null)
        {
            return;
        }

        try { Directory.Delete(directory, recursive: true); } catch (DirectoryNotFoundException) { } catch (IOException) { }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 9, 4, 0, 0, TimeSpan.Zero);
    }
}
