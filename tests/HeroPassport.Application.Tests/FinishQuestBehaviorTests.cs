using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Globalization;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class FinishQuestBehaviorTests
{
    [Fact]
    public async Task SameRequestReplaysChangedRequestPayloadConflictsAndXpCommitsOnce()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var (hero, project, quest) = await StartQuestAsync(app, 'a', token);
            var request = FinishRequest(quest.QuestId, "success", "Completed the durable finish slice with deterministic base XP.");

            var first = await app.FinishQuestAsync(request, project, token);
            var replay = await app.FinishQuestAsync(request, project, token);

            Assert.False(first.Replayed);
            Assert.False(first.AlreadyFinalized);
            Assert.True(replay.Replayed);
            Assert.False(replay.AlreadyFinalized);
            Assert.Equal(first.QuestId, replay.QuestId);
            Assert.Equal(first.Result, replay.Result);
            Assert.Equal(first.Reward, replay.Reward);
            Assert.Equal(first.HeroProgress, replay.HeroProgress);
            Assert.Equal(first.TrustStrain, replay.TrustStrain);
            Assert.Equal(first.Streak, replay.Streak);
            Assert.True(first.SkillProgress.SequenceEqual(replay.SkillProgress));
            Assert.True(first.TraitsUnlocked.SequenceEqual(replay.TraitsUnlocked));
            Assert.True(first.TitlesUnlocked.SequenceEqual(replay.TitlesUnlocked));
            Assert.Equal(first.ActiveTitle, replay.ActiveTitle);
            Assert.True(first.Milestones.SequenceEqual(replay.Milestones));
            Assert.Equal(60, first.Reward.BaseXp);
            Assert.Equal(35, first.Reward.BonusXp);
            Assert.Equal(0, first.Reward.PenaltyXp);
            Assert.Equal(95, first.Reward.RawXp);
            Assert.Equal(1000, first.Reward.OutcomePermille);
            Assert.Equal(95, first.Reward.XpGained);
            Assert.Equal(hero.HeroId, first.HeroProgress.HeroId);
            Assert.Equal(0, first.HeroProgress.TotalXpBefore);
            Assert.Equal(95, first.HeroProgress.TotalXpAfter);

            var changed = await Assert.ThrowsAsync<HeroPassportException>(() =>
                app.FinishQuestAsync(request with { Summary = "Changed finalization payload." }, project, token));
            Assert.Equal("HP135", changed.Code);

            Assert.Equal(1, await ScalarLongAsync(path, "SELECT COUNT(*) FROM quest_reports;", token));
            Assert.Equal(1, await ScalarLongAsync(path, "SELECT COUNT(*) FROM xp_events;", token));
            Assert.Equal(95, await ScalarLongAsync(path, $"SELECT total_xp FROM heroes WHERE id='{hero.HeroId}';", token));
            Assert.Equal(95, await ScalarLongAsync(path, "SELECT total_xp_earned FROM hero_project_stats;", token));
            Assert.Equal(1, await ScalarLongAsync(path, "SELECT quests_finished FROM hero_project_stats;", token));
            Assert.Equal(1, await ScalarLongAsync(path, "SELECT quests_succeeded FROM hero_project_stats;", token));
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task FreshEquivalentRequestReturnsAlreadyFinalizedAndDifferentPayloadReturnsHp136()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var (_, project, quest) = await StartQuestAsync(app, 'b', token);
            var firstRequest = FinishRequest(quest.QuestId, "partial", "Implemented the durable terminal transition with partial outcome.");
            var committed = await app.FinishQuestAsync(firstRequest, project, token);

            var equivalent = await app.FinishQuestAsync(
                firstRequest with { FinishRequestId = MutationRequestId.New() },
                project,
                token);

            Assert.False(equivalent.Replayed);
            Assert.True(equivalent.AlreadyFinalized);
            Assert.Equal(committed.Reward, equivalent.Reward);
            Assert.Equal(committed.HeroProgress, equivalent.HeroProgress);
            Assert.True(committed.SkillProgress.SequenceEqual(equivalent.SkillProgress));
            Assert.Equal(2, await ScalarLongAsync(path, "SELECT COUNT(*) FROM mutation_receipts WHERE operation_key='finish_quest';", token));

            var conflict = await Assert.ThrowsAsync<HeroPassportException>(() =>
                app.FinishQuestAsync(
                    firstRequest with { FinishRequestId = MutationRequestId.New(), Result = "success" },
                    project,
                    token));
            Assert.Equal("HP136", conflict.Code);
            Assert.Equal(1, await ScalarLongAsync(path, "SELECT COUNT(*) FROM quest_reports;", token));
            Assert.Equal(1, await ScalarLongAsync(path, "SELECT COUNT(*) FROM xp_events;", token));
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task ConcurrentDifferentFinalizationsCommitAtMostOnce()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var setup = TestRuntime.CreateApplication(path);
            var (_, project, quest) = await StartQuestAsync(setup, 'c', token);
            var first = TestRuntime.CreateApplication(path);
            var second = TestRuntime.CreateApplication(path);

            var results = await Task.WhenAll(
                CaptureAsync(() => first.FinishQuestAsync(FinishRequest(quest.QuestId, "success", "Successful concurrent finalization payload with enough summary text."), project, token)),
                CaptureAsync(() => second.FinishQuestAsync(FinishRequest(quest.QuestId, "partial", "Partial concurrent finalization payload with enough summary text."), project, token)));

            var winner = Assert.Single(results, static result => result.Result is not null).Result!;
            var loser = Assert.IsType<HeroPassportException>(Assert.Single(results, static result => result.Error is not null).Error);
            Assert.Equal("HP136", loser.Code);
            Assert.Equal(1, await ScalarLongAsync(path, "SELECT COUNT(*) FROM quest_reports;", token));
            Assert.Equal(1, await ScalarLongAsync(path, "SELECT COUNT(*) FROM xp_events;", token));
            Assert.Equal(winner.Reward.XpGained, await ScalarLongAsync(path, "SELECT total_xp FROM heroes;", token));
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task ConcurrentEquivalentFinalizationsConvergeWithoutDuplicateProgression()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var setup = TestRuntime.CreateApplication(path);
            var (_, project, quest) = await StartQuestAsync(setup, '2', token);
            var first = TestRuntime.CreateApplication(path);
            var second = TestRuntime.CreateApplication(path);
            var firstRequest = FinishRequest(
                quest.QuestId,
                "success",
                "Equivalent concurrent finalization payload with deterministic reward and Skill progression.");
            var secondRequest = firstRequest with { FinishRequestId = MutationRequestId.New() };

            var results = await Task.WhenAll(
                CaptureAsync(() => first.FinishQuestAsync(firstRequest, project, token)),
                CaptureAsync(() => second.FinishQuestAsync(secondRequest, project, token)));

            Assert.All(results, static result => Assert.Null(result.Error));
            var fresh = Assert.Single(results, static result => result.Result is { AlreadyFinalized: false }).Result!;
            var converged = Assert.Single(results, static result => result.Result is { AlreadyFinalized: true }).Result!;
            Assert.False(fresh.Replayed);
            Assert.False(converged.Replayed);
            Assert.Equal(fresh.Reward, converged.Reward);
            Assert.Equal(fresh.HeroProgress, converged.HeroProgress);
            Assert.True(fresh.SkillProgress.SequenceEqual(converged.SkillProgress));
            Assert.Equal(1, await ScalarLongAsync(path, "SELECT COUNT(*) FROM quest_reports;", token));
            Assert.Equal(4, await ScalarLongAsync(path, "SELECT COUNT(*) FROM quest_reward_components;", token));
            Assert.Equal(1, await ScalarLongAsync(path, "SELECT COUNT(*) FROM quest_report_skills;", token));
            Assert.Equal(1, await ScalarLongAsync(path, "SELECT COUNT(*) FROM hero_skills;", token));
            Assert.Equal(1, await ScalarLongAsync(path, "SELECT COUNT(*) FROM xp_events;", token));
            Assert.Equal(2, await ScalarLongAsync(path, "SELECT COUNT(*) FROM mutation_receipts WHERE operation_key='finish_quest';", token));
            Assert.Equal(95, await ScalarLongAsync(path, "SELECT total_xp FROM heroes;", token));
            Assert.Equal(95, await ScalarLongAsync(path, "SELECT xp FROM hero_skills WHERE skill_key='coding';", token));
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task ProjectMismatchAndActiveHeroChangesCannotRedirectProgression()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var (owner, project, quest) = await StartQuestAsync(app, 'd', token);
            var other = (await app.CreateHeroAsync(new CreateHeroRequest(MutationRequestId.New(), "Other"), token)).Hero;
            await app.ActivateHeroAsync(other.HeroId, token);
            var request = FinishRequest(quest.QuestId, "success", "Finish must preserve the persisted Quest owner despite preference changes.");

            var mismatch = await Assert.ThrowsAsync<HeroPassportException>(() =>
                app.FinishQuestAsync(request, Project('e', "Wrong Project"), token));
            Assert.Equal("HP134", mismatch.Code);

            var finished = await app.FinishQuestAsync(request, project, token);
            Assert.Equal(owner.HeroId, finished.HeroProgress.HeroId);
            Assert.Equal(95, await ScalarLongAsync(path, $"SELECT total_xp FROM heroes WHERE id='{owner.HeroId}';", token));
            Assert.Equal(0, await ScalarLongAsync(path, $"SELECT total_xp FROM heroes WHERE id='{other.HeroId}';", token));
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task FailureBeforeReceiptCommitRollsBackReportXpProjectionAndQuestStatus()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var (hero, project, quest) = await StartQuestAsync(app, 'f', token);
            await ExecuteSqlAsync(
                path,
                """
                CREATE TRIGGER fail_finish_receipt
                BEFORE INSERT ON mutation_receipts
                WHEN NEW.operation_key = 'finish_quest'
                BEGIN
                    SELECT RAISE(ABORT, 'finish-test-failure');
                END;
                """,
                token);

            await Assert.ThrowsAsync<SqliteException>(() =>
                app.FinishQuestAsync(FinishRequest(quest.QuestId, "success", "This finalization must be rolled back before the receipt commits."), project, token));

            Assert.Equal(0, await ScalarLongAsync(path, "SELECT COUNT(*) FROM quest_reports;", token));
            Assert.Equal(0, await ScalarLongAsync(path, "SELECT COUNT(*) FROM xp_events;", token));
            Assert.Equal(0, await ScalarLongAsync(path, $"SELECT total_xp FROM heroes WHERE id='{hero.HeroId}';", token));
            Assert.Equal(0, await ScalarLongAsync(path, "SELECT quests_finished FROM hero_project_stats;", token));
            Assert.Equal(0, await ScalarLongAsync(path, "SELECT total_xp_earned FROM hero_project_stats;", token));
            Assert.Equal(1, await ScalarLongAsync(path, $"SELECT COUNT(*) FROM quest_sessions WHERE id='{quest.QuestId}' AND status='open' AND finished_at_utc IS NULL;", token));
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task InvalidResultMetricsAndSkillUseStableErrors()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var (_, project, quest) = await StartQuestAsync(app, '1', token);
            var valid = FinishRequest(quest.QuestId, "success", "Validate stable Finish error boundaries for invalid payload fields.");

            Assert.Equal("HP111", (await Assert.ThrowsAsync<HeroPassportException>(() =>
                app.FinishQuestAsync(valid with { Result = "invalid" }, project, token))).Code);
            Assert.Equal("HP120", (await Assert.ThrowsAsync<HeroPassportException>(() =>
                app.FinishQuestAsync(valid with { Metrics = valid.Metrics with { ScopeViolations = 21 } }, project, token))).Code);
            Assert.Equal("HP112", (await Assert.ThrowsAsync<HeroPassportException>(() =>
                app.FinishQuestAsync(valid with { SkillsUsed = ["unknown"] }, project, token))).Code);
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    private static async Task<(HeroIdentitySnapshot Hero, ProjectBindingContext Project, StartedQuestSnapshot Quest)> StartQuestAsync(
        HeroPassportApplication app,
        char projectKey,
        CancellationToken token)
    {
        var hero = (await app.BootstrapAsync(new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true), token)).Hero;
        var project = Project(projectKey, $"Project {projectKey}");
        var started = await app.StartQuestAsync(
            new StartQuestRequest(MutationRequestId.New(), hero.HeroId, "coding", "Quest", "Implement a durable FinishQuest transaction"),
            project,
            token);
        return (hero, project, started.Quest);
    }

    private static FinishQuestRequest FinishRequest(QuestId questId, string result, string summary) =>
        new(
            MutationRequestId.New(),
            questId,
            result,
            summary,
            new FinishQuestMetrics(
                TestsMentioned: true,
                ScopeViolations: 0,
                UserCorrections: 0,
                BuildStatus: "passed",
                BuildEvidence: "observed",
                TestsStatus: "passed",
                TestsEvidence: "observed"),
            ["coding"]);

    private static ProjectBindingContext Project(char fingerprintCharacter, string displayName) =>
        new(displayName, new string(fingerprintCharacter, 64), "project-identity/1");

    private static async Task<(FinishQuestResult? Result, Exception? Error)> CaptureAsync(Func<Task<FinishQuestResult>> action)
    {
        try { return (await action(), null); }
        catch (Exception exception) { return (null, exception); }
    }

    private static async Task ExecuteSqlAsync(string path, string sql, CancellationToken token)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task<long> ScalarLongAsync(string path, string sql, CancellationToken token)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
    }
}
