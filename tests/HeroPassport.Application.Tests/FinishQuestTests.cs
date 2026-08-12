using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Globalization;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class FinishQuestTests
{
    [Fact]
    public async Task FinishReplaysSameRequestAndRejectsChangedPayload()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var application = CreateApplication(path);
            var (hero, quest, project) = await StartCodingQuestAsync(application, 'a', cancellationToken);
            var requestId = MutationRequestId.New();
            var request = Finish(requestId, quest.QuestId, "success", "Completed the requested implementation cleanly.");

            var first = await application.FinishQuestAsync(request, project, cancellationToken);
            var replay = await application.FinishQuestAsync(request, project, cancellationToken);

            Assert.False(first.Replayed);
            Assert.False(first.AlreadyFinalized);
            Assert.True(replay.Replayed);
            Assert.True(replay.AlreadyFinalized);
            Assert.Equal(60, first.Reward.BaseXp);
            Assert.Equal(25, first.Reward.BonusXp);
            Assert.Equal(85, first.Reward.RawXp);
            Assert.Equal(1000, first.Reward.OutcomePermille);
            Assert.Equal(85, first.Reward.XpGained);
            Assert.Equal(0L, first.HeroProgress.TotalXpBefore);
            Assert.Equal(85L, first.HeroProgress.TotalXpAfter);
            Assert.Equal(hero.HeroId, first.HeroProgress.HeroId);
            Assert.Equal(first, replay with { Replayed = false });

            var changed = request with { Summary = "Different finalization payload." };
            var mismatch = await Assert.ThrowsAsync<HeroPassportException>(() => application.FinishQuestAsync(changed, project, cancellationToken));
            Assert.Equal("HP135", mismatch.Code);

            Assert.Equal(1L, await CountRowsAsync(path, "quest_reports", cancellationToken));
            Assert.Equal(1L, await CountRowsAsync(path, "xp_events", cancellationToken));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task FreshEquivalentFinishIsAcceptedButDifferentFinalizationConflicts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var application = CreateApplication(path);
            var (_, quest, project) = await StartCodingQuestAsync(application, 'b', cancellationToken);
            var first = Finish(MutationRequestId.New(), quest.QuestId, "partial", "Delivered a useful subset of the requested work.");

            var committed = await application.FinishQuestAsync(first, project, cancellationToken);
            var equivalent = first with { FinishRequestId = MutationRequestId.New() };
            var accepted = await application.FinishQuestAsync(equivalent, project, cancellationToken);

            Assert.Equal(51, committed.Reward.XpGained);
            Assert.False(accepted.Replayed);
            Assert.True(accepted.AlreadyFinalized);
            Assert.Equal(committed.Reward.XpGained, accepted.Reward.XpGained);

            var different = equivalent with
            {
                FinishRequestId = MutationRequestId.New(),
                Result = "success",
                Summary = "Claimed full success instead."
            };
            var conflict = await Assert.ThrowsAsync<HeroPassportException>(() => application.FinishQuestAsync(different, project, cancellationToken));
            Assert.Equal("HP136", conflict.Code);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task ActiveHeroSwitchCannotRedirectQuestXp()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var application = CreateApplication(path);
            var (hero, quest, project) = await StartCodingQuestAsync(application, 'c', cancellationToken);
            var other = await application.CreateHeroAsync(new CreateHeroRequest(MutationRequestId.New(), "Other"), cancellationToken);
            await application.ActivateHeroAsync(other.Hero.HeroId, cancellationToken);

            await application.FinishQuestAsync(
                Finish(MutationRequestId.New(), quest.QuestId, "success", "Completed work for the original Quest owner."),
                project,
                cancellationToken);

            Assert.Equal(85L, await HeroXpAsync(path, hero.HeroId, cancellationToken));
            Assert.Equal(0L, await HeroXpAsync(path, other.Hero.HeroId, cancellationToken));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task FinishRejectsProjectContextMismatch()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var application = CreateApplication(path);
            var (_, quest, _) = await StartCodingQuestAsync(application, 'd', cancellationToken);

            var error = await Assert.ThrowsAsync<HeroPassportException>(() => application.FinishQuestAsync(
                Finish(MutationRequestId.New(), quest.QuestId, "success", "Completed work in the original project."),
                Project("Other", 'e'),
                cancellationToken));

            Assert.Equal("HP134", error.Code);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task ConflictingConcurrentFinishesCommitExactlyOneOutcome()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var setupApplication = CreateApplication(path);
            var (_, quest, project) = await StartCodingQuestAsync(setupApplication, 'f', cancellationToken);
            var firstApplication = CreateApplication(path);
            var secondApplication = CreateApplication(path);

            var successTask = CaptureAsync(() => firstApplication.FinishQuestAsync(
                Finish(MutationRequestId.New(), quest.QuestId, "success", "Completed the full requested goal."),
                project,
                cancellationToken));
            var partialTask = CaptureAsync(() => secondApplication.FinishQuestAsync(
                Finish(MutationRequestId.New(), quest.QuestId, "partial", "Completed only a useful subset of the goal."),
                project,
                cancellationToken));

            var results = await Task.WhenAll(successTask, partialTask);

            Assert.Single(results, static result => result.Result is not null);
            var error = Assert.Single(results, static result => result.Error is not null).Error;
            Assert.Equal("HP136", Assert.IsType<HeroPassportException>(error).Code);
            Assert.Equal(1L, await CountRowsAsync(path, "quest_reports", cancellationToken));
            Assert.Equal(1L, await CountRowsAsync(path, "xp_events", cancellationToken));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    private static FinishQuestRequest Finish(MutationRequestId requestId, QuestId questId, string result, string summary) =>
        new(
            requestId,
            questId,
            result,
            summary,
            new FinishMetrics(false, 0, 0, "not_run", "none", "not_run", "none"),
            ["coding"]);

    private static async Task<(HeroSummary Hero, QuestSummary Quest, ProjectBindingContext Project)> StartCodingQuestAsync(
        HeroPassportApplication application,
        char fingerprintCharacter,
        CancellationToken cancellationToken)
    {
        var bootstrap = await application.BootstrapAsync(
            new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true),
            cancellationToken);
        var project = Project("Project", fingerprintCharacter);
        var start = await application.StartQuestAsync(
            new StartQuestRequest(MutationRequestId.New(), bootstrap.Hero.HeroId, "coding", "Coding Quest", "Implement a minimal conflict-safe FinishQuest path."),
            project,
            cancellationToken);
        return (bootstrap.Hero, start.Quest, project);
    }

    private static ProjectBindingContext Project(string name, char fingerprintCharacter) =>
        new(name, new string(fingerprintCharacter, 64), "project-identity/1");

    private static HeroPassportApplication CreateApplication(string databasePath) =>
        new(new SqliteHeroPassportStateStore(databasePath), new FixedTimeProvider(new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero)));

    private static async Task<(FinishQuestResult? Result, Exception? Error)> CaptureAsync(Func<Task<FinishQuestResult>> action)
    {
        try
        {
            return (await action(), null);
        }
        catch (Exception exception)
        {
            return (null, exception);
        }
    }

    private static async Task<long> CountRowsAsync(string path, string table, CancellationToken cancellationToken)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static async Task<long> HeroXpAsync(string path, HeroId heroId, CancellationToken cancellationToken)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT total_xp FROM heroes WHERE id=$id;";
        command.Parameters.AddWithValue("$id", heroId.ToString());
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static string CreateDatabasePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hero-passport-finish-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "hero-passport.db");
    }

    private static void DeleteDatabase(string path)
    {
        SqliteConnection.ClearAllPools();
        var directory = Path.GetDirectoryName(path);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
