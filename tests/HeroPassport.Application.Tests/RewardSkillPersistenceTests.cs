using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Globalization;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class RewardSkillPersistenceTests
{
    [Fact]
    public async Task FinishPersistsVersionedRewardSkillHistoryAndReplayRemainsImmutable()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var hero = (await app.BootstrapAsync(
                new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true),
                token)).Hero;
            var project = new ProjectBindingContext("Reward Project", new string('a', 64), "project-identity/1");

            var firstQuest = (await app.StartQuestAsync(
                new StartQuestRequest(
                    MutationRequestId.New(),
                    hero.HeroId,
                    "coding",
                    "Implement full reward",
                    "Persist deterministic reward components and Skill allocation."),
                project,
                token)).Quest;
            var firstRequest = CleanFinish(
                MutationRequestId.New(),
                firstQuest.QuestId,
                ["coding", "testing_awareness", "scope_control"]);

            var first = await app.FinishQuestAsync(firstRequest, project, token);

            Assert.Equal(60, first.Reward.BaseXp);
            Assert.Equal(35, first.Reward.BonusXp);
            Assert.Equal(0, first.Reward.PenaltyXp);
            Assert.Equal(95, first.Reward.RawXp);
            Assert.Equal(1000, first.Reward.OutcomePermille);
            Assert.Equal(95, first.Reward.XpGained);
            Assert.Equal("reward/2.0.0", first.Reward.RewardRuleVersion);
            Assert.Collection(
                first.Reward.Components,
                component => AssertRewardComponent(component, "observed_tests_passed_bonus", 10),
                component => AssertRewardComponent(component, "clean_scope_bonus", 10),
                component => AssertRewardComponent(component, "clear_summary_bonus", 10),
                component => AssertRewardComponent(component, "no_user_corrections_bonus", 5));
            Assert.Collection(
                first.SkillProgress,
                skill => AssertSkill(skill, "coding", 47, 47, 1, 1, 50),
                skill => AssertSkill(skill, "testing_awareness", 29, 29, 1, 1, 50),
                skill => AssertSkill(skill, "scope_control", 19, 19, 1, 1, 50));

            var secondQuest = (await app.StartQuestAsync(
                new StartQuestRequest(
                    MutationRequestId.New(),
                    hero.HeroId,
                    "coding",
                    "Advance coding Skill",
                    "Verify cumulative Skill projection and immutable historical snapshots."),
                project,
                token)).Quest;
            var second = await app.FinishQuestAsync(
                CleanFinish(MutationRequestId.New(), secondQuest.QuestId, ["coding"]),
                project,
                token);

            Assert.Equal(95, second.Reward.XpGained);
            Assert.Collection(
                second.SkillProgress,
                skill => AssertSkill(skill, "coding", 95, 142, 1, 3, 100));

            var restarted = TestRuntime.CreateApplication(path);
            var replay = await restarted.FinishQuestAsync(firstRequest, project, token);

            Assert.True(replay.Replayed);
            Assert.Equal(first.Reward, replay.Reward);
            Assert.Equal(first.HeroProgress, replay.HeroProgress);
            Assert.Equal(first.SkillProgress, replay.SkillProgress);

            Assert.Equal(
                [
                    "observed_tests_passed_bonus:10",
                    "clean_scope_bonus:10",
                    "clear_summary_bonus:10",
                    "no_user_corrections_bonus:5",
                ],
                await StringsAsync(
                    path,
                    $"SELECT component_key || ':' || xp_delta FROM quest_reward_components WHERE quest_report_id=(SELECT id FROM quest_reports WHERE quest_id='{firstQuest.QuestId}') ORDER BY ordinal;",
                    token));
            Assert.Equal(
                [
                    "coding:47:0:47:1:1",
                    "testing_awareness:29:0:29:1:1",
                    "scope_control:19:0:19:1:1",
                ],
                await StringsAsync(
                    path,
                    $"SELECT skill_key || ':' || xp_gained || ':' || xp_before || ':' || xp_after || ':' || level_before || ':' || level_after FROM quest_report_skills WHERE quest_report_id=(SELECT id FROM quest_reports WHERE quest_id='{firstQuest.QuestId}') ORDER BY ordinal;",
                    token));
            Assert.Equal(
                ["coding:95:47:142:1:3"],
                await StringsAsync(
                    path,
                    $"SELECT skill_key || ':' || xp_gained || ':' || xp_before || ':' || xp_after || ':' || level_before || ':' || level_after FROM quest_report_skills WHERE quest_report_id=(SELECT id FROM quest_reports WHERE quest_id='{secondQuest.QuestId}') ORDER BY ordinal;",
                    token));

            Assert.Equal(8, await ScalarLongAsync(path, "SELECT COUNT(*) FROM quest_reward_components;", token));
            Assert.Equal(4, await ScalarLongAsync(path, "SELECT COUNT(*) FROM quest_report_skills;", token));
            Assert.Equal(3, await ScalarLongAsync(path, "SELECT COUNT(*) FROM hero_skills;", token));
            Assert.Equal(142, await SkillXpAsync(path, hero.HeroId, "coding", token));
            Assert.Equal(29, await SkillXpAsync(path, hero.HeroId, "testing_awareness", token));
            Assert.Equal(19, await SkillXpAsync(path, hero.HeroId, "scope_control", token));
            Assert.Equal(190, await ScalarLongAsync(path, "SELECT total_xp FROM heroes;", token));
            Assert.Equal(190, await ScalarLongAsync(path, "SELECT total_xp_earned FROM hero_project_stats;", token));
            Assert.Equal(190, await ScalarLongAsync(path, "SELECT SUM(xp_delta) FROM xp_events;", token));
            Assert.Equal(2, await ScalarLongAsync(path, "SELECT COUNT(*) FROM xp_events;", token));
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task FailureBeforeFinishReceiptRollsBackRewardSkillHistoryAndProjections()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var hero = (await app.BootstrapAsync(
                new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true),
                token)).Hero;
            var project = new ProjectBindingContext("Rollback Project", new string('b', 64), "project-identity/1");
            var quest = (await app.StartQuestAsync(
                new StartQuestRequest(
                    MutationRequestId.New(),
                    hero.HeroId,
                    "coding",
                    "Rollback reward",
                    "Qualify atomic reward and Skill rollback before receipt persistence."),
                project,
                token)).Quest;

            await ExecuteSqlAsync(
                path,
                """
                CREATE TRIGGER fail_reward_skill_finish_receipt
                BEFORE INSERT ON mutation_receipts
                WHEN NEW.operation_key = 'finish_quest'
                BEGIN
                    SELECT RAISE(ABORT, 'reward-skill-finish-test-failure');
                END;
                """,
                token);

            await Assert.ThrowsAsync<SqliteException>(() => app.FinishQuestAsync(
                CleanFinish(
                    MutationRequestId.New(),
                    quest.QuestId,
                    ["coding", "testing_awareness", "scope_control"]),
                project,
                token));

            Assert.Equal(0, await ScalarLongAsync(path, "SELECT COUNT(*) FROM quest_reports;", token));
            Assert.Equal(0, await ScalarLongAsync(path, "SELECT COUNT(*) FROM quest_reward_components;", token));
            Assert.Equal(0, await ScalarLongAsync(path, "SELECT COUNT(*) FROM quest_report_skills;", token));
            Assert.Equal(0, await ScalarLongAsync(path, "SELECT COUNT(*) FROM hero_skills;", token));
            Assert.Equal(0, await ScalarLongAsync(path, "SELECT COUNT(*) FROM xp_events;", token));
            Assert.Equal(0, await ScalarLongAsync(path, "SELECT total_xp FROM heroes;", token));
            Assert.Equal(0, await ScalarLongAsync(path, "SELECT total_xp_earned FROM hero_project_stats;", token));
            Assert.Equal("open", await ScalarStringAsync(path, "SELECT status FROM quest_sessions;", token));
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    private static FinishQuestRequest CleanFinish(
        MutationRequestId requestId,
        QuestId questId,
        IReadOnlyList<string> skills) =>
        new(
            requestId,
            questId,
            "success",
            "Implemented the deterministic reward and Skill transaction with directly observed passing tests.",
            new FinishQuestMetrics(
                TestsMentioned: true,
                ScopeViolations: 0,
                UserCorrections: 0,
                BuildStatus: "passed",
                BuildEvidence: "observed",
                TestsStatus: "passed",
                TestsEvidence: "observed"),
            skills);

    private static void AssertRewardComponent(
        RewardComponentSnapshot component,
        string key,
        long xpDelta)
    {
        Assert.Equal(key, component.Key);
        Assert.Equal(xpDelta, component.XpDelta);
    }

    private static void AssertSkill(
        SkillProgressSnapshot skill,
        string key,
        long gained,
        long after,
        int levelBefore,
        int levelAfter,
        long? nextLevelXpRequired)
    {
        Assert.Equal(key, skill.SkillKey);
        Assert.Equal(gained, skill.XpGained);
        Assert.Equal(after, skill.XpAfter);
        Assert.Equal(levelBefore, skill.LevelBefore);
        Assert.Equal(levelAfter, skill.LevelAfter);
        Assert.Equal(levelAfter == 10, skill.IsLevelCapped);
        Assert.Equal(nextLevelXpRequired, skill.NextLevelXpRequired);
    }

    private static async Task<long> SkillXpAsync(
        string path,
        HeroId heroId,
        string skillKey,
        CancellationToken token) =>
        await ScalarLongAsync(
            path,
            $"SELECT xp FROM hero_skills WHERE hero_id='{heroId}' AND skill_key='{skillKey}';",
            token);

    private static async Task<long> ScalarLongAsync(string path, string sql, CancellationToken token)
    {
        await using var connection = await OpenAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
    }

    private static async Task<string> ScalarStringAsync(string path, string sql, CancellationToken token)
    {
        await using var connection = await OpenAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static async Task<string[]> StringsAsync(string path, string sql, CancellationToken token)
    {
        await using var connection = await OpenAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(token);
        var values = new List<string>();
        while (await reader.ReadAsync(token))
        {
            values.Add(reader.GetString(0));
        }

        return [.. values];
    }

    private static async Task ExecuteSqlAsync(string path, string sql, CancellationToken token)
    {
        await using var connection = await OpenAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task<SqliteConnection> OpenAsync(string path, CancellationToken token)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
        }.ToString());
        await connection.OpenAsync(token);
        return connection;
    }
}