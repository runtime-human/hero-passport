using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class RewardSkillPersistenceTests
{
    [Fact]
    public async Task FinishPersistsFullRewardAndSkillAllocationAtomicallyAndReplayDoesNotDuplicate()
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
            var quest = (await app.StartQuestAsync(
                new StartQuestRequest(
                    MutationRequestId.New(),
                    hero.HeroId,
                    "coding",
                    "Implement full reward",
                    "Persist deterministic reward components and Skill allocation"),
                project,
                token)).Quest;
            var request = new FinishQuestRequest(
                MutationRequestId.New(),
                quest.QuestId,
                "success",
                "Implemented the full deterministic reward and Skill allocation transaction with verified tests.",
                new FinishQuestMetrics(
                    TestsMentioned: true,
                    ScopeViolations: 0,
                    UserCorrections: 0,
                    BuildStatus: "passed",
                    BuildEvidence: "observed",
                    TestsStatus: "passed",
                    TestsEvidence: "observed"),
                ["coding", "testing_awareness", "scope_control"]);

            var first = await app.FinishQuestAsync(request, project, token);
            var replay = await app.FinishQuestAsync(request, project, token);

            Assert.Equal(60, first.Reward.BaseXp);
            Assert.Equal(35, first.Reward.BonusXp);
            Assert.Equal(0, first.Reward.PenaltyXp);
            Assert.Equal(95, first.Reward.RawXp);
            Assert.Equal(1000, first.Reward.OutcomePermille);
            Assert.Equal(95, first.Reward.XpGained);
            Assert.Equal("reward/2.0.0", first.Reward.RewardRuleVersion);
            Assert.True(replay.Replayed);
            Assert.Equal(first, replay with { Replayed = false });

            Assert.Equal(4, await ScalarLongAsync(path, "SELECT COUNT(*) FROM quest_reward_components;", token));
            Assert.Equal(35, await ScalarLongAsync(path, "SELECT COALESCE(SUM(xp_delta),0) FROM quest_reward_components;", token));
            Assert.Equal(3, await ScalarLongAsync(path, "SELECT COUNT(*) FROM quest_report_skills;", token));
            Assert.Equal(3, await ScalarLongAsync(path, "SELECT COUNT(*) FROM hero_skills;", token));
            Assert.Equal(47, await SkillXpAsync(path, hero.HeroId, "coding", token));
            Assert.Equal(29, await SkillXpAsync(path, hero.HeroId, "testing_awareness", token));
            Assert.Equal(19, await SkillXpAsync(path, hero.HeroId, "scope_control", token));
            Assert.Equal(95, await ScalarLongAsync(path, "SELECT total_xp FROM heroes;", token));
            Assert.Equal(95, await ScalarLongAsync(path, "SELECT total_xp_earned FROM hero_project_stats;", token));
            Assert.Equal(95, await ScalarLongAsync(path, "SELECT xp_delta FROM xp_events;", token));
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task FailureBeforeFinishReceiptRollsBackRewardAndSkillRowsWithAllProjections()
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
                new StartQuestRequest(MutationRequestId.New(), hero.HeroId, "coding", "Rollback", "Qualify reward and Skill rollback"),
                project,
                token)).Quest;

            await ExecuteSqlAsync(
                path,
                """
                CREATE TRIGGER fail_task10_finish_receipt
                BEFORE INSERT ON mutation_receipts
                WHEN NEW.operation_key = 'finish_quest'
                BEGIN
                    SELECT RAISE(ABORT, 'task10-finish-test-failure');
                END;
                """,
                token);

            var request = new FinishQuestRequest(
                MutationRequestId.New(),
                quest.QuestId,
                "success",
                "This finalization has full quality bonuses but must roll back every Task 10 write.",
                new FinishQuestMetrics(true, 0, 0, "passed", "observed", "passed", "observed"),
                ["coding", "testing_awareness", "scope_control"]);

            await Assert.ThrowsAsync<SqliteException>(() => app.FinishQuestAsync(request, project, token));

            Assert.Equal(0, await ScalarLongAsync(path, "SELECT COUNT(*) FROM quest_reports;", token));
            Assert.Equal(0, await ScalarLongAsync(path, "SELECT COUNT(*) FROM quest_reward_components;", token));
            Assert.Equal(0, await ScalarLongAsync(path, "SELECT COUNT(*) FROM quest_report_skills;", token));
            Assert.Equal(0, await ScalarLongAsync(path, "SELECT COUNT(*) FROM hero_skills;", token));
            Assert.Equal(0, await ScalarLongAsync(path, "SELECT COUNT(*) FROM xp_events;", token));
            Assert.Equal(0, await ScalarLongAsync(path, "SELECT total_xp FROM heroes;", token));
            Assert.Equal(0, await ScalarLongAsync(path, "SELECT total_xp_earned FROM hero_project_stats;", token));
            Assert.Equal(1, await ScalarLongAsync(path, "SELECT COUNT(*) FROM quest_sessions WHERE status='open';", token));
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    private static async Task<long> SkillXpAsync(string path, HeroId heroId, string skillKey, CancellationToken token) =>
        await ScalarLongAsync(
            path,
            $"SELECT xp FROM hero_skills WHERE hero_id='{heroId}' AND skill_key='{skillKey}';",
            token);

    private static async Task<long> ScalarLongAsync(string path, string sql, CancellationToken token)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
        }.ToString());
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteSqlAsync(string path, string sql, CancellationToken token)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
        }.ToString());
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(token);
    }
}
