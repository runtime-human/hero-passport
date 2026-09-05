using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class StartQuestBehaviorTests
{
    [Fact]
    public async Task SameRequestReplaysAndChangedHeroOrProjectConflicts()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var firstHero = (await app.BootstrapAsync(new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true), token)).Hero;
            var secondHero = (await app.CreateHeroAsync(new CreateHeroRequest(MutationRequestId.New(), "Other"), token)).Hero;
            var project = Project('a', "Project");
            var request = new StartQuestRequest(MutationRequestId.New(), firstHero.HeroId, "coding", "Quest", "Implement durable quest start");

            var first = await app.StartQuestAsync(request, project, token);
            var replay = await app.StartQuestAsync(request, project, token);

            Assert.False(first.Replayed);
            Assert.True(replay.Replayed);
            Assert.Equal(first.Quest, replay.Quest);

            var changedHero = await Assert.ThrowsAsync<HeroPassportException>(() =>
                app.StartQuestAsync(request with { HeroId = secondHero.HeroId }, project, token));
            Assert.Equal("HP135", changedHero.Code);

            var changedProject = await Assert.ThrowsAsync<HeroPassportException>(() =>
                app.StartQuestAsync(request, Project('b', "Other Project"), token));
            Assert.Equal("HP135", changedProject.Code);
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task ExplicitHeroAndOriginalLocaleSurvivePreferenceChanges()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var explicitHero = (await app.BootstrapAsync(new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true), token)).Hero;
            var otherHero = (await app.CreateHeroAsync(new CreateHeroRequest(MutationRequestId.New(), "Other"), token)).Hero;
            await app.ActivateHeroAsync(otherHero.HeroId, token);

            var project = Project('c', "Project");
            var request = new StartQuestRequest(MutationRequestId.New(), explicitHero.HeroId, "research", "Research", "Verify explicit Hero ownership");
            var started = await app.StartQuestAsync(request, project, token);

            Assert.Equal(explicitHero.HeroId, started.Quest.HeroId);
            Assert.Equal("en-US", started.Quest.Locale);

            await app.ConfigureAsync(new ConfigureRequest("ru-RU", "minimal", false, false), token);
            var replay = await app.StartQuestAsync(request, project, token);

            Assert.True(replay.Replayed);
            Assert.Equal(explicitHero.HeroId, replay.Quest.HeroId);
            Assert.Equal("en-US", replay.Quest.Locale);
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task ConcurrentFreshStartsSameHeroProjectCreateOneOpenQuest()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var hero = (await app.BootstrapAsync(new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true), token)).Hero;
            var project = Project('d', "Project");
            var first = TestRuntime.CreateApplication(path);
            var second = TestRuntime.CreateApplication(path);

            var results = await Task.WhenAll(
                CaptureAsync(() => first.StartQuestAsync(new StartQuestRequest(MutationRequestId.New(), hero.HeroId, "coding", "First", "First independent start"), project, token)),
                CaptureAsync(() => second.StartQuestAsync(new StartQuestRequest(MutationRequestId.New(), hero.HeroId, "coding", "Second", "Second independent start"), project, token)));

            Assert.Single(results, static result => result.Result is not null);
            Assert.Equal("HP133", Assert.IsType<HeroPassportException>(Assert.Single(results, static result => result.Error is not null).Error).Code);
            Assert.Equal(1, await ScalarLongAsync(path, "SELECT COUNT(*) FROM quest_sessions WHERE status='open';", token));
            Assert.Equal(1, await ScalarLongAsync(path, "SELECT COUNT(*) FROM mutation_receipts WHERE operation_key='start_quest';", token));
            Assert.Equal(1, await ScalarLongAsync(path, "SELECT quests_started FROM hero_project_stats;", token));
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task DifferentHeroesOrProjectsCanHaveIndependentOpenQuests()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var firstHero = (await app.BootstrapAsync(new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true), token)).Hero;
            var secondHero = (await app.CreateHeroAsync(new CreateHeroRequest(MutationRequestId.New(), "Other"), token)).Hero;
            var firstProject = Project('e', "First Project");
            var secondProject = Project('f', "Second Project");

            await app.StartQuestAsync(new StartQuestRequest(MutationRequestId.New(), firstHero.HeroId, "coding", "A", "First Hero first project"), firstProject, token);
            await app.StartQuestAsync(new StartQuestRequest(MutationRequestId.New(), secondHero.HeroId, "coding", "B", "Second Hero same project"), firstProject, token);
            await app.StartQuestAsync(new StartQuestRequest(MutationRequestId.New(), firstHero.HeroId, "coding", "C", "First Hero second project"), secondProject, token);

            Assert.Equal(3, await ScalarLongAsync(path, "SELECT COUNT(*) FROM quest_sessions WHERE status='open';", token));
            Assert.Equal(3, await ScalarLongAsync(path, "SELECT SUM(quests_started) FROM hero_project_stats;", token));
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task SameFingerprintWithDifferentDisplayNameStillSharesOneOpenScope()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var hero = (await app.BootstrapAsync(new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true), token)).Hero;
            var fingerprint = new string('1', 64);

            await app.StartQuestAsync(
                new StartQuestRequest(MutationRequestId.New(), hero.HeroId, "coding", "Worktree A", "Start in linked worktree A"),
                new ProjectBindingContext("Worktree A", fingerprint, "project-identity/1"),
                token);

            var conflict = await Assert.ThrowsAsync<HeroPassportException>(() =>
                app.StartQuestAsync(
                    new StartQuestRequest(MutationRequestId.New(), hero.HeroId, "coding", "Worktree B", "Start in linked worktree B"),
                    new ProjectBindingContext("Worktree B", fingerprint, "project-identity/1"),
                    token));

            Assert.Equal("HP133", conflict.Code);
            Assert.Equal(1, await ScalarLongAsync(path, "SELECT COUNT(*) FROM projects;", token));
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task FailureBeforeReceiptCommitRollsBackProjectQuestStatsAndReceipt()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var hero = (await app.BootstrapAsync(new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true), token)).Hero;
            await ExecuteSqlAsync(path,
                """
                CREATE TRIGGER fail_start_receipt
                BEFORE INSERT ON mutation_receipts
                WHEN NEW.operation_key = 'start_quest'
                BEGIN
                    SELECT RAISE(ABORT, 'start-test-failure');
                END;
                """,
                token);

            var request = new StartQuestRequest(MutationRequestId.New(), hero.HeroId, "coding", "Quest", "Prove transactional rollback");
            await Assert.ThrowsAsync<SqliteException>(() => app.StartQuestAsync(request, Project('2', "New Project"), token));

            Assert.Equal(0, await ScalarLongAsync(path, "SELECT COUNT(*) FROM projects;", token));
            Assert.Equal(0, await ScalarLongAsync(path, "SELECT COUNT(*) FROM quest_sessions;", token));
            Assert.Equal(0, await ScalarLongAsync(path, "SELECT COUNT(*) FROM hero_project_stats;", token));
            Assert.Equal(0, await ScalarLongAsync(path, "SELECT COUNT(*) FROM mutation_receipts WHERE operation_key='start_quest';", token));
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task FreshApplicationReplaysCommittedStartAndInvalidInputsUseStableErrors()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var hero = (await app.BootstrapAsync(new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true), token)).Hero;
            var project = Project('3', "Project");
            var request = new StartQuestRequest(MutationRequestId.New(), hero.HeroId, "coding", "Quest", "Committed start replay");

            var committed = await app.StartQuestAsync(request, project, token);
            var replay = await TestRuntime.CreateApplication(path).StartQuestAsync(request, project, token);

            Assert.True(replay.Replayed);
            Assert.Equal(committed.Quest, replay.Quest);

            var invalidType = await Assert.ThrowsAsync<HeroPassportException>(() =>
                app.StartQuestAsync(new StartQuestRequest(MutationRequestId.New(), hero.HeroId, "invalid", "Quest", "Goal"), Project('4', "Invalid"), token));
            Assert.Equal("HP110", invalidType.Code);
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    private static ProjectBindingContext Project(char fingerprintCharacter, string displayName) =>
        new(displayName, new string(fingerprintCharacter, 64), "project-identity/1");

    private static async Task<(StartQuestResult? Result, Exception? Error)> CaptureAsync(Func<Task<StartQuestResult>> action)
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
        return Convert.ToInt64(await command.ExecuteScalarAsync(token));
    }
}
