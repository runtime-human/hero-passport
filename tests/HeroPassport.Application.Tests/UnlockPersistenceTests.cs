using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Globalization;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class UnlockPersistenceTests
{
    [Fact]
    public async Task RealQuestProgressionUnlocksMonotonicTraitsTitlesAndReplaysImmutableMilestones()
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
            var project = new ProjectBindingContext("Unlock Project", new string('f', 64), "project-identity/1");

            FinishQuestResult? fourth = null;
            FinishQuestResult? fifth = null;
            QuestId? fourthQuestId = null;
            QuestId? fifthQuestId = null;
            for (var questNumber = 1; questNumber <= 5; questNumber++)
            {
                var quest = (await app.StartQuestAsync(
                    new StartQuestRequest(
                        MutationRequestId.New(),
                        hero.HeroId,
                        "coding",
                        $"Unlock qualification {questNumber}",
                        "Qualify deterministic monotonic unlock persistence through normal Quest progression."),
                    project,
                    token)).Quest;

                var finished = await app.FinishQuestAsync(
                    FinishRequest(quest.QuestId),
                    project,
                    token);

                if (questNumber == 4)
                {
                    fourthQuestId = quest.QuestId;
                    fourth = finished;
                }
                else if (questNumber == 5)
                {
                    fifthQuestId = quest.QuestId;
                    fifth = finished;
                }
            }

            fourth = Assert.IsType<FinishQuestResult>(fourth);
            var fourthId = Assert.IsType<QuestId>(fourthQuestId);
            Assert.Empty(fourth.TraitsUnlocked);
            Assert.Equal(["skill_specialist"], fourth.TitlesUnlocked);
            Assert.Equal("skill_specialist", fourth.ActiveTitle);
            Assert.Contains(fourth.Milestones, static milestone =>
                milestone == new MilestoneSnapshot("title_unlocked", "title:skill_specialist"));

            fifth = Assert.IsType<FinishQuestResult>(fifth);
            var fifthId = Assert.IsType<QuestId>(fifthQuestId);
            Assert.Equal(["precise_executor", "test_scout", "steady_hand"], fifth.TraitsUnlocked);
            Assert.Empty(fifth.TitlesUnlocked);
            Assert.Equal("skill_specialist", fifth.ActiveTitle);
            Assert.Contains(fifth.Milestones, static milestone =>
                milestone == new MilestoneSnapshot("trait_unlocked", "trait:precise_executor"));
            Assert.Contains(fifth.Milestones, static milestone =>
                milestone == new MilestoneSnapshot("trait_unlocked", "trait:test_scout"));
            Assert.Contains(fifth.Milestones, static milestone =>
                milestone == new MilestoneSnapshot("trait_unlocked", "trait:steady_hand"));

            Assert.Equal(
                [$"skill_specialist:{fourthId}"],
                await StringsAsync(
                    path,
                    $"SELECT title_key || ':' || source_quest_id FROM hero_titles WHERE hero_id='{hero.HeroId}' ORDER BY title_key;",
                    token));
            Assert.Equal(
                [
                    $"precise_executor:{fifthId}",
                    $"steady_hand:{fifthId}",
                    $"test_scout:{fifthId}",
                ],
                await StringsAsync(
                    path,
                    $"SELECT trait_key || ':' || source_quest_id FROM hero_traits WHERE hero_id='{hero.HeroId}' ORDER BY trait_key;",
                    token));
            Assert.Equal(
                ":skill_specialist",
                await ScalarStringAsync(
                    path,
                    $"SELECT COALESCE(active_title_before,'') || ':' || COALESCE(active_title_after,'') FROM quest_reports WHERE quest_id='{fourthId}';",
                    token));
            Assert.Equal(
                "skill_specialist:skill_specialist",
                await ScalarStringAsync(
                    path,
                    $"SELECT COALESCE(active_title_before,'') || ':' || COALESCE(active_title_after,'') FROM quest_reports WHERE quest_id='{fifthId}';",
                    token));
            Assert.Equal(
                fifth.Milestones.Select(static milestone => $"{milestone.EventKey}:{milestone.SemanticKey}").ToArray(),
                await StringsAsync(
                    path,
                    $"SELECT event_key || ':' || semantic_key FROM quest_milestones WHERE quest_report_id=(SELECT id FROM quest_reports WHERE quest_id='{fifthId}') ORDER BY ordinal;",
                    token));

            var replay = await app.FinishQuestAsync(FinishRequest(fifth.QuestId) with
            {
                FinishRequestId = MutationRequestId.New(),
            }, project, token);
            Assert.True(replay.AlreadyFinalized);
            Assert.Equal(fifth.TraitsUnlocked, replay.TraitsUnlocked);
            Assert.Equal(fifth.TitlesUnlocked, replay.TitlesUnlocked);
            Assert.Equal(fifth.ActiveTitle, replay.ActiveTitle);
            Assert.Equal(fifth.Milestones, replay.Milestones);

            var card = await app.GetCardAsync(hero.HeroId, project, token);
            Assert.Equal(["precise_executor", "steady_hand", "test_scout"], card.Hero.Traits);
            Assert.Equal(["skill_specialist"], card.Hero.Titles);
            Assert.Equal("skill_specialist", card.Hero.ActiveTitle);
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task FailedFourthFinishRollsBackTitleMilestonesAndAllProgressionProjections()
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
            var project = new ProjectBindingContext("Unlock Rollback", new string('e', 64), "project-identity/1");

            for (var questNumber = 1; questNumber <= 3; questNumber++)
            {
                var committedQuest = await StartQuestAsync(app, hero.HeroId, project, questNumber, token);
                await app.FinishQuestAsync(FinishRequest(committedQuest.QuestId), project, token);
            }

            var milestonesBefore = await ScalarLongAsync(path, "SELECT COUNT(*) FROM quest_milestones;", token);
            var fourthQuest = await StartQuestAsync(app, hero.HeroId, project, 4, token);
            await ExecuteSqlAsync(
                path,
                """
                CREATE TRIGGER fail_unlock_finish_receipt
                BEFORE INSERT ON mutation_receipts
                WHEN NEW.operation_key = 'finish_quest'
                BEGIN
                    SELECT RAISE(ABORT, 'unlock-finish-test-failure');
                END;
                """,
                token);

            await Assert.ThrowsAsync<SqliteException>(() =>
                app.FinishQuestAsync(FinishRequest(fourthQuest.QuestId), project, token));

            Assert.Equal(0, await ScalarLongAsync(path, "SELECT COUNT(*) FROM hero_titles;", token));
            Assert.Equal(0, await ScalarLongAsync(path, "SELECT COUNT(*) FROM hero_traits;", token));
            Assert.Equal(3, await ScalarLongAsync(path, "SELECT COUNT(*) FROM quest_reports;", token));
            Assert.Equal(milestonesBefore, await ScalarLongAsync(path, "SELECT COUNT(*) FROM quest_milestones;", token));
            Assert.Equal(285, await ScalarLongAsync(path, "SELECT total_xp FROM heroes;", token));
            Assert.Equal(285, await ScalarLongAsync(path, $"SELECT xp FROM hero_skills WHERE hero_id='{hero.HeroId}' AND skill_key='coding';", token));
            Assert.Equal(3, await ScalarLongAsync(path, "SELECT success_streak FROM heroes;", token));
            Assert.Equal(
                "open",
                await ScalarStringAsync(path, $"SELECT status FROM quest_sessions WHERE id='{fourthQuest.QuestId}';", token));
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    private static async Task<StartedQuestSnapshot> StartQuestAsync(
        HeroPassportApplication app,
        HeroId heroId,
        ProjectBindingContext project,
        int questNumber,
        CancellationToken token) =>
        (await app.StartQuestAsync(
            new StartQuestRequest(
                MutationRequestId.New(),
                heroId,
                "coding",
                $"Unlock qualification {questNumber}",
                "Qualify deterministic monotonic unlock persistence through normal Quest progression."),
            project,
            token)).Quest;

    private static FinishQuestRequest FinishRequest(QuestId questId) =>
        new(
            MutationRequestId.New(),
            questId,
            "success",
            "Completed the unlock qualification Quest with observed tests and clean scope for deterministic progression.",
            new FinishQuestMetrics(
                TestsMentioned: true,
                ScopeViolations: 0,
                UserCorrections: 0,
                BuildStatus: "not_run",
                BuildEvidence: "none",
                TestsStatus: "passed",
                TestsEvidence: "observed"),
            ["coding"]);

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
