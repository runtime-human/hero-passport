using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class HeroManagementTests
{
    [Fact]
    public async Task ListOrdersActiveThenAvailableThenArchived()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var application = CreateApplication(path);
            var initial = await BootstrapAsync(application, cancellationToken);
            var second = await application.CreateHeroAsync(new CreateHeroRequest(MutationRequestId.New(), "Second"), cancellationToken);
            var third = await application.CreateHeroAsync(new CreateHeroRequest(MutationRequestId.New(), "Third"), cancellationToken);

            await application.ActivateHeroAsync(second.Hero.HeroId, cancellationToken);
            await application.ArchiveHeroAsync(third.Hero.HeroId, cancellationToken);
            var list = await application.ListHeroesAsync(cancellationToken);

            Assert.Equal(3, list.Heroes.Count);
            Assert.Equal(second.Hero.HeroId, list.Heroes[0].HeroId);
            Assert.True(list.Heroes[0].Active);
            Assert.Equal(initial.Hero.HeroId, list.Heroes[1].HeroId);
            Assert.False(list.Heroes[1].Archived);
            Assert.Equal(third.Hero.HeroId, list.Heroes[2].HeroId);
            Assert.True(list.Heroes[2].Archived);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task ArchiveProtectsActiveHeroAndOpenQuestOwner()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var application = CreateApplication(path);
            var initial = await BootstrapAsync(application, cancellationToken);
            var second = await application.CreateHeroAsync(new CreateHeroRequest(MutationRequestId.New(), "Second"), cancellationToken);

            var activeError = await Assert.ThrowsAsync<HeroPassportException>(() => application.ArchiveHeroAsync(initial.Hero.HeroId, cancellationToken));
            Assert.Equal("HP145", activeError.Code);

            var project = Project('a');
            await application.StartQuestAsync(
                new StartQuestRequest(MutationRequestId.New(), second.Hero.HeroId, "coding", "Open work", "Keep this Hero protected while its Quest is open."),
                project,
                cancellationToken);
            var openError = await Assert.ThrowsAsync<HeroPassportException>(() => application.ArchiveHeroAsync(second.Hero.HeroId, cancellationToken));
            Assert.Equal("HP143", openError.Code);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task ArchiveAndRestoreAreIdempotentAndRestoreDoesNotActivate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var application = CreateApplication(path);
            var initial = await BootstrapAsync(application, cancellationToken);
            var second = await application.CreateHeroAsync(new CreateHeroRequest(MutationRequestId.New(), "Second"), cancellationToken);

            var archived = await application.ArchiveHeroAsync(second.Hero.HeroId, cancellationToken);
            var archivedAgain = await application.ArchiveHeroAsync(second.Hero.HeroId, cancellationToken);
            Assert.True(archived.Hero.Archived);
            Assert.True(archivedAgain.Hero.Archived);
            Assert.True(archivedAgain.AlreadyInRequestedState);

            var restored = await application.RestoreHeroAsync(second.Hero.HeroId, cancellationToken);
            var restoredAgain = await application.RestoreHeroAsync(second.Hero.HeroId, cancellationToken);
            Assert.False(restored.Hero.Archived);
            Assert.False(restoredAgain.Hero.Archived);
            Assert.True(restoredAgain.AlreadyInRequestedState);

            var list = await application.ListHeroesAsync(cancellationToken);
            Assert.Equal(initial.Hero.HeroId, Assert.Single(list.Heroes, static hero => hero.Active).HeroId);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task GetCardUsesExplicitHeroAndCurrentProjectProjection()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var application = CreateApplication(path);
            var initial = await BootstrapAsync(application, cancellationToken);
            var second = await application.CreateHeroAsync(new CreateHeroRequest(MutationRequestId.New(), "Second"), cancellationToken);
            var project = Project('b');

            var start = await application.StartQuestAsync(
                new StartQuestRequest(MutationRequestId.New(), initial.Hero.HeroId, "coding", "Card work", "Create project statistics for the explicit Hero card."),
                project,
                cancellationToken);
            await application.FinishQuestAsync(
                new FinishQuestRequest(
                    MutationRequestId.New(),
                    start.Quest.QuestId,
                    "success",
                    "Completed the Quest used to populate the current project projection.",
                    new FinishMetrics(false, 0, 0, "not_run", "none", "not_run", "none"),
                    ["coding"]),
                project,
                cancellationToken);
            await application.ActivateHeroAsync(second.Hero.HeroId, cancellationToken);

            var card = await application.GetHeroCardAsync(initial.Hero.HeroId, project, cancellationToken);

            Assert.Equal(initial.Hero.HeroId, card.Hero.HeroId);
            Assert.Equal(85L, card.Hero.TotalXp);
            Assert.Equal(1, card.Hero.Level);
            Assert.Equal(85L, card.Hero.LevelXp);
            Assert.Equal(100L, card.Hero.NextLevelXpRequired);
            Assert.Equal(52, card.Hero.Trust);
            Assert.Equal(18, card.Hero.Strain);
            Assert.Equal(1, card.Hero.SuccessStreak);
            Assert.Equal(1, card.Project.QuestsStarted);
            Assert.Equal(1, card.Project.QuestsFinished);
            Assert.Equal(1, card.Project.QuestsSucceeded);
            Assert.Equal(85L, card.Project.TotalXpEarned);
            Assert.Equal(1000, card.Project.SuccessRatePermille);
            Assert.Equal("Project", card.Project.DisplayName);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    private static async Task<BootstrapResult> BootstrapAsync(HeroPassportApplication application, CancellationToken cancellationToken) =>
        await application.BootstrapAsync(
            new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true),
            cancellationToken);

    private static ProjectBindingContext Project(char fingerprintCharacter) =>
        new("Project", new string(fingerprintCharacter, 64), "project-identity/1");

    private static HeroPassportApplication CreateApplication(string databasePath) =>
        new(new SqliteHeroPassportStateStore(databasePath), new FixedTimeProvider(new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero)));

    private static string CreateDatabasePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hero-passport-hero-management-tests", Guid.NewGuid().ToString("N"));
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
